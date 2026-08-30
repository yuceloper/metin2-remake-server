using Metin2.Infrastructure.Persistence.Postgres.Auth;
using Metin2.Infrastructure.Persistence.Postgres.Migrations;
using Metin2.Infrastructure.Persistence.Postgres.Security;
using Metin2.Modules.Auth.Application;
using Npgsql;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class PostgresAccountCredentialVerifierIntegrationTests
{
    private const string ConnectionStringEnvironmentVariable = "METIN2_TEST_POSTGRES_CONNECTION_STRING";

    [TestMethod]
    public async Task Migrations_and_account_verification_work_against_live_postgres()
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
            historyCommand.Parameters.AddWithValue("V001__create_accounts");
            object? count = await historyCommand.ExecuteScalarAsync();
            Assert.AreEqual(1L, Convert.ToInt64(count));
        }

        const string username = "IntegrationPlayer";
        const string password = "correct-horse-battery-staple";
        string normalizedUsername = UsernameNormalizer.Normalize(username);
        var hasher = new Pbkdf2PasswordHasher(iterations: 10_000);
        string passwordHash = hasher.Hash(password);

        await using (NpgsqlCommand cleanupCommand = dataSource.CreateCommand(
            "DELETE FROM accounts WHERE username_normalized = $1;"))
        {
            cleanupCommand.Parameters.AddWithValue(normalizedUsername);
            await cleanupCommand.ExecuteNonQueryAsync();
        }

        await using (NpgsqlCommand insertCommand = dataSource.CreateCommand(
            """
            INSERT INTO accounts (username, username_normalized, password_hash, login_enabled)
            VALUES ($1, $2, $3, TRUE);
            """))
        {
            insertCommand.Parameters.AddWithValue(username);
            insertCommand.Parameters.AddWithValue(normalizedUsername);
            insertCommand.Parameters.AddWithValue(passwordHash);
            Assert.AreEqual(1, await insertCommand.ExecuteNonQueryAsync());
        }

        var verifier = new PostgresAccountCredentialVerifier(dataSource, hasher);

        CredentialVerificationResult success = await verifier.VerifyAsync(" integrationplayer ", password);
        Assert.IsTrue(success.IsSuccess);
        Assert.AreEqual(username, success.Username);
        Assert.IsTrue(success.AccountId.Value > 0);

        CredentialVerificationResult wrongPassword = await verifier.VerifyAsync(username, "wrong-password");
        Assert.IsFalse(wrongPassword.IsSuccess);

        await using (NpgsqlCommand disableCommand = dataSource.CreateCommand(
            "UPDATE accounts SET login_enabled = FALSE WHERE username_normalized = $1;"))
        {
            disableCommand.Parameters.AddWithValue(normalizedUsername);
            Assert.AreEqual(1, await disableCommand.ExecuteNonQueryAsync());
        }

        CredentialVerificationResult disabledAccount = await verifier.VerifyAsync(username, password);
        Assert.IsFalse(disabledAccount.IsSuccess);
    }
}
