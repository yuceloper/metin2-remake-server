using System.Text;
using Metin2.Infrastructure.Persistence.Postgres.Auth;
using Metin2.Infrastructure.Persistence.Postgres.Security;
using Npgsql;

namespace Metin2.Infrastructure.Persistence.Postgres.Development;

public sealed class PostgresDevelopmentAccountSeeder(
    NpgsqlDataSource dataSource,
    IPasswordHasher passwordHasher)
{
    public async Task SeedAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrEmpty(password);

        username = username.Trim();
        if (username.Length > 30 || Encoding.ASCII.GetByteCount(username) != username.Length)
        {
            throw new ArgumentException(
                "Development username must be ASCII and at most 30 characters.",
                nameof(username));
        }

        string normalizedUsername = UsernameNormalizer.Normalize(username);
        string passwordHash = passwordHasher.Hash(password);
        string characterName = CreateCharacterName(username);

        await using NpgsqlCommand command = dataSource.CreateCommand(
            """
            WITH account AS (
                INSERT INTO accounts (
                    username,
                    username_normalized,
                    password_hash,
                    login_enabled,
                    empire)
                VALUES ($1, $2, $3, TRUE, 1)
                ON CONFLICT (username_normalized) DO UPDATE SET
                    username = EXCLUDED.username,
                    password_hash = EXCLUDED.password_hash,
                    login_enabled = TRUE,
                    empire = 1,
                    updated_at = NOW()
                RETURNING id
            )
            INSERT INTO characters (
                account_id,
                slot,
                name,
                class,
                level,
                strength,
                vitality,
                dexterity,
                intelligence,
                position_x,
                position_y,
                map_id,
                experience,
                gold,
                available_status_points)
            SELECT
                id,
                0,
                $4,
                0,
                1,
                6,
                4,
                3,
                3,
                459770,
                953980,
                1,
                0,
                10000,
                0
            FROM account
            ON CONFLICT (account_id, slot) DO UPDATE SET
                name = EXCLUDED.name,
                updated_at = NOW();
            """);
        command.Parameters.AddWithValue(username);
        command.Parameters.AddWithValue(normalizedUsername);
        command.Parameters.AddWithValue(passwordHash);
        command.Parameters.AddWithValue(characterName);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string CreateCharacterName(string username)
    {
        const string suffix = "Hero";
        int prefixLength = Math.Min(username.Length, 24 - suffix.Length);
        return username[..prefixLength] + suffix;
    }
}
