using Metin2.Shared.Identity;

namespace Metin2.Modules.World;

public readonly record struct PlayerRuntimeReservation(
    EntityId EntityId,
    CharacterId CharacterId,
    Position Position);
