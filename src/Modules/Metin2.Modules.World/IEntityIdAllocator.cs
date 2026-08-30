using Metin2.Shared.Identity;

namespace Metin2.Modules.World;

public interface IEntityIdAllocator
{
    EntityId Next();
}
