using Metin2.Modules.Characters.Application;
using Metin2.Shared.Identity;
using Npgsql;

namespace Metin2.Infrastructure.Persistence.Postgres.Characters;

public sealed class PostgresCharacterBootstrapRepository(NpgsqlDataSource dataSource) : ICharacterBootstrapRepository
{
    public async ValueTask<CharacterBootstrapSnapshot?> GetOwnedAsync(
        AccountId accountId,
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            """
            SELECT
                c.id,
                c.account_id,
                c.name,
                c.class,
                c.level,
                c.experience,
                c.gold,
                c.strength,
                c.vitality,
                c.dexterity,
                c.intelligence,
                c.body_part,
                c.hair_part,
                c.position_x,
                c.position_y,
                c.map_id,
                c.skill_group,
                c.available_status_points,
                a.empire
            FROM characters AS c
            INNER JOIN accounts AS a ON a.id = c.account_id
            WHERE c.id = $1 AND c.account_id = $2
            LIMIT 1;
            """);
        command.Parameters.AddWithValue((long)characterId.Value);
        command.Parameters.AddWithValue((long)accountId.Value);

        await using NpgsqlDataReader reader = await command
            .ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new CharacterBootstrapSnapshot(
            new CharacterId(checked((uint)reader.GetInt64(0))),
            new AccountId(checked((uint)reader.GetInt64(1))),
            reader.GetString(2),
            checked((byte)reader.GetInt16(3)),
            checked((byte)reader.GetInt16(4)),
            checked((uint)reader.GetInt64(5)),
            checked((uint)reader.GetInt64(6)),
            checked((byte)reader.GetInt16(7)),
            checked((byte)reader.GetInt16(8)),
            checked((byte)reader.GetInt16(9)),
            checked((byte)reader.GetInt16(10)),
            checked((ushort)reader.GetInt32(11)),
            checked((ushort)reader.GetInt32(12)),
            reader.GetInt32(13),
            reader.GetInt32(14),
            new MapId(reader.GetInt32(15)),
            checked((byte)reader.GetInt16(16)),
            checked((uint)reader.GetInt64(17)),
            checked((byte)reader.GetInt16(18)));
    }
}
