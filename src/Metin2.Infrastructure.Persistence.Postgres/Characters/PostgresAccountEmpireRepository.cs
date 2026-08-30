using Metin2.Modules.Characters.Application;
using Metin2.Shared.Identity;
using Npgsql;

namespace Metin2.Infrastructure.Persistence.Postgres.Characters;

public sealed class PostgresAccountEmpireRepository(NpgsqlDataSource dataSource) : IAccountEmpireRepository
{
    public async ValueTask<byte> GetEmpireAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT empire FROM accounts WHERE id = $1;");
        command.Parameters.AddWithValue((long)accountId.Value);

        object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (value is null || value is DBNull)
        {
            throw new KeyNotFoundException($"Account {accountId.Value} was not found.");
        }

        return checked((byte)Convert.ToInt16(value));
    }
}
