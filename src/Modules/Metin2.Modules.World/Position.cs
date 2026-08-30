using Metin2.Shared.Identity;

namespace Metin2.Modules.World;

public readonly record struct Position(MapId MapId, int X, int Y);
