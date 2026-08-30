using Metin2.Modules.Characters.Application;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class CharacterSelectionServiceTests
{
    [TestMethod]
    public async Task Selection_snapshot_orders_slots_and_preserves_empire()
    {
        CharacterListEntry slotTwo = CreateEntry(2, 202, "Second");
        CharacterListEntry slotZero = CreateEntry(0, 101, "First");
        var listService = new CharacterListService(new StubCharacterListRepository([slotTwo, slotZero]));
        var service = new CharacterSelectionService(new StubEmpireRepository(2), listService);

        CharacterSelectionSnapshot snapshot = await service.GetAsync(new AccountId(1));

        Assert.AreEqual((byte)2, snapshot.Empire);
        Assert.AreEqual(2, snapshot.Characters.Count);
        Assert.AreEqual((byte)0, snapshot.Characters[0].Slot);
        Assert.AreEqual((byte)2, snapshot.Characters[1].Slot);
    }

    [TestMethod]
    public async Task Character_list_rejects_duplicate_slots()
    {
        CharacterListEntry first = CreateEntry(1, 101, "First");
        CharacterListEntry duplicate = CreateEntry(1, 202, "Duplicate");
        var service = new CharacterListService(new StubCharacterListRepository([first, duplicate]));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.GetAsync(new AccountId(1)));
    }

    [TestMethod]
    public async Task Selection_snapshot_rejects_invalid_empire()
    {
        var listService = new CharacterListService(new StubCharacterListRepository([]));
        var service = new CharacterSelectionService(new StubEmpireRepository(4), listService);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.GetAsync(new AccountId(1)));
    }

    [TestMethod]
    public async Task Character_select_returns_only_repository_owned_character()
    {
        var service = new CharacterSelectService(new StubCharacterSelectionRepository(new CharacterId(101)));

        CharacterSelectResult result = await service.SelectAsync(new AccountId(7), 0);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(new CharacterId(101), result.CharacterId);
    }

    [TestMethod]
    public async Task Character_select_rejects_missing_and_out_of_range_slots()
    {
        var repository = new CountingCharacterSelectionRepository(null);
        var service = new CharacterSelectService(repository);

        CharacterSelectResult missing = await service.SelectAsync(new AccountId(7), 1);
        CharacterSelectResult invalid = await service.SelectAsync(new AccountId(7), 4);

        Assert.IsFalse(missing.IsSuccess);
        Assert.IsFalse(invalid.IsSuccess);
        Assert.AreEqual(1, repository.CallCount);
    }

    private static CharacterListEntry CreateEntry(byte slot, uint id, string name) =>
        new(
            slot,
            new CharacterId(id),
            name,
            0,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            new GuildId(0),
            string.Empty);

    private sealed class StubCharacterListRepository(IReadOnlyList<CharacterListEntry> entries) : ICharacterListRepository
    {
        public ValueTask<IReadOnlyList<CharacterListEntry>> GetByAccountAsync(
            AccountId accountId,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(entries);
    }

    private sealed class StubEmpireRepository(byte empire) : IAccountEmpireRepository
    {
        public ValueTask<byte> GetEmpireAsync(
            AccountId accountId,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(empire);
    }

    private sealed class StubCharacterSelectionRepository(CharacterId? characterId) : ICharacterSelectionRepository
    {
        public ValueTask<CharacterId?> FindOwnedCharacterIdAsync(
            AccountId accountId,
            byte slot,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(characterId);
    }

    private sealed class CountingCharacterSelectionRepository(CharacterId? characterId) : ICharacterSelectionRepository
    {
        public int CallCount { get; private set; }

        public ValueTask<CharacterId?> FindOwnedCharacterIdAsync(
            AccountId accountId,
            byte slot,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(characterId);
        }
    }
}
