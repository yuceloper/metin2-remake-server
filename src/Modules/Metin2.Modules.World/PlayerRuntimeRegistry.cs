using Metin2.Shared.Identity;

namespace Metin2.Modules.World;

public sealed class PlayerRuntimeRegistry(IEntityIdAllocator entityIdAllocator)
{
    private readonly object _gate = new();
    private readonly Dictionary<EntityId, PlayerRuntimeReservation> _byEntityId = new();
    private readonly Dictionary<CharacterId, EntityId> _byCharacterId = new();
    private readonly HashSet<EntityId> _spawned = new();

    public bool TryReserve(CharacterId characterId, Position position, out PlayerRuntimeReservation reservation)
    {
        lock (_gate)
        {
            if (_byCharacterId.ContainsKey(characterId))
            {
                reservation = default;
                return false;
            }

            EntityId entityId;
            do
            {
                entityId = entityIdAllocator.Next();
                if (entityId.Value == 0)
                {
                    throw new InvalidOperationException("Entity id allocator returned reserved id 0.");
                }
            }
            while (_byEntityId.ContainsKey(entityId));

            reservation = new PlayerRuntimeReservation(entityId, characterId, position);
            _byEntityId.Add(entityId, reservation);
            _byCharacterId.Add(characterId, entityId);
            return true;
        }
    }

    public bool TryPromoteToSpawned(EntityId entityId, CharacterId characterId)
    {
        lock (_gate)
        {
            if (!_byEntityId.TryGetValue(entityId, out PlayerRuntimeReservation reservation) ||
                reservation.CharacterId != characterId)
            {
                return false;
            }

            return _spawned.Add(entityId);
        }
    }

    public bool IsSpawned(EntityId entityId)
    {
        lock (_gate)
        {
            return _spawned.Contains(entityId);
        }
    }

    public bool TryGet(EntityId entityId, out PlayerRuntimeReservation reservation)
    {
        lock (_gate)
        {
            return _byEntityId.TryGetValue(entityId, out reservation);
        }
    }

    public bool TryGetByCharacter(CharacterId characterId, out PlayerRuntimeReservation reservation)
    {
        lock (_gate)
        {
            if (!_byCharacterId.TryGetValue(characterId, out EntityId entityId))
            {
                reservation = default;
                return false;
            }

            return _byEntityId.TryGetValue(entityId, out reservation);
        }
    }

    public bool Release(EntityId entityId)
    {
        lock (_gate)
        {
            if (!_byEntityId.Remove(entityId, out PlayerRuntimeReservation reservation))
            {
                return false;
            }

            _spawned.Remove(entityId);
            _byCharacterId.Remove(reservation.CharacterId);
            return true;
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _byEntityId.Count;
            }
        }
    }
}
