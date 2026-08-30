namespace Metin2.Modules.Characters.Application;

public readonly record struct CharacterSelectionSnapshot(
    byte Empire,
    IReadOnlyList<CharacterListEntry> Characters);
