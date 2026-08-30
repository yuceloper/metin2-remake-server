using Metin2.Infrastructure.Persistence.Postgres.Auth;
using Metin2.Infrastructure.Persistence.Postgres.Migrations;
using Metin2.Infrastructure.Persistence.Postgres.Security;
using Metin2.Modules.Auth.Application;
using Metin2.Shared.Identity;
using Npgsql;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class PostgresAuthTokenStoreIntegrationTests
{
    private const string ConnectionStringEnvironmentVariable = "METIN2_TEST_POSTGRES_CONNECTION_STRING";

    [TestMethod]
    public async Task Issued_token_is_one_time_bound_to_username_and_expires()
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
        await migrator.MigrateAsync();

        await using (NpgsqlCommand historyCommand = dataSource.CreateCommand(
            "SELECT COUNT(*) FROM schema_migrations WHERE version = $1;"))
        {
            historyCommand.Parameters.AddWithValue("V002__create_auth_tokens");
            object? count = await historyCommand.ExecuteScalarAsync();
            Assert.AreEqual(1L, Convert.ToInt64(count));
        }

        const string username = "TokenPlayer";
        string normalizedUsername = UsernameNormalizer.Normalize(username);
        var hasher = new Pbkdf2PasswordHasher(iterations: 10_000);

        await using (NpgsqlCommand cleanupCommand = dataSource.CreateCommand(
            "DELETE FROM accounts WHERE username_normalized = $1;"))
        {
            cleanupCommand.Parameters.AddWithValue(normalizedUsername);
            await cleanupCommand.ExecuteNonQueryAsync();
        }

        long rawAccountId;
        await using (NpgsqlCommand insertCommand = dataSource.CreateCommand(
            """
            INSERT INTO accounts (username, username_normalized, password_hash, login_enabled)
            VALUES ($1, $2, $3, TRUE)
            RETURNING id;
            """))
        {
            insertCommand.Parameters.AddWithValue(username);
            insertCommand.Parameters.AddWithValue(normalizedUsername);
            insertCommand.Parameters.AddWithValue(hasher.Hash("password"));
            rawAccountId = Convert.ToInt64(await insertCommand.ExecuteScalarAsync());
        }

        var accountId = new AccountId(checked((uint)rawAccountId));
        var consumer = new PostgresAuthTokenConsumer(dataSource);
        var issuer = new PostgresAuthTokenIssuer(dataSource, TimeSpan.FromMinutes(5));

        uint token = await issuer.IssueAsync(accountId, username);
        Assert.AreNotEqual(0u, token);

        AuthTokenPrincipal? wrongUsername = await consumer.ConsumeAsync(token, "SomebodyElse");
        Assert.IsNull(wrongUsername);

        AuthTokenPrincipal? principal = await consumer.ConsumeAsync(token, " tokenplayer ");
        Assert.IsTrue(principal.HasValue);
        Assert.AreEqual(accountId, principal.Value.AccountId);
        Assert.AreEqual(username, principal.Value.Username);

        AuthTokenPrincipal? replay = await consumer.ConsumeAsync(token, username);
        Assert.IsNull(replay);

        var shortLivedIssuer = new PostgresAuthTokenIssuer(dataSource, TimeSpan.FromMilliseconds(20));
        uint expiringToken = await shortLivedIssuer.IssueAsync(accountId, username);
        await Task.Delay(100);

        AuthTokenPrincipal? expired = await consumer.ConsumeAsync(expiringToken, username);
        Assert.IsNull(expired);
    }
}
