using Metin2.Infrastructure.Persistence.Postgres.Auth;
using Metin2.Infrastructure.Persistence.Postgres.Development;
using Metin2.Infrastructure.Persistence.Postgres.Migrations;
using Metin2.Infrastructure.Persistence.Postgres.Security;
using Metin2.Modules.Auth.Application;
using Npgsql;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class PostgresDevelopmentAccountSeederIntegrationTests
{
    private const string ConnectionStringEnvironmentVariable = "METIN2_TEST_POSTGRES_CONNECTION_STRING";

    [TestMethod]
    public async Task Concurrent_migrations_and_development_seed_are_idempotent()
    {
        string? connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Inconclusive($"Set {ConnectionStringEnvironmentVariable} to run PostgreSQL integration tests.");
            return;
        }

        await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
        await Task.WhenAll(
            new PostgresMigrator(dataSource).MigrateAsync(),
            new PostgresMigrator(dataSource).MigrateAsync());

        const string username = "ClientTrial";
        const string password = "client-trial-password";
        string normalized = UsernameNormalizer.Normalize(username);
        var hasher = new Pbkdf2PasswordHasher(iterations: 10_000);
        var seeder = new PostgresDevelopmentAccountSeeder(dataSource, hasher);

        await seeder.SeedAsync(username, password);
        await seeder.SeedAsync(username, password);

        var verifier = new PostgresAccountCredentialVerifier(dataSource, hasher);
        CredentialVerificationResult result = await verifier.VerifyAsync(username, password);
        Assert.IsTrue(result.IsSuccess);

        await using NpgsqlCommand command = dataSource.CreateCommand(
            """
            SELECT COUNT(*)
            FROM characters
            WHERE account_id = $1 AND slot = 0 AND name = $2;
            """);
        command.Parameters.AddWithValue((long)result.AccountId.Value);
        command.Parameters.AddWithValue("ClientTrialHero");
        Assert.AreEqual(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }
}
