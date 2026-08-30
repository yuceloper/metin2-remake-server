using Metin2.Shared.Identity;

namespace Metin2.Modules.Characters.Application;

public readonly record struct CharacterBootstrapSnapshot(
    CharacterId CharacterId,
    AccountId AccountId,
    string Name,
    byte Class,
    byte Level,
    uint Experience,
    uint Gold,
    byte Strength,
    byte Vitality,
    byte Dexterity,
    byte Intelligence,
    ushort BodyPart,
    ushort HairPart,
    int PositionX,
    int PositionY,
    MapId MapId,
    byte SkillGroup,
    uint AvailableStatusPoints);
