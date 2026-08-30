using Metin2.Shared.Identity;

namespace Metin2.Modules.Characters.Application;

public readonly record struct CharacterSelectResult(bool IsSuccess, CharacterId CharacterId)
{
    public static CharacterSelectResult Success(CharacterId characterId) => new(true, characterId);
    public static CharacterSelectResult NotFound() => default;
}

public sealed class CharacterSelectService(ICharacterSelectionRepository repository)
{
    public async ValueTask<CharacterSelectResult> SelectAsync(
        AccountId accountId,
        byte slot,
        CancellationToken cancellationToken = default)
    {
        if (slot > 3)
        {
            return CharacterSelectResult.NotFound();
        }

        CharacterId? characterId = await repository
            .FindOwnedCharacterIdAsync(accountId, slot, cancellationToken)
            .ConfigureAwait(false);

        return characterId.HasValue
            ? CharacterSelectResult.Success(characterId.Value)
            : CharacterSelectResult.NotFound();
    }
}
