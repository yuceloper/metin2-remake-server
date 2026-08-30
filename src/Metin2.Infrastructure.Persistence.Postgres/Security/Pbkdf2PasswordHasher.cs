using System.Globalization;
using System.Security.Cryptography;

namespace Metin2.Infrastructure.Persistence.Postgres.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const int DefaultIterations = 600_000;
    public const int SaltSize = 16;
    public const int HashSize = 32;

    private const string Algorithm = "pbkdf2-sha256";
    private readonly int _iterations;

    public Pbkdf2PasswordHasher(int iterations = DefaultIterations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
        _iterations = iterations;
    }

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            _iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"${Algorithm}$i={_iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}");
    }

    public bool Verify(string password, string encodedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(encodedHash))
        {
            return false;
        }

        if (!TryParse(encodedHash, out int iterations, out byte[]? salt, out byte[]? expectedHash))
        {
            return false;
        }

        byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static bool TryParse(
        string encodedHash,
        out int iterations,
        out byte[] salt,
        out byte[] expectedHash)
    {
        iterations = 0;
        salt = Array.Empty<byte>();
        expectedHash = Array.Empty<byte>();

        string[] parts = encodedHash.Split('$', StringSplitOptions.None);
        if (parts.Length != 5 ||
            parts[0].Length != 0 ||
            !string.Equals(parts[1], Algorithm, StringComparison.Ordinal) ||
            !parts[2].StartsWith("i=", StringComparison.Ordinal) ||
            !int.TryParse(parts[2].AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out iterations) ||
            iterations <= 0)
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expectedHash = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length == SaltSize && expectedHash.Length == HashSize;
    }
}
