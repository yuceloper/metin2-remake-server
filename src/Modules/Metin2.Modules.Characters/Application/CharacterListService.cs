using Metin2.Shared.Identity;

namespace Metin2.Modules.Characters.Application;

public sealed class CharacterListService(ICharacterListRepository repository)
{
    public async ValueTask<IReadOnlyList<CharacterListEntry>> GetAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CharacterListEntry> characters = await repository
            .GetByAccountAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        if (characters.Count > 4)
        {
            throw new InvalidOperationException("A legacy character list cannot contain more than four slots.");
        }

        var seenSlots = new bool[4];
        CharacterListEntry[] ordered = characters.OrderBy(static character => character.Slot).ToArray();
        foreach (CharacterListEntry character in ordered)
        {
            if (character.Slot > 3)
            {
                throw new InvalidOperationException($"Character slot {character.Slot} is outside the legacy 0..3 range.");
            }

            if (seenSlots[character.Slot])
            {
                throw new InvalidOperationException($"Character slot {character.Slot} is duplicated.");
            }

            seenSlots[character.Slot] = true;
        }

        return ordered;
    }
}
