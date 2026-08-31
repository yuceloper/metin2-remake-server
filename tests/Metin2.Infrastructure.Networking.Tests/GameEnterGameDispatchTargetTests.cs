using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Game;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.World;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class GameEnterGameDispatchTargetTests
{
    [TestMethod]
    public async Task EnterGame_promotes_reservation_and_publishes_game_phase_time_and_channel()
    {
        var registry = new PlayerRuntimeRegistry(new MonotonicEntityIdAllocator(777));
        var characterId = new CharacterId(101);
        Assert.IsTrue(registry.TryReserve(
            characterId,
            new Position(new MapId(1), 1000, 2000),
            out PlayerRuntimeReservation reservation));

        var session = new GameSession(PacketPhase.Loading);
        session.Authenticate(new AccountId(7), "player", new uint[] { 1, 2, 3, 4 });
        session.SelectCharacter(characterId);
        session.BindRuntimeEntity(reservation.EntityId);
        var pipe = new Pipe();
        var target = new GameEnterGameDispatchTarget(
            session,
            pipe.Writer,
            registry,
            new ConstantTimeProvider(0x11223344),
            3);

        await target.HandleAsync(new EnterGame(), CancellationToken.None);

        ReadResult read = await pipe.Reader.ReadAsync();
        byte[] bytes = read.Buffer.ToArray();
        pipe.Reader.AdvanceTo(read.Buffer.End);

        CollectionAssert.AreEqual(
            new byte[] { 0xFD, 0x05, 0x6A, 0x44, 0x33, 0x22, 0x11, 0x79, 0x03 },
            bytes);
        Assert.AreEqual(PacketPhase.Game, session.Phase);
        Assert.IsTrue(registry.IsSpawned(reservation.EntityId));
    }

    [TestMethod]
    public async Task EnterGame_without_runtime_reservation_is_rejected()
    {
        var registry = new PlayerRuntimeRegistry(new MonotonicEntityIdAllocator());
        var session = new GameSession(PacketPhase.Loading);
        session.Authenticate(new AccountId(7), "player", new uint[] { 1, 2, 3, 4 });
        session.SelectCharacter(new CharacterId(101));
        var pipe = new Pipe();
        var target = new GameEnterGameDispatchTarget(
            session,
            pipe.Writer,
            registry,
            new ConstantTimeProvider(1));

        await Assert.ThrowsExactlyAsync<EnterGameRejectedException>(
            () => target.HandleAsync(new EnterGame(), CancellationToken.None).AsTask());
        Assert.AreEqual(PacketPhase.Loading, session.Phase);
    }

    private sealed class ConstantTimeProvider(long value) : IServerTimeProvider
    {
        public long GetMilliseconds() => value;
    }
}
