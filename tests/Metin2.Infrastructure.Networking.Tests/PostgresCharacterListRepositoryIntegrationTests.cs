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
    public async Task Character_list_is_account_scoped_and_slot_ordered_against_live_postgres()
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
            "SELECT COUNT(*) FROM schema_migrations WHERE version = $1;"))
        {
            historyCommand.Parameters.AddWithValue("V003__create_characters");
            Assert.AreEqual(1L, Convert.ToInt64(await historyCommand.ExecuteScalarAsync()));
        }

        long firstAccountId = await CreateAccountAsync(dataSource, "CharacterListA");
        long secondAccountId = await CreateAccountAsync(dataSource, "CharacterListB");

        await InsertCharacterAsync(dataSource, firstAccountId, 2, "SecondSlot", 202, 2, 35, 600, 3000, 4000);
        await InsertCharacterAsync(dataSource, firstAccountId, 0, "FirstSlot", 101, 0, 42, 1200, 1000, 2000);
        await InsertCharacterAsync(dataSource, secondAccountId, 1, "OtherAccount", 303, 1, 20, 90, 5000, 6000);

        var repository = new PostgresCharacterListRepository(dataSource);
        var service = new CharacterListService(repository);
        IReadOnlyList<CharacterListEntry> characters = await service.GetAsync(new AccountId(checked((uint)firstAccountId)));

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
        Assert.AreEqual("SecondSlot", characters[1].Name);
        Assert.AreEqual(new GuildId(0), characters[1].GuildId);
        Assert.AreEqual(string.Empty, characters[1].GuildName);
    }

    private static async Task<long> CreateAccountAsync(NpgsqlDataSource dataSource, string username)
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
            INSERT INTO accounts (username, username_normalized, password_hash, login_enabled)
            VALUES ($1, $2, 'integration-test-hash', TRUE)
            RETURNING id;
            """);
        insert.Parameters.AddWithValue(username);
        insert.Parameters.AddWithValue(normalized);
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
        int positionY)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            """
            INSERT INTO characters (
                id, account_id, slot, name, class, level, playtime_minutes,
                strength, vitality, dexterity, intelligence,
                body_part, name_change, hair_part,
                position_x, position_y, map_id, skill_group)
            VALUES (
                $1, $2, $3, $4, $5, $6, $7,
                10, 11, 12, 13,
                0, 0, 0,
                $8, $9, 1, 0);
            """);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(accountId);
        command.Parameters.AddWithValue(slot);
        command.Parameters.AddWithValue(name);
        command.Parameters.AddWithValue(@class);
        command.Parameters.AddWithValue(level);
        command.Parameters.AddWithValue(playtimeMinutes);
        command.Parameters.AddWithValue(positionX);
        command.Parameters.AddWithValue(positionY);
        Assert.AreEqual(1, await command.ExecuteNonQueryAsync());
    }
}
