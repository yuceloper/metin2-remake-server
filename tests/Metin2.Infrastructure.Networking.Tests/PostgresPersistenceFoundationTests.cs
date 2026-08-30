using Metin2.Infrastructure.Persistence.Postgres.Auth;
using Metin2.Infrastructure.Persistence.Postgres.Migrations;
using Npgsql;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class PostgresPersistenceFoundationTests
{
    [TestMethod]
    public void Username_normalization_is_trimmed_and_case_insensitive()
    {
        Assert.AreEqual("player", UsernameNormalizer.Normalize("  PlAyEr  "));
    }

    [TestMethod]
    public void Initial_accounts_migration_is_embedded_and_discoverable()
    {
        using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1");
        var migrator = new PostgresMigrator(dataSource);

        IReadOnlyList<MigrationResource> migrations = migrator.DiscoverMigrations();

        Assert.IsTrue(migrations.Count >= 1);
        MigrationResource initial = migrations.Single(static migration =>
            migration.Version == "V001__create_accounts");
        StringAssert.Contains(initial.Sql, "CREATE TABLE IF NOT EXISTS accounts");
        StringAssert.Contains(initial.Sql, "username_normalized");
        StringAssert.Contains(initial.Sql, "password_hash");
    }
}
