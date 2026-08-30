using Npgsql;

namespace Metin2.Infrastructure.Persistence.Postgres;

public static class PostgresDataSourceFactory
{
    public static NpgsqlDataSource Create(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        return builder.Build();
    }
}
