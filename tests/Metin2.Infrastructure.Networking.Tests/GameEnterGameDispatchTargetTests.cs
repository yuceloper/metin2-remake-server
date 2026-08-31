using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Game;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Characters.Application;
using Metin2.Modules.World;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class GameEnterGameDispatchTargetTests
{
    [TestMethod]
    public async Task EnterGame_promotes_reservation_and_publishes_full_self_spawn_batch()
    {
        var registry = new PlayerRuntimeRegistry(new MonotonicEntityIdAllocator(777));
        CharacterBootstrapSnapshot snapshot = CreateSnapshot();
        Assert.IsTrue(registry.TryReserve(
            snapshot.CharacterId,
            new Position(snapshot.MapId, snapshot.PositionX, snapshot.PositionY),
            out PlayerRuntimeReservation reservation));

        var session = CreateLoadingSession(snapshot, reservation.EntityId);
        var pipe = new Pipe();
        var target = new GameEnterGameDispatchTarget(
            session,
            pipe.Writer,
            registry,
            new ConstantTimeProvider(0x11223344),
            new CharacterBootstrapService(new FixedBootstrapRepository(snapshot)),
            new FixedRuntimeContextProvider(CreateRuntimeContext()),
            3);

        await target.HandleAsync(new EnterGame(), CancellationToken.None);

        ReadResult read = await pipe.Reader.ReadAsync();
        byte[] bytes = read.Buffer.ToArray();
        pipe.Reader.AdvanceTo(read.Buffer.End);

        Assert.AreEqual(98, bytes.Length);
        Assert.AreEqual((byte)0xFD, bytes[0]);
        Assert.AreEqual((byte)0x05, bytes[1]);
        Assert.AreEqual((byte)0x6A, bytes[2]);
        Assert.AreEqual(0x11223344u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(3, 4)));
        Assert.AreEqual((byte)0x79, bytes[7]);
        Assert.AreEqual((byte)3, bytes[8]);
        Assert.AreEqual((byte)0x01, bytes[9]);
        Assert.AreEqual(777u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(10, 4)));
        Assert.AreEqual((byte)6, bytes[30]);
        Assert.AreEqual((byte)0x88, bytes[44]);
        Assert.AreEqual(777u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(45, 4)));
        Assert.AreEqual(PacketPhase.Game, session.Phase);
        Assert.IsTrue(registry.IsSpawned(reservation.EntityId));
    }

    [TestMethod]
    public async Task EnterGame_without_runtime_reservation_is_rejected()
    {
        CharacterBootstrapSnapshot snapshot = CreateSnapshot();
        var registry = new PlayerRuntimeRegistry(new MonotonicEntityIdAllocator());
        var session = new GameSession(PacketPhase.Loading);
        session.Authenticate(snapshot.AccountId, "player", new uint[] { 1, 2, 3, 4 });
        session.SelectCharacter(snapshot.CharacterId);
        var pipe = new Pipe();
        var target = new GameEnterGameDispatchTarget(
            session,
            pipe.Writer,
            registry,
            new ConstantTimeProvider(1),
            new CharacterBootstrapService(new FixedBootstrapRepository(snapshot)),
            new FixedRuntimeContextProvider(CreateRuntimeContext()));

        await Assert.ThrowsExactlyAsync<EnterGameRejectedException>(
            () => target.HandleAsync(new EnterGame(), CancellationToken.None).AsTask());
        Assert.AreEqual(PacketPhase.Loading, session.Phase);
    }

    private static GameSession CreateLoadingSession(CharacterBootstrapSnapshot snapshot, EntityId entityId)
    {
        var session = new GameSession(PacketPhase.Loading);
        session.Authenticate(snapshot.AccountId, "player", new uint[] { 1, 2, 3, 4 });
        session.SelectCharacter(snapshot.CharacterId);
        session.BindRuntimeEntity(entityId);
        return session;
    }

    private static CharacterBootstrapSnapshot CreateSnapshot() =>
        new(
            new CharacterId(101),
            new AccountId(7),
            "Warrior",
            0,
            42,
            100,
            200,
            10,
            11,
            12,
            13,
            10,
            30,
            1000,
            2000,
            new MapId(1),
            0,
            3,
            2);

    private static LegacyCharacterBootstrapRuntimeContext CreateRuntimeContext() =>
        new(
            0xDEADBEEF,
            new uint[255],
            new ushort[] { 10, 20, 0, 30 },
            150,
            140,
            0,
            new uint[2],
            new GuildId(0),
            0,
            0,
            0);

    private sealed class FixedBootstrapRepository(CharacterBootstrapSnapshot snapshot) : ICharacterBootstrapRepository
    {
        public ValueTask<CharacterBootstrapSnapshot?> GetOwnedAsync(
            AccountId accountId,
            CharacterId characterId,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<CharacterBootstrapSnapshot?>(
                snapshot.AccountId == accountId && snapshot.CharacterId == characterId ? snapshot : null);
    }

    private sealed class FixedRuntimeContextProvider(LegacyCharacterBootstrapRuntimeContext context)
        : ILegacyCharacterBootstrapRuntimeContextProvider
    {
        public LegacyCharacterBootstrapRuntimeContext Get(
            GameSession session,
            in CharacterBootstrapSnapshot snapshot) => context;
    }

    private sealed class ConstantTimeProvider(long value) : IServerTimeProvider
    {
        public long GetMilliseconds() => value;
    }
}
