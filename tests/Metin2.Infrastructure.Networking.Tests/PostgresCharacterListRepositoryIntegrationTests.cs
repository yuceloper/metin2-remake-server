using Metin2.Infrastructure.Persistence.Postgres.Characters;
using Metin2.Infrastructure.Persistence.Postgres.Migrations;
using Metin2.Modules.Characters.Application;
using Metin2.Shared.Identity;
using Npgsql;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class PostgresCharacterListRepositoryIntegrationTests
{
    private const string ConnectionStringEnvironmentVariable = "METIN2_TEST_POSTGRES_CONNECTION_STRING";

    [TestMethod]
    public async Task Selection_and_bootstrap_reads_are_account_scoped_against_live_postgres()
    {
        string? connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Inconclusive($"Set {ConnectionStringEnvironmentVariable} to run PostgreSQL integration tests.");
            return;
        }

        await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
        var migrator = new PostgresMigrator(dataSource);
        await migrator.MigrateAsync();

        await using (NpgsqlCommand historyCommand = dataSource.CreateCommand(
            "SELECT COUNT(*) FROM schema_migrations WHERE version IN ($1, $2);"))
        {
            historyCommand.Parameters.AddWithValue("V003__create_characters");
            historyCommand.Parameters.AddWithValue("V004__add_character_bootstrap_state");
            Assert.AreEqual(2L, Convert.ToInt64(await historyCommand.ExecuteScalarAsync()));
        }

        long firstAccountId = await CreateAccountAsync(dataSource, "CharacterListA", empire: 2);
        long secondAccountId = await CreateAccountAsync(dataSource, "CharacterListB", empire: 3);

        await InsertCharacterAsync(dataSource, firstAccountId, 2, "SecondSlot", 202, 2, 35, 600, 3000, 4000, 1234, 5000, 7);
        await InsertCharacterAsync(dataSource, firstAccountId, 0, "FirstSlot", 101, 0, 42, 1200, 1000, 2000, 987654, 123456, 11);
        await InsertCharacterAsync(dataSource, secondAccountId, 1, "OtherAccount", 303, 1, 20, 90, 5000, 6000, 88, 99, 3);

        var listRepository = new PostgresCharacterListRepository(dataSource);
        var empireRepository = new PostgresAccountEmpireRepository(dataSource);
        var ownedSelectionRepository = new PostgresCharacterSelectionRepository(dataSource);
        var bootstrapRepository = new PostgresCharacterBootstrapRepository(dataSource);
        var listService = new CharacterListService(listRepository);
        var selectionService = new CharacterSelectionService(empireRepository, listService);
        var selectService = new CharacterSelectService(ownedSelectionRepository);
        var firstAccount = new AccountId(checked((uint)firstAccountId));

        CharacterSelectionSnapshot snapshot = await selectionService.GetAsync(firstAccount);
        IReadOnlyList<CharacterListEntry> characters = snapshot.Characters;

        Assert.AreEqual((byte)2, snapshot.Empire);
        Assert.AreEqual(2, characters.Count);
        Assert.AreEqual((byte)0, characters[0].Slot);
        Assert.AreEqual(new CharacterId(101), characters[0].CharacterId);
        Assert.AreEqual("FirstSlot", characters[0].Name);
        Assert.AreEqual((byte)42, characters[0].Level);
        Assert.AreEqual(1200u, characters[0].PlaytimeMinutes);
        Assert.AreEqual(1000, characters[0].PositionX);
        Assert.AreEqual(2000, characters[0].PositionY);
        Assert.AreEqual((byte)2, characters[1].Slot);
        Assert.AreEqual(new CharacterId(202), characters[1].CharacterId);

        CharacterSelectResult ownedSlot = await selectService.SelectAsync(firstAccount, 0);
        CharacterSelectResult otherAccountsSlot = await selectService.SelectAsync(firstAccount, 1);
        Assert.IsTrue(ownedSlot.IsSuccess);
        Assert.AreEqual(new CharacterId(101), ownedSlot.CharacterId);
        Assert.IsFalse(otherAccountsSlot.IsSuccess);

        CharacterBootstrapSnapshot? bootstrap = await bootstrapRepository.GetOwnedAsync(
            firstAccount,
            new CharacterId(101));
        CharacterBootstrapSnapshot? crossAccountBootstrap = await bootstrapRepository.GetOwnedAsync(
            firstAccount,
            new CharacterId(303));

        Assert.IsTrue(bootstrap.HasValue);
        Assert.AreEqual(new CharacterId(101), bootstrap.Value.CharacterId);
        Assert.AreEqual(firstAccount, bootstrap.Value.AccountId);
        Assert.AreEqual("FirstSlot", bootstrap.Value.Name);
        Assert.AreEqual((byte)42, bootstrap.Value.Level);
        Assert.AreEqual(987654u, bootstrap.Value.Experience);
        Assert.AreEqual(123456u, bootstrap.Value.Gold);
        Assert.AreEqual(11u, bootstrap.Value.AvailableStatusPoints);
        Assert.AreEqual(new MapId(1), bootstrap.Value.MapId);
        Assert.IsFalse(crossAccountBootstrap.HasValue);
    }

    private static async Task<long> CreateAccountAsync(NpgsqlDataSource dataSource, string username, short empire)
    {
        string normalized = username.ToUpperInvariant();
        await using (NpgsqlCommand cleanup = dataSource.CreateCommand(
            "DELETE FROM accounts WHERE username_normalized = $1;"))
        {
            cleanup.Parameters.AddWithValue(normalized);
            await cleanup.ExecuteNonQueryAsync();
        }

        await using NpgsqlCommand insert = dataSource.CreateCommand(
            """
            INSERT INTO accounts (username, username_normalized, password_hash, login_enabled, empire)
            VALUES ($1, $2, 'integration-test-hash', TRUE, $3)
            RETURNING id;
            """);
        insert.Parameters.AddWithValue(username);
        insert.Parameters.AddWithValue(normalized);
        insert.Parameters.AddWithValue(empire);
        return Convert.ToInt64(await insert.ExecuteScalarAsync());
    }

    private static async Task InsertCharacterAsync(
        NpgsqlDataSource dataSource,
        long accountId,
        short slot,
        string name,
        long id,
        short @class,
        short level,
        long playtimeMinutes,
        int positionX,
        int positionY,
        long experience,
        long gold,
        long availableStatusPoints)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            """
            INSERT INTO characters (
                id, account_id, slot, name, class, level, playtime_minutes,
                experience, gold, available_status_points,
                strength, vitality, dexterity, intelligence,
                body_part, name_change, hair_part,
                position_x, position_y, map_id, skill_group)
            VALUES (
                $1, $2, $3, $4, $5, $6, $7,
                $8, $9, $10,
                10, 11, 12, 13,
                0, 0, 0,
                $11, $12, 1, 0);
            """);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(accountId);
        command.Parameters.AddWithValue(slot);
        command.Parameters.AddWithValue(name);
        command.Parameters.AddWithValue(@class);
        command.Parameters.AddWithValue(level);
        command.Parameters.AddWithValue(playtimeMinutes);
        command.Parameters.AddWithValue(experience);
        command.Parameters.AddWithValue(gold);
        command.Parameters.AddWithValue(availableStatusPoints);
        command.Parameters.AddWithValue(positionX);
        command.Parameters.AddWithValue(positionY);
        Assert.AreEqual(1, await command.ExecuteNonQueryAsync());
    }
}
