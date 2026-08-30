using Metin2.Shared.Identity;

namespace Metin2.Modules.Characters.Application;

public interface ICharacterSelectionRepository
{
    ValueTask<CharacterId?> FindOwnedCharacterIdAsync(
        AccountId accountId,
        byte slot,
        CancellationToken cancellationToken = default);
}
