using Metin2.Shared.Identity;

namespace Metin2.Modules.World;

public sealed class MonotonicEntityIdAllocator : IEntityIdAllocator
{
    private readonly object _gate = new();
    private uint _next;

    public MonotonicEntityIdAllocator(uint first = 1)
    {
        _next = first == 0 ? 1 : first;
    }

    public EntityId Next()
    {
        lock (_gate)
        {
            uint value = _next;
            _next = value == uint.MaxValue ? 1 : value + 1;
            return new EntityId(value);
        }
    }
}
