using Metin2.Modules.Characters.Application;
using Metin2.Shared.Identity;
using Npgsql;

namespace Metin2.Infrastructure.Persistence.Postgres.Characters;

public sealed class PostgresCharacterSelectionRepository(NpgsqlDataSource dataSource) : ICharacterSelectionRepository
{
    public async ValueTask<CharacterId?> FindOwnedCharacterIdAsync(
        AccountId accountId,
        byte slot,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT id FROM characters WHERE account_id = $1 AND slot = $2;");
        command.Parameters.AddWithValue((long)accountId.Value);
        command.Parameters.AddWithValue((short)slot);

        object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (value is null || value is DBNull)
        {
            return null;
        }

        return new CharacterId(checked((uint)Convert.ToInt64(value)));
    }
}
