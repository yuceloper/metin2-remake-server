using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Characters.Application;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Protocol.Legacy;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Game;

public sealed class GameCharacterSelectDispatchTarget(
    GameSession session,
    PipeWriter output,
    CharacterSelectService selectService)
{
    private const int PhaseFrameSize = 1 + PhaseCodec.PayloadSize;

    public async ValueTask HandleAsync(SelectCharacter packet, CancellationToken cancellationToken)
    {
        if (session.Phase != PacketPhase.Select || session.AccountId is not AccountId accountId)
        {
            throw new CharacterSelectRejectedException(packet.Slot);
        }

        CharacterSelectResult result = await selectService
            .SelectAsync(accountId, packet.Slot, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            throw new CharacterSelectRejectedException(packet.Slot);
        }

        var phase = new Phase((byte)LegacyPhaseCode.Loading);
        Memory<byte> memory = output.GetMemory(PhaseFrameSize);
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in phase, memory.Span, out int written);
        if (status != PacketFrameWriteStatus.Done || written != PhaseFrameSize)
        {
            throw new InvalidOperationException(
                $"Loading phase frame could not be written: {status} ({written} bytes)." );
        }

        output.Advance(written);
        FlushResult flush = await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        if (flush.IsCanceled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        session.SelectCharacter(result.CharacterId);
        session.TransitionTo(PacketPhase.Loading);
    }
}

public sealed class CharacterSelectRejectedException : Exception
{
    public CharacterSelectRejectedException(byte slot)
        : base($"Character selection was rejected for slot {slot}.")
    {
    }
}
