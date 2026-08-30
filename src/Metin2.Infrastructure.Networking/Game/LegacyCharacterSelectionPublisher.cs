using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Characters.Application;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Protocol.Generated.Types;
using Metin2.Protocol.Legacy;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Game;

public interface ILegacyCharacterSelectionPublisher
{
    ValueTask PublishAsync(GameSession session, CancellationToken cancellationToken = default);
}

public sealed class LegacyCharacterSelectionPublisher(
    PipeWriter output,
    CharacterSelectionService selectionService,
    ILegacyCharacterSelectionWireContextProvider contextProvider) : ILegacyCharacterSelectionPublisher
{
    private const int EmpireFrameSize = 1 + EmpireCodec.PayloadSize + 1;
    private const int PhaseFrameSize = 1 + PhaseCodec.PayloadSize;
    private const int CharactersFrameSize = 1 + CharactersCodec.PayloadSize;
    private const int TotalFrameSize = EmpireFrameSize + PhaseFrameSize + CharactersFrameSize;

    public async ValueTask PublishAsync(GameSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!session.IsAuthenticated || session.AccountId is not AccountId accountId)
        {
            throw new InvalidOperationException("Character selection cannot be published before Game authentication.");
        }

        CharacterSelectionSnapshot snapshot = await selectionService
            .GetAsync(accountId, cancellationToken)
            .ConfigureAwait(false);
        LegacyCharacterSelectionWireContext context = contextProvider.Get(session);

        CharacterSummary[] summaries = new CharacterSummary[4];
        GuildId[] guildIds = new GuildId[4];
        string[] guildNames = new string[4];

        foreach (CharacterListEntry entry in snapshot.Characters)
        {
            int slot = entry.Slot;
            summaries[slot] = new CharacterSummary(
                entry.CharacterId,
                entry.Name,
                entry.Class,
                entry.Level,
                entry.PlaytimeMinutes,
                entry.Strength,
                entry.Vitality,
                entry.Dexterity,
                entry.Intelligence,
                entry.BodyPart,
                entry.NameChange,
                entry.HairPart,
                0,
                entry.PositionX,
                entry.PositionY,
                context.AddressWireValue,
                context.Port,
                entry.SkillGroup);
            guildIds[slot] = entry.GuildId;
            guildNames[slot] = entry.GuildName;
        }

        var empire = new Empire(snapshot.Empire);
        var phase = new Phase((byte)LegacyPhaseCode.Select);
        var characters = new Characters(
            summaries,
            guildIds,
            guildNames,
            context.Handle,
            context.RandomKey);

        Memory<byte> memory = output.GetMemory(TotalFrameSize);
        Span<byte> destination = memory.Span;
        int offset = 0;

        offset += WriteSequenced(in empire, context.EmpireSequence, destination[offset..], EmpireFrameSize);
        offset += Write(in phase, destination[offset..], PhaseFrameSize);
        offset += Write(in characters, destination[offset..], CharactersFrameSize);

        if (offset != TotalFrameSize)
        {
            throw new InvalidOperationException($"Character selection batch size mismatch: {offset} != {TotalFrameSize}.");
        }

        output.Advance(offset);
        FlushResult flush = await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        if (flush.IsCanceled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        session.TransitionTo(PacketPhase.Select);
    }

    private static int Write<TPacket>(in TPacket packet, Span<byte> destination, int expectedSize)
        where TPacket : struct
    {
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, destination, out int written);
        if (status != PacketFrameWriteStatus.Done || written != expectedSize)
        {
            throw new InvalidOperationException(
                $"Selection packet '{typeof(TPacket).Name}' could not be written: {status} ({written} bytes)." );
        }

        return written;
    }

    private static int WriteSequenced<TPacket>(in TPacket packet, byte sequence, Span<byte> destination, int expectedSize)
        where TPacket : struct
    {
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, sequence, destination, out int written);
        if (status != PacketFrameWriteStatus.Done || written != expectedSize)
        {
            throw new InvalidOperationException(
                $"Sequenced selection packet '{typeof(TPacket).Name}' could not be written: {status} ({written} bytes)." );
        }

        return written;
    }
}
