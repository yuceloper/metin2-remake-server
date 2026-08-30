using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Characters.Application;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Game;

public readonly record struct LegacyCharacterBootstrapRuntimeContext(
    uint Vid,
    ReadOnlyMemory<uint> Points,
    ReadOnlyMemory<ushort> Parts,
    byte MoveSpeed,
    byte AttackSpeed,
    byte State,
    ReadOnlyMemory<uint> Affects,
    GuildId GuildId,
    short RankPoints,
    byte PkMode,
    uint MountVnum);

public interface ILegacyCharacterBootstrapRuntimeContextProvider
{
    LegacyCharacterBootstrapRuntimeContext Get(
        GameSession session,
        in CharacterBootstrapSnapshot snapshot);
}
