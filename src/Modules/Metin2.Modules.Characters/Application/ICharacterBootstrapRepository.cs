using Metin2.Shared.Identity;

namespace Metin2.Modules.Characters.Application;

public interface ICharacterBootstrapRepository
{
    ValueTask<CharacterBootstrapSnapshot?> GetOwnedAsync(
        AccountId accountId,
        CharacterId characterId,
        CancellationToken cancellationToken = default);
}
