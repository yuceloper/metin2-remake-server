using Metin2.Infrastructure.Networking.Compatibility;
using Metin2.Infrastructure.Networking.Security;
using Metin2.Protocol.Legacy;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class LegacyClientCompatibilityProfileTests
{
    [TestMethod]
    public void Classic_TEA_requires_mode_specific_key_material()
    {
        var sequence = new LegacySequenceProfile("test", new byte[] { 0xAA });

        Assert.ThrowsExactly<ArgumentException>(() =>
            new LegacyClientCompatibilityProfile(
                "invalid",
                sequence,
                LegacyPacketEncryptionMode.ClassicTea));
    }

    [TestMethod]
    public void Improved_mode_is_explicitly_represented_but_not_claimed_implemented()
    {
        var profile = new LegacyClientCompatibilityProfile(
            "40250-research-placeholder",
            new LegacySequenceProfile("unknown-target-table", new byte[] { 0xAA }),
            LegacyPacketEncryptionMode.ImprovedPacketEncryption);

        Assert.AreEqual(LegacyPacketEncryptionMode.ImprovedPacketEncryption, profile.EncryptionMode);
        Assert.IsFalse(profile.IsEncryptionImplemented);
    }

    [TestMethod]
    public void Classic_profile_keeps_sequence_and_crypto_material_together()
    {
        var sequence = new LegacySequenceProfile("reference-sequence", new byte[] { 0xAA, 0xBB });
        var tea = new LegacyTeaSecurityProfile(
            "reference-tea",
            "1234abcd5678efgh"u8,
            "1234abcd5678efgh"u8);
        var profile = new LegacyClientCompatibilityProfile(
            "reference-classic",
            sequence,
            LegacyPacketEncryptionMode.ClassicTea,
            tea);

        Assert.AreSame(sequence, profile.Sequence);
        Assert.AreSame(tea, profile.ClassicTea);
        Assert.IsTrue(profile.IsEncryptionImplemented);
    }
}
