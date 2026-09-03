using Metin2.Infrastructure.Persistence.Postgres.Development;
using Metin2.Infrastructure.Persistence.Postgres.Migrations;
using Metin2.Infrastructure.Persistence.Postgres.Security;
using Npgsql;

namespace Metin2.Server;

public static class ServerDatabaseBootstrap
{
    public const string SeedDevelopmentAccountEnvironmentVariable = "METIN2_SEED_DEVELOPMENT_ACCOUNT";
    public const string DevelopmentUsernameEnvironmentVariable = "METIN2_DEV_USERNAME";
    public const string DevelopmentPasswordEnvironmentVariable = "METIN2_DEV_PASSWORD";

    public static async Task InitializeAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        await new PostgresMigrator(dataSource)
            .MigrateAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!string.Equals(
                Environment.GetEnvironmentVariable(SeedDevelopmentAccountEnvironmentVariable),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string? username = Environment.GetEnvironmentVariable(DevelopmentUsernameEnvironmentVariable);
        string? password = Environment.GetEnvironmentVariable(DevelopmentPasswordEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException(
                $"{DevelopmentUsernameEnvironmentVariable} and {DevelopmentPasswordEnvironmentVariable} " +
                $"are required when {SeedDevelopmentAccountEnvironmentVariable}=true.");
        }

        var seeder = new PostgresDevelopmentAccountSeeder(
            dataSource,
            new Pbkdf2PasswordHasher());
        await seeder.SeedAsync(username, password, cancellationToken).ConfigureAwait(false);
    }
}
