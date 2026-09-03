using Metin2.Infrastructure.Networking.Compatibility;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class ClientVs22EncryptionModeTests
{
    [TestMethod]
    public void Explicit_none_mode_preserves_source_verified_sequence_profile()
    {
        LegacyClientCompatibilityProfile improved =
            ClientVs22_28249CompatibilityProfile.Create();
        LegacyClientCompatibilityProfile plaintext =
            ClientVs22_28249CompatibilityProfile.Create(LegacyPacketEncryptionMode.None);

        Assert.AreEqual(
            LegacyPacketEncryptionMode.ImprovedPacketEncryption,
            improved.EncryptionMode);
        Assert.AreEqual(LegacyPacketEncryptionMode.None, plaintext.EncryptionMode);
        Assert.AreEqual(improved.Sequence.Length, plaintext.Sequence.Length);
        Assert.AreEqual(improved.Sequence[0], plaintext.Sequence[0]);
        Assert.AreEqual(
            improved.Sequence[improved.Sequence.Length - 1],
            plaintext.Sequence[plaintext.Sequence.Length - 1]);
    }
}
