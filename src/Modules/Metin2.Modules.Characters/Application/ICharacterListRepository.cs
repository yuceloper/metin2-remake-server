using Metin2.Shared.Identity;

namespace Metin2.Modules.Characters.Application;

public interface ICharacterListRepository
{
    ValueTask<IReadOnlyList<CharacterListEntry>> GetByAccountAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default);
}
