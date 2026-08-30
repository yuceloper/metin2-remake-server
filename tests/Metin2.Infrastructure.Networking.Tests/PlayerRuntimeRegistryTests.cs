using Metin2.Modules.World;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class PlayerRuntimeRegistryTests
{
    [TestMethod]
    public void Reserve_indexes_player_by_entity_and_character()
    {
        var registry = new PlayerRuntimeRegistry(new MonotonicEntityIdAllocator());
        var position = new Position(new MapId(1), 100, 200);

        bool reserved = registry.TryReserve(new CharacterId(10), position, out PlayerRuntimeReservation reservation);

        Assert.IsTrue(reserved);
        Assert.AreEqual(new EntityId(1), reservation.EntityId);
        Assert.AreEqual(new CharacterId(10), reservation.CharacterId);
        Assert.AreEqual(position, reservation.Position);
        Assert.AreEqual(1, registry.Count);
        Assert.IsTrue(registry.TryGet(reservation.EntityId, out PlayerRuntimeReservation byEntity));
        Assert.AreEqual(reservation, byEntity);
        Assert.IsTrue(registry.TryGetByCharacter(reservation.CharacterId, out PlayerRuntimeReservation byCharacter));
        Assert.AreEqual(reservation, byCharacter);
    }

    [TestMethod]
    public void Duplicate_character_is_rejected_until_reservation_is_released()
    {
        var registry = new PlayerRuntimeRegistry(new MonotonicEntityIdAllocator());
        var characterId = new CharacterId(10);

        Assert.IsTrue(registry.TryReserve(characterId, new Position(new MapId(1), 1, 2), out PlayerRuntimeReservation first));
        Assert.IsFalse(registry.TryReserve(characterId, new Position(new MapId(2), 3, 4), out _));
        Assert.IsTrue(registry.Release(first.EntityId));
        Assert.IsTrue(registry.TryReserve(characterId, new Position(new MapId(2), 3, 4), out PlayerRuntimeReservation second));
        Assert.AreNotEqual(first.EntityId, second.EntityId);
        Assert.AreEqual(new MapId(2), second.Position.MapId);
    }

    [TestMethod]
    public void Allocator_wraps_from_uint_max_to_one_without_emitting_zero()
    {
        var allocator = new MonotonicEntityIdAllocator(uint.MaxValue);

        Assert.AreEqual(new EntityId(uint.MaxValue), allocator.Next());
        Assert.AreEqual(new EntityId(1), allocator.Next());
        Assert.AreEqual(new EntityId(2), allocator.Next());
    }

    [TestMethod]
    public void Registry_skips_entity_id_that_is_still_reserved_after_allocator_wrap_or_collision()
    {
        var allocator = new SequenceAllocator(5, 5, 6);
        var registry = new PlayerRuntimeRegistry(allocator);

        Assert.IsTrue(registry.TryReserve(new CharacterId(1), new Position(new MapId(1), 0, 0), out PlayerRuntimeReservation first));
        Assert.IsTrue(registry.TryReserve(new CharacterId(2), new Position(new MapId(1), 0, 0), out PlayerRuntimeReservation second));

        Assert.AreEqual(new EntityId(5), first.EntityId);
        Assert.AreEqual(new EntityId(6), second.EntityId);
    }

    [TestMethod]
    public void Registry_rejects_allocator_zero_id()
    {
        var registry = new PlayerRuntimeRegistry(new SequenceAllocator(0));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            registry.TryReserve(new CharacterId(1), new Position(new MapId(1), 0, 0), out _));
    }

    private sealed class SequenceAllocator(params uint[] values) : IEntityIdAllocator
    {
        private int _index;

        public EntityId Next()
        {
            if (_index >= values.Length)
            {
                throw new InvalidOperationException("Test allocator exhausted.");
            }

            return new EntityId(values[_index++]);
        }
    }
}
