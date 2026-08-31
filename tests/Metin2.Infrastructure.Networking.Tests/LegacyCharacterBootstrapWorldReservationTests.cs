using System.Buffers.Binary;
using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Game;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Characters.Application;
using Metin2.Modules.World;
using Metin2.Protocol.Generated;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class LegacyCharacterBootstrapWorldReservationTests
{
    [TestMethod]
    public async Task Bootstrap_uses_world_entity_id_instead_of_provider_vid()
    {
        var snapshot = CreateSnapshot();
        var registry = new PlayerRuntimeRegistry(new MonotonicEntityIdAllocator(0x10203040));
        var pipe = new Pipe();
        var publisher = new LegacyCharacterBootstrapPublisher(
            pipe.Writer,
            new CharacterBootstrapService(new FixedBootstrapRepository(snapshot)),
            new FixedRuntimeContextProvider(CreateRuntimeContext(0xDEADBEEF)),
            registry);
        GameSession session = CreateLoadingSession(snapshot);

        await publisher.PublishAsync(session);
        ReadResult read = await pipe.Reader.ReadAsync();
        byte[] bytes = read.Buffer.ToArray();
        pipe.Reader.AdvanceTo(read.Buffer.End);

        Assert.AreEqual((byte)0x71, bytes[0]);
        Assert.AreEqual(0x10203040u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(1, sizeof(uint))));
        Assert.AreEqual(0x10203040u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(1068, sizeof(uint))));
        Assert.AreEqual(new EntityId(0x10203040), session.RuntimeEntityId);
        Assert.IsTrue(registry.TryGetByCharacter(snapshot.CharacterId, out PlayerRuntimeReservation reservation));
        Assert.AreEqual(new EntityId(0x10203040), reservation.EntityId);
        Assert.AreEqual(snapshot.MapId, reservation.Position.MapId);
        Assert.AreEqual(snapshot.PositionX, reservation.Position.X);
        Assert.AreEqual(snapshot.PositionY, reservation.Position.Y);
    }

    [TestMethod]
    public async Task Bootstrap_failure_rolls_back_world_reservation()
    {
        var snapshot = CreateSnapshot();
        var registry = new PlayerRuntimeRegistry(new MonotonicEntityIdAllocator(123));
        var pipe = new Pipe();
        LegacyCharacterBootstrapRuntimeContext invalid = CreateRuntimeContext(999) with
        {
            Points = new uint[1]
        };
        var publisher = new LegacyCharacterBootstrapPublisher(
            pipe.Writer,
            new CharacterBootstrapService(new FixedBootstrapRepository(snapshot)),
            new FixedRuntimeContextProvider(invalid),
            registry);
        GameSession session = CreateLoadingSession(snapshot);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => publisher.PublishAsync(session).AsTask());

        Assert.IsNull(session.RuntimeEntityId);
        Assert.IsFalse(registry.TryGetByCharacter(snapshot.CharacterId, out _));
    }

    private static GameSession CreateLoadingSession(CharacterBootstrapSnapshot snapshot)
    {
        var session = new GameSession(PacketPhase.Loading);
        session.Authenticate(snapshot.AccountId, "player", new uint[] { 1, 2, 3, 4 });
        session.SelectCharacter(snapshot.CharacterId);
        return session;
    }

    private static CharacterBootstrapSnapshot CreateSnapshot() =>
        new(
            new CharacterId(101), new AccountId(7), "Warrior", 0, 42,
            100, 200, 10, 11, 12, 13, 0, 0,
            1000, 2000, new MapId(1), 0, 3, 2);

    private static LegacyCharacterBootstrapRuntimeContext CreateRuntimeContext(uint ignoredProviderVid) =>
        new(
            ignoredProviderVid,
            new uint[255],
            new ushort[4],
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
}
