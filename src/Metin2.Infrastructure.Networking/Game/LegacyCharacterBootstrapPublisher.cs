using System.IO.Pipelines;
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

public sealed class LegacyCharacterBootstrapPublisher(
    PipeWriter output,
    CharacterBootstrapService bootstrapService,
    ILegacyCharacterBootstrapRuntimeContextProvider runtimeContextProvider,
    PlayerRuntimeRegistry runtimeRegistry) : ILegacyCharacterBootstrapPublisher
{
    private const int PointCount = 255;
    private const int PartCount = 4;
    private const int AffectCount = 2;
    private const int CharacterDetailsFrameSize = 1 + CharacterDetailsCodec.PayloadSize;
    private const int CharacterPointsFrameSize = 1 + CharacterPointsCodec.PayloadSize;
    private const int CharacterUpdateFrameSize = 1 + CharacterUpdateCodec.PayloadSize;
    private const int TotalFrameSize = CharacterDetailsFrameSize + CharacterPointsFrameSize + CharacterUpdateFrameSize;

    public async ValueTask PublishAsync(GameSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Phase != PacketPhase.Loading ||
            session.AccountId is not AccountId accountId ||
            session.SelectedCharacterId is not CharacterId characterId)
        {
            throw new InvalidOperationException("Character bootstrap requires an authenticated selected character in Loading phase.");
        }

        CharacterBootstrapSnapshot snapshot = await bootstrapService
            .GetRequiredOwnedAsync(accountId, characterId, cancellationToken)
            .ConfigureAwait(false);

        var position = new Position(snapshot.MapId, snapshot.PositionX, snapshot.PositionY);
        if (!runtimeRegistry.TryReserve(snapshot.CharacterId, position, out PlayerRuntimeReservation reservation))
        {
            throw new InvalidOperationException($"Character {snapshot.CharacterId} already has an active runtime reservation.");
        }

        bool bound = false;
        try
        {
            session.BindRuntimeEntity(reservation.EntityId);
            bound = true;

            LegacyCharacterBootstrapRuntimeContext runtime = runtimeContextProvider.Get(session, in snapshot);
            if (runtime.Points.Length != PointCount) throw new InvalidOperationException($"Legacy point projection must contain exactly {PointCount} values.");
            if (runtime.Parts.Length != PartCount) throw new InvalidOperationException($"Legacy character update requires exactly {PartCount} parts.");
            if (runtime.Affects.Length != AffectCount) throw new InvalidOperationException($"Legacy character update requires exactly {AffectCount} affect words.");

            uint[] points = runtime.Points.ToArray();
            ApplyDurablePoints(points, in snapshot);
            uint vid = reservation.EntityId.Value;

            var details = new CharacterDetails(vid, snapshot.Class, snapshot.Name, snapshot.PositionX, snapshot.PositionY, 0, snapshot.Empire, snapshot.SkillGroup);
            var pointPacket = new CharacterPoints(points);
            var update = new CharacterUpdate(vid, runtime.Parts, runtime.MoveSpeed, runtime.AttackSpeed, runtime.State, runtime.Affects, runtime.GuildId, runtime.RankPoints, runtime.PkMode, runtime.MountVnum);

            Memory<byte> memory = output.GetMemory(TotalFrameSize);
            Span<byte> destination = memory.Span;
            int offset = 0;
            PacketFrameWriteStatus detailsStatus = PacketFrameWriter.TryWrite(in details, destination[offset..], out int detailsWritten);
            EnsureWritten(nameof(CharacterDetails), detailsStatus, detailsWritten, CharacterDetailsFrameSize); offset += detailsWritten;
            PacketFrameWriteStatus pointsStatus = PacketFrameWriter.TryWrite(in pointPacket, destination[offset..], out int pointsWritten);
            EnsureWritten(nameof(CharacterPoints), pointsStatus, pointsWritten, CharacterPointsFrameSize); offset += pointsWritten;
            PacketFrameWriteStatus updateStatus = PacketFrameWriter.TryWrite(in update, destination[offset..], out int updateWritten);
            EnsureWritten(nameof(CharacterUpdate), updateStatus, updateWritten, CharacterUpdateFrameSize); offset += updateWritten;

            output.Advance(offset);
            FlushResult flush = await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (flush.IsCanceled) throw new OperationCanceledException(cancellationToken);
        }
        catch
        {
            if (bound) session.ClearRuntimeEntity();
            runtimeRegistry.Release(reservation.EntityId);
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
