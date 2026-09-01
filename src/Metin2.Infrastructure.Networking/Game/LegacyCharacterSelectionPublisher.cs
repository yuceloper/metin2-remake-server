using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Send;
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

public sealed class LegacyCharacterSelectionPublisher : ILegacyCharacterSelectionPublisher
{
    private const int EmpireFrameSize = 1 + EmpireCodec.PayloadSize + 1;
    private const int PhaseFrameSize = 1 + PhaseCodec.PayloadSize;
    private const int CharactersFrameSize = 1 + CharactersCodec.PayloadSize;

    private readonly LegacyPacketOutput _output;
    private readonly CharacterSelectionService _selectionService;
    private readonly ILegacyCharacterSelectionWireContextProvider _contextProvider;

    public LegacyCharacterSelectionPublisher(
        PipeWriter output,
        CharacterSelectionService selectionService,
        ILegacyCharacterSelectionWireContextProvider contextProvider)
        : this(new LegacyPacketOutput(output, new GameSession()), selectionService, contextProvider)
    {
    }

    public LegacyCharacterSelectionPublisher(
        LegacyPacketOutput output,
        CharacterSelectionService selectionService,
        ILegacyCharacterSelectionWireContextProvider contextProvider)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(selectionService);
        ArgumentNullException.ThrowIfNull(contextProvider);
        _output = output;
        _selectionService = selectionService;
        _contextProvider = contextProvider;
    }

    public async ValueTask PublishAsync(GameSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!session.IsAuthenticated || session.AccountId is not AccountId accountId)
        {
            throw new InvalidOperationException("Character selection cannot be published before Game authentication.");
        }

        CharacterSelectionSnapshot snapshot = await _selectionService
            .GetAsync(accountId, cancellationToken)
            .ConfigureAwait(false);
        LegacyCharacterSelectionWireContext context = _contextProvider.Get(session);

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
        var characters = new Characters(summaries, guildIds, guildNames, context.Handle, context.RandomKey);

        Span<byte> empireFrame = stackalloc byte[EmpireFrameSize];
        PacketFrameWriteStatus empireStatus = PacketFrameWriter.TryWrite(
            in empire,
            context.EmpireSequence,
            empireFrame,
            out int empireWritten);
        EnsureWritten(nameof(Empire), empireStatus, empireWritten, EmpireFrameSize);
        _output.Write(empireFrame[..empireWritten]);

        Span<byte> phaseFrame = stackalloc byte[PhaseFrameSize];
        PacketFrameWriteStatus phaseStatus = PacketFrameWriter.TryWrite(in phase, phaseFrame, out int phaseWritten);
        EnsureWritten(nameof(Phase), phaseStatus, phaseWritten, PhaseFrameSize);
        _output.Write(phaseFrame[..phaseWritten]);

        Span<byte> charactersFrame = stackalloc byte[CharactersFrameSize];
        PacketFrameWriteStatus charactersStatus = PacketFrameWriter.TryWrite(in characters, charactersFrame, out int charactersWritten);
        EnsureWritten(nameof(Characters), charactersStatus, charactersWritten, CharactersFrameSize);
        _output.Write(charactersFrame[..charactersWritten]);

        await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
        session.TransitionTo(PacketPhase.Select);
    }

    private static void EnsureWritten(string packetName, PacketFrameWriteStatus status, int written, int expectedSize)
    {
        if (status != PacketFrameWriteStatus.Done || written != expectedSize)
        {
            throw new InvalidOperationException(
                $"Selection packet '{packetName}' could not be written: {status} ({written} bytes)." );
        }
    }
}
