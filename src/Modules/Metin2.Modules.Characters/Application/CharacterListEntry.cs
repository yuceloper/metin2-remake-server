using Metin2.Shared.Identity;

namespace Metin2.Modules.Characters.Application;

public readonly record struct CharacterListEntry(
    byte Slot,
    CharacterId CharacterId,
    string Name,
    byte Class,
    byte Level,
    uint PlaytimeMinutes,
    byte Strength,
    byte Vitality,
    byte Dexterity,
    byte Intelligence,
    ushort BodyPart,
    byte NameChange,
    ushort HairPart,
    int PositionX,
    int PositionY,
    byte SkillGroup,
    GuildId GuildId,
    string GuildName);
