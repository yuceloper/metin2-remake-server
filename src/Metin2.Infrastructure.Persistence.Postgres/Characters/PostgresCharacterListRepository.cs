using Metin2.Modules.Characters.Application;
using Metin2.Shared.Identity;
using Npgsql;

namespace Metin2.Infrastructure.Persistence.Postgres.Characters;

public sealed class PostgresCharacterListRepository(NpgsqlDataSource dataSource) : ICharacterListRepository
{
    private const string Query = """
        SELECT
            id,
            slot,
            name,
            class,
            level,
            playtime_minutes,
            strength,
            vitality,
            dexterity,
            intelligence,
            body_part,
            name_change,
            hair_part,
            position_x,
            position_y,
            skill_group
        FROM characters
        WHERE account_id = $1
        ORDER BY slot;
        """;

    public async ValueTask<IReadOnlyList<CharacterListEntry>> GetByAccountAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(Query);
        command.Parameters.AddWithValue((long)accountId.Value);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var entries = new List<CharacterListEntry>(4);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(new CharacterListEntry(
                checked((byte)reader.GetInt16(1)),
                new CharacterId(checked((uint)reader.GetInt64(0))),
                reader.GetString(2),
                checked((byte)reader.GetInt16(3)),
                checked((byte)reader.GetInt16(4)),
                checked((uint)reader.GetInt64(5)),
                checked((byte)reader.GetInt16(6)),
                checked((byte)reader.GetInt16(7)),
                checked((byte)reader.GetInt16(8)),
                checked((byte)reader.GetInt16(9)),
                checked((ushort)reader.GetInt32(10)),
                checked((byte)reader.GetInt16(11)),
                checked((ushort)reader.GetInt32(12)),
                reader.GetInt32(13),
                reader.GetInt32(14),
                checked((byte)reader.GetInt16(15)),
                new GuildId(0),
                string.Empty));
        }

        return entries;
    }
}
