using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Send;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Characters.Application;
using Metin2.Modules.World;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Protocol.Legacy;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Game;

public sealed class GameEnterGameDispatchTarget
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

    private readonly GameSession _session;
    private readonly LegacyPacketOutput _output;
    private readonly PlayerRuntimeRegistry _runtimeRegistry;
    private readonly IServerTimeProvider _timeProvider;
    private readonly CharacterBootstrapService _bootstrapService;
    private readonly ILegacyCharacterBootstrapRuntimeContextProvider _runtimeContextProvider;
    private readonly byte _channelNumber;

    public GameEnterGameDispatchTarget(
        GameSession session,
        PipeWriter output,
        PlayerRuntimeRegistry runtimeRegistry,
        IServerTimeProvider timeProvider,
        CharacterBootstrapService bootstrapService,
        ILegacyCharacterBootstrapRuntimeContextProvider runtimeContextProvider,
        byte channelNumber = 1)
        : this(
            session,
            new LegacyPacketOutput(output, session),
            runtimeRegistry,
            timeProvider,
            bootstrapService,
            runtimeContextProvider,
            channelNumber)
    {
    }

    public GameEnterGameDispatchTarget(
        GameSession session,
        LegacyPacketOutput output,
        PlayerRuntimeRegistry runtimeRegistry,
        IServerTimeProvider timeProvider,
        CharacterBootstrapService bootstrapService,
        ILegacyCharacterBootstrapRuntimeContextProvider runtimeContextProvider,
        byte channelNumber = 1)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(runtimeRegistry);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(bootstrapService);
        ArgumentNullException.ThrowIfNull(runtimeContextProvider);
        _session = session;
        _output = output;
        _runtimeRegistry = runtimeRegistry;
        _timeProvider = timeProvider;
        _bootstrapService = bootstrapService;
        _runtimeContextProvider = runtimeContextProvider;
        _channelNumber = channelNumber;
    }

    public async ValueTask HandleAsync(EnterGame packet, CancellationToken cancellationToken)
    {
        if (_session.Phase != PacketPhase.Loading ||
            _session.AccountId is not AccountId accountId ||
            _session.SelectedCharacterId is not CharacterId characterId ||
            _session.RuntimeEntityId is not EntityId entityId ||
            !_runtimeRegistry.TryGet(entityId, out PlayerRuntimeReservation reservation) ||
            reservation.CharacterId != characterId ||
            _runtimeRegistry.IsSpawned(entityId))
        {
            throw new EnterGameRejectedException();
        }

        CharacterBootstrapSnapshot snapshot = await _bootstrapService
            .GetRequiredOwnedAsync(accountId, characterId, cancellationToken)
            .ConfigureAwait(false);
        LegacyCharacterBootstrapRuntimeContext runtime = _runtimeContextProvider.Get(_session, in snapshot);

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
        var gameTime = new GameTime(checked((uint)_timeProvider.GetMilliseconds()));
        var channel = new Channel(_channelNumber);
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

        Span<byte> batch = stackalloc byte[TotalFrameSize];
        int offset = 0;

        Write(in phase, batch, ref offset, PhaseFrameSize, nameof(Phase));
        Write(in gameTime, batch, ref offset, GameTimeFrameSize, nameof(GameTime));
        Write(in channel, batch, ref offset, ChannelFrameSize, nameof(Channel));
        Write(in spawn, batch, ref offset, SpawnCharacterFrameSize, nameof(SpawnCharacter));
        Write(in info, batch, ref offset, CharacterInfoFrameSize, nameof(CharacterInfo));

        if (offset != TotalFrameSize)
        {
            throw new InvalidOperationException($"EnterGame batch size mismatch: {offset} != {TotalFrameSize}.");
        }

        _output.Write(batch);
        await _output.FlushAsync(cancellationToken).ConfigureAwait(false);

        if (!_runtimeRegistry.TryPromoteToSpawned(entityId, characterId))
        {
            throw new InvalidOperationException("Runtime reservation could not be promoted after EnterGame publication.");
        }

        _session.TransitionTo(PacketPhase.Game);
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
