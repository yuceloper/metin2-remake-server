using Metin2.Infrastructure.Persistence.Postgres.Security;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class Pbkdf2PasswordHasherTests
{
    [TestMethod]
    public void Correct_password_verifies_and_wrong_password_fails()
    {
        var hasher = new Pbkdf2PasswordHasher(iterations: 10_000);
        string encoded = hasher.Hash("correct horse battery staple");

        Assert.IsTrue(hasher.Verify("correct horse battery staple", encoded));
        Assert.IsFalse(hasher.Verify("wrong", encoded));
    }

    [TestMethod]
    public void Same_password_produces_different_hashes_due_to_unique_salts()
    {
        var hasher = new Pbkdf2PasswordHasher(iterations: 10_000);

        string first = hasher.Hash("password");
        string second = hasher.Hash("password");

        Assert.AreNotEqual(first, second);
        Assert.IsTrue(hasher.Verify("password", first));
        Assert.IsTrue(hasher.Verify("password", second));
    }

    [TestMethod]
    public void Malformed_or_unsupported_hashes_fail_safely()
    {
        var hasher = new Pbkdf2PasswordHasher(iterations: 10_000);

        Assert.IsFalse(hasher.Verify("password", string.Empty));
        Assert.IsFalse(hasher.Verify("password", "not-a-hash"));
        Assert.IsFalse(hasher.Verify("password", "$argon2id$i=1$AAAA$BBBB"));
        Assert.IsFalse(hasher.Verify("password", "$pbkdf2-sha256$i=nope$AAAA$BBBB"));
        Assert.IsFalse(hasher.Verify("password", "$pbkdf2-sha256$i=10000$%%%$%%%"));
    }

    [TestMethod]
    public void Encoded_hash_contains_versioned_algorithm_and_work_factor()
    {
        var hasher = new Pbkdf2PasswordHasher(iterations: 10_000);

        string encoded = hasher.Hash("password");

        StringAssert.StartsWith(encoded, "$pbkdf2-sha256$i=10000$");
    }
}
