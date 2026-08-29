using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;

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

    private readonly GameSession _session;
    private readonly PipeWriter _output;
    private readonly IServerTimeProvider _timeProvider;
    private readonly LegacyHandshakeState _state;
    private readonly PacketPhase _nextPhase;

    public LegacyHandshakeDispatchTarget(
        GameSession session,
        PipeWriter output,
        IServerTimeProvider timeProvider,
        IHandshakeTokenSource tokenSource,
        PacketPhase nextPhase)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(tokenSource);

        if (nextPhase is PacketPhase.Handshake or PacketPhase.Any)
        {
            throw new ArgumentOutOfRangeException(nameof(nextPhase), "Post-handshake phase must be a concrete non-handshake phase.");
        }

        _session = session;
        _output = output;
        _timeProvider = timeProvider;
        _nextPhase = nextPhase;
        _state = new LegacyHandshakeState(tokenSource.NextToken());
    }

    public uint Token => _state.Token;

    public bool IsCompleted => !_state.IsActive && _session.Phase == _nextPhase;

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        Handshake packet = _state.Start(_timeProvider.GetMilliseconds());
        await WriteHandshakeAsync(packet, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(Handshake packet, CancellationToken cancellationToken)
    {
        LegacyHandshakeResult result = _state.Handle(in packet, _timeProvider.GetMilliseconds());

        switch (result.Decision)
        {
            case LegacyHandshakeDecision.Completed:
                _session.TransitionTo(_nextPhase);
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

    private async ValueTask WriteHandshakeAsync(Handshake packet, CancellationToken cancellationToken)
    {
        Memory<byte> memory = _output.GetMemory(HandshakeFrameSize);
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, memory.Span, out int written);
        if (status != PacketFrameWriteStatus.Done || written != HandshakeFrameSize)
        {
            throw new InvalidOperationException($"Handshake frame could not be written: {status} ({written} bytes).");
        }

        _output.Advance(written);
        FlushResult flush = await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
        if (flush.IsCanceled)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private static ValueTask Unsupported<TPacket>(TPacket packet) =>
        ValueTask.FromException(new InvalidOperationException(
            $"Packet '{typeof(TPacket).Name}' is not handled by the handshake target."));
}
