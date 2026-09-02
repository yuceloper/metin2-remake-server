using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Send;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Protocol.Legacy;
using HandshakePacket = Metin2.Protocol.Generated.Packets.Handshake;

namespace Metin2.Infrastructure.Networking.Handshake;

public sealed class LegacyHandshakeRejectedException : Exception
{
    public LegacyHandshakeRejectedException(LegacyHandshakeDecision decision)
        : base($"Legacy handshake rejected: {decision}.")
    {
        Decision = decision;
    }

    public LegacyHandshakeDecision Decision { get; }
}

public sealed class LegacyHandshakeDispatchTarget : IPacketDispatchTarget
{
    private const int HandshakeFrameSize = 13;
    private const int PhaseFrameSize = 2;

    private readonly GameSession _session;
    private readonly LegacyPacketOutput _output;
    private readonly IServerTimeProvider _timeProvider;
    private readonly LegacyHandshakeState _state;
    private readonly PacketPhase _nextPhase;
    private readonly Func<GameSession, CancellationToken, ValueTask>? _onCompleted;
    private readonly bool _deferPostHandshakePhase;

    public LegacyHandshakeDispatchTarget(
        GameSession session,
        PipeWriter output,
        IServerTimeProvider timeProvider,
        IHandshakeTokenSource tokenSource,
        PacketPhase nextPhase,
        Func<GameSession, CancellationToken, ValueTask>? onCompleted = null,
        bool deferPostHandshakePhase = false)
        : this(session, new LegacyPacketOutput(output, session), timeProvider, tokenSource, nextPhase, onCompleted, deferPostHandshakePhase)
    {
    }

    public LegacyHandshakeDispatchTarget(
        GameSession session,
        LegacyPacketOutput output,
        IServerTimeProvider timeProvider,
        IHandshakeTokenSource tokenSource,
        PacketPhase nextPhase,
        Func<GameSession, CancellationToken, ValueTask>? onCompleted = null,
        bool deferPostHandshakePhase = false)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(tokenSource);

        if (nextPhase is PacketPhase.Handshake or PacketPhase.Any)
        {
            throw new ArgumentOutOfRangeException(nameof(nextPhase), "Post-handshake phase must be a concrete non-handshake phase.");
        }
        if (deferPostHandshakePhase && onCompleted is null)
        {
            throw new ArgumentException("Deferred post-handshake phase requires a completion callback.", nameof(onCompleted));
        }

        _session = session;
        _output = output;
        _timeProvider = timeProvider;
        _nextPhase = nextPhase;
        _onCompleted = onCompleted;
        _deferPostHandshakePhase = deferPostHandshakePhase;
        _state = new LegacyHandshakeState(tokenSource.NextToken());
    }

    public uint Token => _state.Token;

    public bool IsCompleted => !_state.IsActive && (_deferPostHandshakePhase || _session.Phase == _nextPhase);

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        await WritePhaseAsync(LegacyPhaseCode.Handshake, cancellationToken).ConfigureAwait(false);

        HandshakePacket packet = _state.Start(_timeProvider.GetMilliseconds());
        await WriteHandshakeAsync(packet, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(HandshakePacket packet, CancellationToken cancellationToken)
    {
        LegacyHandshakeResult result = _state.Handle(in packet, _timeProvider.GetMilliseconds());

        switch (result.Decision)
        {
            case LegacyHandshakeDecision.Completed:
                if (_deferPostHandshakePhase)
                {
                    await _onCompleted!(_session, cancellationToken).ConfigureAwait(false);
                    return;
                }

                _session.TransitionTo(_nextPhase);
                await WritePhaseAsync(MapWirePhase(_nextPhase), cancellationToken).ConfigureAwait(false);
                if (_onCompleted is not null)
                {
                    await _onCompleted(_session, cancellationToken).ConfigureAwait(false);
                }
                return;

            case LegacyHandshakeDecision.Retry:
                await WriteHandshakeAsync(result.Response!.Value, cancellationToken).ConfigureAwait(false);
                return;

            case LegacyHandshakeDecision.RejectedWrongToken:
            case LegacyHandshakeDecision.RejectedUnexpectedPacket:
                throw new LegacyHandshakeRejectedException(result.Decision);

            default:
                throw new InvalidOperationException($"Unknown handshake decision '{result.Decision}'.");
        }
    }

    public ValueTask HandleAsync(LoginFailed packet, CancellationToken cancellationToken) => Unsupported(packet);
    public ValueTask HandleAsync(LoginRequest packet, CancellationToken cancellationToken) => Unsupported(packet);
    public ValueTask HandleAsync(LoginSuccess packet, CancellationToken cancellationToken) => Unsupported(packet);
    public ValueTask HandleAsync(Phase packet, CancellationToken cancellationToken) => Unsupported(packet);
    public ValueTask HandleAsync(TokenLogin packet, CancellationToken cancellationToken) => Unsupported(packet);

    private async ValueTask WriteHandshakeAsync(HandshakePacket packet, CancellationToken cancellationToken)
    {
        Span<byte> frame = stackalloc byte[HandshakeFrameSize];
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, frame, out int written);
        if (status != PacketFrameWriteStatus.Done || written != HandshakeFrameSize)
        {
            throw new InvalidOperationException($"Handshake frame could not be written: {status} ({written} bytes).");
        }

        _output.Write(frame[..written]);
        await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask WritePhaseAsync(LegacyPhaseCode phaseCode, CancellationToken cancellationToken)
    {
        var packet = new Phase((byte)phaseCode);
        Span<byte> frame = stackalloc byte[PhaseFrameSize];
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, frame, out int written);
        if (status != PacketFrameWriteStatus.Done || written != PhaseFrameSize)
        {
            throw new InvalidOperationException($"Phase frame could not be written: {status} ({written} bytes).");
        }

        _output.Write(frame[..written]);
        await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static LegacyPhaseCode MapWirePhase(PacketPhase phase) =>
        phase switch
        {
            PacketPhase.Login => LegacyPhaseCode.Login,
            PacketPhase.Auth => LegacyPhaseCode.Auth,
            PacketPhase.Select => LegacyPhaseCode.Select,
            PacketPhase.Loading => LegacyPhaseCode.Loading,
            PacketPhase.Game => LegacyPhaseCode.Game,
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unsupported post-handshake phase.")
        };

    private static ValueTask Unsupported<TPacket>(TPacket packet) =>
        ValueTask.FromException(new InvalidOperationException(
            $"Packet '{typeof(TPacket).Name}' is not handled by the handshake target."));
}
