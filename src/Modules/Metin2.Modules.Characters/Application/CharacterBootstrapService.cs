using Metin2.Shared.Identity;

namespace Metin2.Modules.Characters.Application;

public sealed class CharacterBootstrapService(ICharacterBootstrapRepository repository)
{
    public async ValueTask<CharacterBootstrapSnapshot> GetRequiredOwnedAsync(
        AccountId accountId,
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        CharacterBootstrapSnapshot? snapshot = await repository
            .GetOwnedAsync(accountId, characterId, cancellationToken)
            .ConfigureAwait(false);

        return snapshot ?? throw new InvalidOperationException(
            $"Character '{characterId.Value}' is not owned by account '{accountId.Value}'.");
    }
}
