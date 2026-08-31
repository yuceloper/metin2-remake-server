using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Characters.Application;
using Metin2.Modules.World;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Protocol.Legacy;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Game;

public sealed class GameEnterGameDispatchTarget(
    GameSession session,
    PipeWriter output,
    PlayerRuntimeRegistry runtimeRegistry,
    IServerTimeProvider timeProvider,
    CharacterBootstrapService bootstrapService,
    ILegacyCharacterBootstrapRuntimeContextProvider runtimeContextProvider,
    byte channelNumber = 1)
{
    private const byte PlayerCharacterType = 6;
    private const int PartCount = 4;
    private const int AffectCount = 2;
    private const int PhaseFrameSize = 1 + PhaseCodec.PayloadSize;
    private const int GameTimeFrameSize = 1 + GameTimeCodec.PayloadSize;
    private const int ChannelFrameSize = 1 + ChannelCodec.PayloadSize;
    private const int SpawnCharacterFrameSize = 1 + SpawnCharacterCodec.PayloadSize;
    private const int CharacterInfoFrameSize = 1 + CharacterInfoCodec.PayloadSize;
    private const int TotalFrameSize = PhaseFrameSize + GameTimeFrameSize + ChannelFrameSize + SpawnCharacterFrameSize + CharacterInfoFrameSize;

    public async ValueTask HandleAsync(EnterGame packet, CancellationToken cancellationToken)
    {
        if (session.Phase != PacketPhase.Loading ||
            session.AccountId is not AccountId accountId ||
            session.SelectedCharacterId is not CharacterId characterId ||
            session.RuntimeEntityId is not EntityId entityId ||
            !runtimeRegistry.TryGet(entityId, out PlayerRuntimeReservation reservation) ||
            reservation.CharacterId != characterId ||
            runtimeRegistry.IsSpawned(entityId))
        {
            throw new EnterGameRejectedException();
        }

        CharacterBootstrapSnapshot snapshot = await bootstrapService
            .GetRequiredOwnedAsync(accountId, characterId, cancellationToken)
            .ConfigureAwait(false);
        LegacyCharacterBootstrapRuntimeContext runtime = runtimeContextProvider.Get(session, in snapshot);

        if (runtime.Parts.Length != PartCount)
        {
            throw new InvalidOperationException($"Legacy self spawn requires exactly {PartCount} parts.");
        }

        if (runtime.Affects.Length != AffectCount)
        {
            throw new InvalidOperationException($"Legacy self spawn requires exactly {AffectCount} affect words.");
        }

        uint vid = entityId.Value;
        var phase = new Phase((byte)LegacyPhaseCode.Game);
        var gameTime = new GameTime(checked((uint)timeProvider.GetMilliseconds()));
        var channel = new Channel(channelNumber);
        var spawn = new SpawnCharacter(
            vid,
            0f,
            snapshot.PositionX,
            snapshot.PositionY,
            0,
            PlayerCharacterType,
            snapshot.Class,
            runtime.MoveSpeed,
            runtime.AttackSpeed,
            runtime.State,
            runtime.Affects);
        var info = new CharacterInfo(
            vid,
            snapshot.Name,
            runtime.Parts,
            snapshot.Empire,
            runtime.GuildId,
            snapshot.Level,
            runtime.RankPoints,
            runtime.PkMode,
            runtime.MountVnum);

        Memory<byte> memory = output.GetMemory(TotalFrameSize);
        Span<byte> destination = memory.Span;
        int offset = 0;

        Write(in phase, destination, ref offset, PhaseFrameSize, nameof(Phase));
        Write(in gameTime, destination, ref offset, GameTimeFrameSize, nameof(GameTime));
        Write(in channel, destination, ref offset, ChannelFrameSize, nameof(Channel));
        Write(in spawn, destination, ref offset, SpawnCharacterFrameSize, nameof(SpawnCharacter));
        Write(in info, destination, ref offset, CharacterInfoFrameSize, nameof(CharacterInfo));

        if (offset != TotalFrameSize)
        {
            throw new InvalidOperationException($"EnterGame batch size mismatch: {offset} != {TotalFrameSize}.");
        }

        output.Advance(offset);
        FlushResult flush = await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        if (flush.IsCanceled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (!runtimeRegistry.TryPromoteToSpawned(entityId, characterId))
        {
            throw new InvalidOperationException("Runtime reservation could not be promoted after EnterGame publication.");
        }

        session.TransitionTo(PacketPhase.Game);
    }

    private static void Write(in Phase packet, Span<byte> destination, ref int offset, int expected, string name)
    {
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, destination[offset..], out int written);
        EnsureWritten(name, status, written, expected);
        offset += written;
    }

    private static void Write(in GameTime packet, Span<byte> destination, ref int offset, int expected, string name)
    {
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, destination[offset..], out int written);
        EnsureWritten(name, status, written, expected);
        offset += written;
    }

    private static void Write(in Channel packet, Span<byte> destination, ref int offset, int expected, string name)
    {
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, destination[offset..], out int written);
        EnsureWritten(name, status, written, expected);
        offset += written;
    }

    private static void Write(in SpawnCharacter packet, Span<byte> destination, ref int offset, int expected, string name)
    {
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, destination[offset..], out int written);
        EnsureWritten(name, status, written, expected);
        offset += written;
    }

    private static void Write(in CharacterInfo packet, Span<byte> destination, ref int offset, int expected, string name)
    {
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, destination[offset..], out int written);
        EnsureWritten(name, status, written, expected);
        offset += written;
    }

    private static void EnsureWritten(string packetName, PacketFrameWriteStatus status, int written, int expected)
    {
        if (status != PacketFrameWriteStatus.Done || written != expected)
        {
            throw new InvalidOperationException($"{packetName} frame could not be written: {status} ({written}/{expected} bytes)." );
        }
    }
}

public sealed class EnterGameRejectedException : Exception
{
    public EnterGameRejectedException()
        : base("EnterGame was rejected because the session has no valid Loading runtime reservation.")
    {
    }
}
