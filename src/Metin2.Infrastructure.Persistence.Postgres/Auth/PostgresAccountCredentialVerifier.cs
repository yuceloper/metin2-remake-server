using Metin2.Infrastructure.Persistence.Postgres.Security;
using Metin2.Modules.Auth.Application;
using Metin2.Shared.Identity;
using Npgsql;

namespace Metin2.Infrastructure.Persistence.Postgres.Auth;

public sealed class PostgresAccountCredentialVerifier : IAccountCredentialVerifier
{
    private const string DummyPasswordHash = "$pbkdf2-sha256$i=600000$AAAAAAAAAAAAAAAAAAAAAA==$ccQiYcYQnJtuyO0XjYgwnGsfF5Uw4ZLsXmVqY48VZps=";

    private readonly NpgsqlDataSource _dataSource;
    private readonly IPasswordHasher _passwordHasher;

    public PostgresAccountCredentialVerifier(
        NpgsqlDataSource dataSource,
        IPasswordHasher passwordHasher)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        _dataSource = dataSource;
        _passwordHasher = passwordHasher;
    }

    public async ValueTask<CredentialVerificationResult> VerifyAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return CredentialVerificationResult.InvalidCredentials();
        }

        string normalizedUsername = UsernameNormalizer.Normalize(username);

        await using NpgsqlCommand command = _dataSource.CreateCommand(
            """
            SELECT id, username, password_hash, login_enabled
            FROM accounts
            WHERE username_normalized = $1
            LIMIT 1;
            """);
        command.Parameters.AddWithValue(normalizedUsername);

        await using NpgsqlDataReader reader = await command
            .ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            _ = _passwordHasher.Verify(password, DummyPasswordHash);
            return CredentialVerificationResult.InvalidCredentials();
        }

        long rawAccountId = reader.GetInt64(0);
        string canonicalUsername = reader.GetString(1);
        string passwordHash = reader.GetString(2);
        bool loginEnabled = reader.GetBoolean(3);

        bool passwordMatches = _passwordHasher.Verify(password, passwordHash);
        if (!passwordMatches || !loginEnabled || rawAccountId is < 1 or > uint.MaxValue)
        {
            return CredentialVerificationResult.InvalidCredentials();
        }

        return CredentialVerificationResult.Success(
            new AccountId(checked((uint)rawAccountId)),
            canonicalUsername);
    }
}
