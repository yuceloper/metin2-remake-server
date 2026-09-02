using Metin2.Infrastructure.Networking.Compatibility;
using Metin2.Protocol.Legacy;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class ClientVs22_28249CompatibilityProfileTests
{
    [TestMethod]
    public void Source_verified_profile_uses_exact_sequence_and_improved_transport()
    {
        LegacyClientCompatibilityProfile profile = ClientVs22_28249CompatibilityProfile.Create();

        Assert.AreEqual(ClientVs22_28249CompatibilityProfile.Name, profile.Name);
        Assert.AreEqual(LegacyPacketEncryptionMode.ImprovedPacketEncryption, profile.EncryptionMode);
        Assert.IsTrue(profile.IsEncryptionImplemented);
        Assert.AreEqual(ClientVs22_28249CompatibilityProfile.SequenceLength, profile.Sequence.Length);

        Assert.AreEqual((byte)0xAF, profile.Sequence[0]);
        Assert.AreEqual((byte)0xCA, profile.Sequence[1]);
        Assert.AreEqual((byte)0x84, profile.Sequence[15]);
        Assert.AreEqual((byte)0xBC, profile.Sequence[16]);
        Assert.AreEqual((byte)0x45, profile.Sequence[255]);
        Assert.AreEqual((byte)0x6A, profile.Sequence[256]);
        Assert.AreEqual((byte)0x68, profile.Sequence[1023]);
        Assert.AreEqual((byte)0xB2, profile.Sequence[4096]);
        Assert.AreEqual((byte)0xCC, profile.Sequence[16384]);
        Assert.AreEqual((byte)0x24, profile.Sequence[32766]);
        Assert.AreEqual((byte)0x81, profile.Sequence[32767]);
    }

    [TestMethod]
    public void Sequence_profile_instances_do_not_share_mutable_storage()
    {
        LegacySequenceProfile first = ClientVs22_28249CompatibilityProfile.CreateSequenceProfile();
        LegacySequenceProfile second = ClientVs22_28249CompatibilityProfile.CreateSequenceProfile();

        Assert.AreNotSame(first, second);
        Assert.AreEqual(first.Length, second.Length);
        Assert.AreEqual(first[0], second[0]);
        Assert.AreEqual(first[first.Length - 1], second[second.Length - 1]);
    }
}
