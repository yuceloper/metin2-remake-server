using Metin2.Shared.Identity;

namespace Metin2.Modules.Characters.Application;

public sealed class CharacterSelectionService(
    IAccountEmpireRepository empireRepository,
    CharacterListService characterListService)
{
    public async ValueTask<CharacterSelectionSnapshot> GetAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default)
    {
        byte empire = await empireRepository
            .GetEmpireAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        if (empire > 3)
        {
            throw new InvalidOperationException($"Account empire {empire} is outside the legacy 0..3 range.");
        }

        IReadOnlyList<CharacterListEntry> characters = await characterListService
            .GetAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        return new CharacterSelectionSnapshot(empire, characters);
    }
}
