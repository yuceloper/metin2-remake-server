using Metin2.Infrastructure.Networking.Security;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class LegacyTeaSecurityTests
{
    [TestMethod]
    public void Zero_block_and_zero_key_match_classic_metin2_block_vector()
    {
        var block = new byte[8];
        var key = new uint[4];

        LegacyTeaCipher.EncryptBlock(block, key);

        CollectionAssert.AreEqual(
            new byte[] { 0xD8, 0xD4, 0xE9, 0xDE, 0xD9, 0x1E, 0x13, 0xF7 },
            block);

        LegacyTeaCipher.DecryptBlock(block, key);
        CollectionAssert.AreEqual(new byte[8], block);
    }

    [TestMethod]
    public void Padded_encrypt_matches_metin2_zero_fill_and_eight_byte_rounding()
    {
        byte[] plaintext = [0x01, 0x02, 0x03];
        var encrypted = new byte[LegacyTeaCipher.GetEncryptedSize(plaintext.Length)];
        var decrypted = new byte[encrypted.Length];

        int encryptedLength = LegacyTeaCipher.EncryptPadded(plaintext, encrypted, new uint[4]);

        Assert.AreEqual(8, encryptedLength);
        CollectionAssert.AreEqual(
            new byte[] { 0xA4, 0x9C, 0x57, 0x76, 0x58, 0xE8, 0xE0, 0x12 },
            encrypted);

        int decryptedLength = LegacyTeaCipher.DecryptBlocks(encrypted, decrypted, new uint[4]);
        Assert.AreEqual(8, decryptedLength);
        CollectionAssert.AreEqual(
            new byte[] { 0x01, 0x02, 0x03, 0, 0, 0, 0, 0 },
            decrypted);
    }

    [TestMethod]
    public void Profile_derives_server_encryption_key_using_metin2_block_semantics()
    {
        var profile = CreateReferenceProfile();
        uint[] clientKey = [1, 2, 3, 4];
        Span<uint> derived = stackalloc uint[4];

        profile.DeriveServerEncryptionKey(clientKey, derived);

        CollectionAssert.AreEqual(
            new uint[] { 0xF2703277u, 0xBF70B4FBu, 0x3F970615u, 0xB1DE9815u },
            derived.ToArray());
    }

    [TestMethod]
    public void Security_state_activates_initial_key_then_rotates_from_Login2_client_key()
    {
        LegacyTeaSecurityProfile profile = CreateReferenceProfile();
        var state = new LegacyTeaSecurityState();
        uint[] clientKey = [1, 2, 3, 4];

        Assert.AreEqual(LegacyTeaSecurityStage.Plaintext, state.Stage);
        Assert.IsFalse(state.IsActive);

        state.ActivateInitial(profile);

        Assert.AreEqual(LegacyTeaSecurityStage.InitialKey, state.Stage);
        Assert.IsTrue(state.IsActive);
        CollectionAssert.AreEqual(
            new uint[] { 0x34333231u, 0x64636261u, 0x38373635u, 0x68676665u },
            state.DecryptionKey.ToArray());
        CollectionAssert.AreEqual(state.DecryptionKey.ToArray(), state.EncryptionKey.ToArray());

        state.RotateFromClientKey(clientKey, profile);

        Assert.AreEqual(LegacyTeaSecurityStage.RotatedClientKey, state.Stage);
        CollectionAssert.AreEqual(clientKey, state.DecryptionKey.ToArray());
        CollectionAssert.AreEqual(
            new uint[] { 0xF2703277u, 0xBF70B4FBu, 0x3F970615u, 0xB1DE9815u },
            state.EncryptionKey.ToArray());
    }

    [TestMethod]
    public void Cipher_rejects_partial_ciphertext_blocks()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            LegacyTeaCipher.DecryptBlocks(new byte[7], new byte[7], new uint[4]));
    }

    private static LegacyTeaSecurityProfile CreateReferenceProfile() =>
        new(
            "reference-europe-static-key",
            "1234abcd5678efgh"u8,
            "1234abcd5678efgh"u8);
}
