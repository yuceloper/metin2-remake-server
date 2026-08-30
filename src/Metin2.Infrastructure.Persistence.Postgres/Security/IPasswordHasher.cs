namespace Metin2.Infrastructure.Persistence.Postgres.Security;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string encodedHash);
}
