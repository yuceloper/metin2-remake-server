using System.Buffers.Binary;
using System.Security.Cryptography;
using Metin2.Modules.Auth.Application;
using Metin2.Shared.Identity;
using Npgsql;

namespace Metin2.Infrastructure.Persistence.Postgres.Auth;

public sealed class PostgresAuthTokenIssuer : IAuthTokenIssuer
{
    public static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromMinutes(5);
    private const int MaxCollisionRetries = 8;

    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeSpan _timeToLive;

    public PostgresAuthTokenIssuer(
        NpgsqlDataSource dataSource,
        TimeSpan? timeToLive = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        _dataSource = dataSource;
        _timeToLive = timeToLive ?? DefaultTimeToLive;

        if (_timeToLive <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), "Auth token TTL must be positive.");
        }
    }

    public async ValueTask<uint> IssueAsync(
        AccountId accountId,
        string username,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        string normalizedUsername = UsernameNormalizer.Normalize(username);

        for (int attempt = 0; attempt < MaxCollisionRetries; attempt++)
        {
            uint token = CreateToken();

            try
            {
                await using NpgsqlCommand command = _dataSource.CreateCommand(
                    """
                    INSERT INTO auth_tokens (token, account_id, username_normalized, expires_at)
                    VALUES ($1, $2, $3, NOW() + $4);
                    """);
                command.Parameters.AddWithValue((long)token);
                command.Parameters.AddWithValue((long)accountId.Value);
                command.Parameters.AddWithValue(normalizedUsername);
                command.Parameters.AddWithValue(_timeToLive);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return token;
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                // Extremely unlikely random token collision; generate another value.
            }
        }

        throw new InvalidOperationException("Could not allocate a unique auth token after repeated collisions.");
    }

    private static uint CreateToken()
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        uint token;
        do
        {
            RandomNumberGenerator.Fill(bytes);
            token = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        }
        while (token == 0);

        return token;
    }
}

public sealed class PostgresAuthTokenConsumer : IAuthTokenConsumer
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAuthTokenConsumer(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        _dataSource = dataSource;
    }

    public async ValueTask<AuthTokenPrincipal?> ConsumeAsync(
        uint token,
        string username,
        CancellationToken cancellationToken = default)
    {
        if (token == 0 || string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        string normalizedUsername = UsernameNormalizer.Normalize(username);

        await using NpgsqlCommand command = _dataSource.CreateCommand(
            """
            WITH consumed AS (
                DELETE FROM auth_tokens
                WHERE token = $1
                  AND username_normalized = $2
                  AND expires_at > NOW()
                RETURNING account_id
            )
            SELECT accounts.id, accounts.username
            FROM consumed
            INNER JOIN accounts ON accounts.id = consumed.account_id;
            """);
        command.Parameters.AddWithValue((long)token);
        command.Parameters.AddWithValue(normalizedUsername);

        await using NpgsqlDataReader reader = await command
            .ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        long rawAccountId = reader.GetInt64(0);
        if (rawAccountId is < 1 or > uint.MaxValue)
        {
            return null;
        }

        return new AuthTokenPrincipal(
            new AccountId(checked((uint)rawAccountId)),
            reader.GetString(1));
    }
}
