using System.Reflection;
using Npgsql;

namespace Metin2.Infrastructure.Persistence.Postgres.Migrations;

public sealed class PostgresMigrator
{
    private const string MigrationResourceMarker = ".Migrations.";
    private const long MigrationAdvisoryLockId = 0x4D32524D;
    private readonly NpgsqlDataSource _dataSource;
    private readonly Assembly _migrationAssembly;

    public PostgresMigrator(NpgsqlDataSource dataSource, Assembly? migrationAssembly = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        _dataSource = dataSource;
        _migrationAssembly = migrationAssembly ?? typeof(PostgresMigrator).Assembly;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection lockConnection = await _dataSource
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await SetMigrationLockAsync(lockConnection, acquire: true, cancellationToken).ConfigureAwait(false);

        try
        {
            await EnsureHistoryTableAsync(cancellationToken).ConfigureAwait(false);

            IReadOnlyList<MigrationResource> migrations = DiscoverMigrations();
            HashSet<string> appliedVersions = await LoadAppliedVersionsAsync(cancellationToken).ConfigureAwait(false);

            foreach (MigrationResource migration in migrations)
            {
                if (appliedVersions.Contains(migration.Version))
                {
                    continue;
                }

                await ApplyAsync(migration, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await SetMigrationLockAsync(lockConnection, acquire: false, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static async Task SetMigrationLockAsync(
        NpgsqlConnection connection,
        bool acquire,
        CancellationToken cancellationToken)
    {
        string function = acquire ? "pg_advisory_lock" : "pg_advisory_unlock";
        await using var command = new NpgsqlCommand(
            $"SELECT {function}($1);",
            connection);
        command.Parameters.AddWithValue(MigrationAdvisoryLockId);
        _ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureHistoryTableAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = _dataSource.CreateCommand(
            """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version TEXT PRIMARY KEY,
                applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            """);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<HashSet<string>> LoadAppliedVersionsAsync(CancellationToken cancellationToken)
    {
        var versions = new HashSet<string>(StringComparer.Ordinal);
        await using NpgsqlCommand command = _dataSource.CreateCommand(
            "SELECT version FROM schema_migrations;");
        await using NpgsqlDataReader reader = await command
            .ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            versions.Add(reader.GetString(0));
        }

        return versions;
    }

    private async Task ApplyAsync(MigrationResource migration, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await _dataSource
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using NpgsqlTransaction transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await using (var migrationCommand = new NpgsqlCommand(migration.Sql, connection, transaction))
            {
                await migrationCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var historyCommand = new NpgsqlCommand(
                "INSERT INTO schema_migrations (version) VALUES ($1);",
                connection,
                transaction))
            {
                historyCommand.Parameters.AddWithValue(migration.Version);
                await historyCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    internal IReadOnlyList<MigrationResource> DiscoverMigrations()
    {
        return _migrationAssembly
            .GetManifestResourceNames()
            .Where(static name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .Where(name => name.Contains(MigrationResourceMarker, StringComparison.Ordinal))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .Select(LoadMigration)
            .ToArray();
    }

    private MigrationResource LoadMigration(string resourceName)
    {
        int markerIndex = resourceName.LastIndexOf(MigrationResourceMarker, StringComparison.Ordinal);
        string fileName = resourceName[(markerIndex + MigrationResourceMarker.Length)..];
        string version = Path.GetFileNameWithoutExtension(fileName);

        using Stream stream = _migrationAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Migration resource '{resourceName}' could not be opened.");
        using var reader = new StreamReader(stream);
        string sql = reader.ReadToEnd();

        return new MigrationResource(version, resourceName, sql);
    }
}

public readonly record struct MigrationResource(string Version, string ResourceName, string Sql);
