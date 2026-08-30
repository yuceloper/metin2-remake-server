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

        PacketFrameWriteStatus empireStatus = PacketFrameWriter.TryWrite(
            in empire,
            context.EmpireSequence,
            destination[offset..],
            out int empireWritten);
        EnsureWritten(nameof(Empire), empireStatus, empireWritten, EmpireFrameSize);
        offset += empireWritten;

        PacketFrameWriteStatus phaseStatus = PacketFrameWriter.TryWrite(
            in phase,
            destination[offset..],
            out int phaseWritten);
        EnsureWritten(nameof(Phase), phaseStatus, phaseWritten, PhaseFrameSize);
        offset += phaseWritten;

        PacketFrameWriteStatus charactersStatus = PacketFrameWriter.TryWrite(
            in characters,
            destination[offset..],
            out int charactersWritten);
        EnsureWritten(nameof(Characters), charactersStatus, charactersWritten, CharactersFrameSize);
        offset += charactersWritten;

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

    private static void EnsureWritten(
        string packetName,
        PacketFrameWriteStatus status,
        int written,
        int expectedSize)
    {
        if (status != PacketFrameWriteStatus.Done || written != expectedSize)
        {
            throw new InvalidOperationException(
                $"Selection packet '{packetName}' could not be written: {status} ({written} bytes)." );
        }
    }
}
