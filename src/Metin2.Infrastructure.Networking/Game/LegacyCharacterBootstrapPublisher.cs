using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Send;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Characters.Application;
using Metin2.Modules.World;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Game;

public interface ILegacyCharacterBootstrapPublisher
{
    ValueTask PublishAsync(GameSession session, CancellationToken cancellationToken = default);
}

public sealed class LegacyCharacterBootstrapPublisher : ILegacyCharacterBootstrapPublisher
{
    private const int PointCount = 255;
    private const int PartCount = 4;
    private const int AffectCount = 2;
    private const int CharacterDetailsFrameSize = 1 + CharacterDetailsCodec.PayloadSize;
    private const int CharacterPointsFrameSize = 1 + CharacterPointsCodec.PayloadSize;
    private const int CharacterUpdateFrameSize = 1 + CharacterUpdateCodec.PayloadSize;
    private const int TotalFrameSize = CharacterDetailsFrameSize + CharacterPointsFrameSize + CharacterUpdateFrameSize;

    private readonly LegacyPacketOutput _output;
    private readonly CharacterBootstrapService _bootstrapService;
    private readonly ILegacyCharacterBootstrapRuntimeContextProvider _runtimeContextProvider;
    private readonly PlayerRuntimeRegistry _runtimeRegistry;

    public LegacyCharacterBootstrapPublisher(
        PipeWriter output,
        CharacterBootstrapService bootstrapService,
        ILegacyCharacterBootstrapRuntimeContextProvider runtimeContextProvider,
        PlayerRuntimeRegistry runtimeRegistry)
        : this(new LegacyPacketOutput(output), bootstrapService, runtimeContextProvider, runtimeRegistry)
    {
    }

    public LegacyCharacterBootstrapPublisher(
        LegacyPacketOutput output,
        CharacterBootstrapService bootstrapService,
        ILegacyCharacterBootstrapRuntimeContextProvider runtimeContextProvider,
        PlayerRuntimeRegistry runtimeRegistry)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(bootstrapService);
        ArgumentNullException.ThrowIfNull(runtimeContextProvider);
        ArgumentNullException.ThrowIfNull(runtimeRegistry);
        _output = output;
        _bootstrapService = bootstrapService;
        _runtimeContextProvider = runtimeContextProvider;
        _runtimeRegistry = runtimeRegistry;
    }

    public async ValueTask PublishAsync(GameSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Phase != PacketPhase.Loading ||
            session.AccountId is not AccountId accountId ||
            session.SelectedCharacterId is not CharacterId characterId)
        {
            throw new InvalidOperationException("Character bootstrap requires an authenticated selected character in Loading phase.");
        }

        CharacterBootstrapSnapshot snapshot = await _bootstrapService
            .GetRequiredOwnedAsync(accountId, characterId, cancellationToken)
            .ConfigureAwait(false);

        var position = new Position(snapshot.MapId, snapshot.PositionX, snapshot.PositionY);
        if (!_runtimeRegistry.TryReserve(snapshot.CharacterId, position, out PlayerRuntimeReservation reservation))
        {
            throw new InvalidOperationException($"Character {snapshot.CharacterId} already has an active runtime reservation.");
        }

        bool bound = false;
        try
        {
            session.BindRuntimeEntity(reservation.EntityId);
            bound = true;

            LegacyCharacterBootstrapRuntimeContext runtime = _runtimeContextProvider.Get(session, in snapshot);
            if (runtime.Points.Length != PointCount) throw new InvalidOperationException($"Legacy point projection must contain exactly {PointCount} values.");
            if (runtime.Parts.Length != PartCount) throw new InvalidOperationException($"Legacy character update requires exactly {PartCount} parts.");
            if (runtime.Affects.Length != AffectCount) throw new InvalidOperationException($"Legacy character update requires exactly {AffectCount} affect words.");

            uint[] points = runtime.Points.ToArray();
            ApplyDurablePoints(points, in snapshot);
            uint vid = reservation.EntityId.Value;

            var details = new CharacterDetails(vid, snapshot.Class, snapshot.Name, snapshot.PositionX, snapshot.PositionY, 0, snapshot.Empire, snapshot.SkillGroup);
            var pointPacket = new CharacterPoints(points);
            var update = new CharacterUpdate(vid, runtime.Parts, runtime.MoveSpeed, runtime.AttackSpeed, runtime.State, runtime.Affects, runtime.GuildId, runtime.RankPoints, runtime.PkMode, runtime.MountVnum);

            Span<byte> batch = stackalloc byte[TotalFrameSize];
            int offset = 0;
            PacketFrameWriteStatus detailsStatus = PacketFrameWriter.TryWrite(in details, batch[offset..], out int detailsWritten);
            EnsureWritten(nameof(CharacterDetails), detailsStatus, detailsWritten, CharacterDetailsFrameSize); offset += detailsWritten;
            PacketFrameWriteStatus pointsStatus = PacketFrameWriter.TryWrite(in pointPacket, batch[offset..], out int pointsWritten);
            EnsureWritten(nameof(CharacterPoints), pointsStatus, pointsWritten, CharacterPointsFrameSize); offset += pointsWritten;
            PacketFrameWriteStatus updateStatus = PacketFrameWriter.TryWrite(in update, batch[offset..], out int updateWritten);
            EnsureWritten(nameof(CharacterUpdate), updateStatus, updateWritten, CharacterUpdateFrameSize); offset += updateWritten;

            if (offset != TotalFrameSize)
            {
                throw new InvalidOperationException($"Character bootstrap batch size mismatch: {offset} != {TotalFrameSize}.");
            }

            _output.Write(batch);
            await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (bound) session.ClearRuntimeEntity();
            _runtimeRegistry.Release(reservation.EntityId);
            throw;
        }
    }

    private static void ApplyDurablePoints(uint[] points, in CharacterBootstrapSnapshot snapshot)
    {
        points[1] = snapshot.Level; points[3] = snapshot.Experience; points[11] = snapshot.Gold;
        points[12] = snapshot.Strength; points[13] = snapshot.Vitality; points[14] = snapshot.Dexterity;
        points[15] = snapshot.Intelligence; points[26] = snapshot.AvailableStatusPoints;
    }

    private static void EnsureWritten(string packetName, PacketFrameWriteStatus status, int written, int expected)
    {
        if (status != PacketFrameWriteStatus.Done || written != expected)
            throw new InvalidOperationException($"{packetName} frame could not be written: {status} ({written}/{expected} bytes)." );
    }
}
