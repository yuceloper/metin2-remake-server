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
        CollectionAssert.AreEqual(
            improved.Sequence.Table.ToArray(),
            plaintext.Sequence.Table.ToArray());
    }
}
