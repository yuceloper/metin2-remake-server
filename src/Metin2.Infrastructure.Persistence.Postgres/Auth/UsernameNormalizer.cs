namespace Metin2.Infrastructure.Persistence.Postgres.Auth;

public static class UsernameNormalizer
{
    public static string Normalize(string username)
    {
        ArgumentNullException.ThrowIfNull(username);
        return username.Trim().ToLowerInvariant();
    }
}
