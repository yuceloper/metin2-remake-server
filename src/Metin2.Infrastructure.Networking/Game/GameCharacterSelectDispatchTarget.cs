using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Send;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Characters.Application;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Protocol.Legacy;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Game;

public sealed class GameCharacterSelectDispatchTarget
{
    private const int PhaseFrameSize = 1 + PhaseCodec.PayloadSize;

    private readonly GameSession _session;
    private readonly LegacyPacketOutput _output;
    private readonly CharacterSelectService _selectService;
    private readonly ILegacyCharacterBootstrapPublisher _bootstrapPublisher;
    private readonly Action? _selectionCompleted;

    public GameCharacterSelectDispatchTarget(
        GameSession session,
        PipeWriter output,
        CharacterSelectService selectService,
        ILegacyCharacterBootstrapPublisher bootstrapPublisher,
        Action? selectionCompleted = null)
        : this(session, new LegacyPacketOutput(output, session), selectService, bootstrapPublisher, selectionCompleted)
    {
    }

    public GameCharacterSelectDispatchTarget(
        GameSession session,
        LegacyPacketOutput output,
        CharacterSelectService selectService,
        ILegacyCharacterBootstrapPublisher bootstrapPublisher,
        Action? selectionCompleted = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(selectService);
        ArgumentNullException.ThrowIfNull(bootstrapPublisher);
        _session = session;
        _output = output;
        _selectService = selectService;
        _bootstrapPublisher = bootstrapPublisher;
        _selectionCompleted = selectionCompleted;
    }

    public async ValueTask HandleAsync(SelectCharacter packet, CancellationToken cancellationToken)
    {
        if (_session.Phase != PacketPhase.Select || _session.AccountId is not AccountId accountId)
        {
            throw new CharacterSelectRejectedException(packet.Slot);
        }

        CharacterSelectResult result = await _selectService
            .SelectAsync(accountId, packet.Slot, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            throw new CharacterSelectRejectedException(packet.Slot);
        }

        var phase = new Phase((byte)LegacyPhaseCode.Loading);
        Span<byte> frame = stackalloc byte[PhaseFrameSize];
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in phase, frame, out int written);
        if (status != PacketFrameWriteStatus.Done || written != PhaseFrameSize)
        {
            throw new InvalidOperationException(
                $"Loading phase frame could not be written: {status} ({written} bytes)." );
        }

        _output.Write(frame[..written]);
        await _output.FlushAsync(cancellationToken).ConfigureAwait(false);

        _session.SelectCharacter(result.CharacterId);
        _session.TransitionTo(PacketPhase.Loading);
        _selectionCompleted?.Invoke();

        await _bootstrapPublisher.PublishAsync(_session, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class CharacterSelectRejectedException : Exception
{
    public CharacterSelectRejectedException(byte slot)
        : base($"Character selection was rejected for slot {slot}.")
    {
    }
}
