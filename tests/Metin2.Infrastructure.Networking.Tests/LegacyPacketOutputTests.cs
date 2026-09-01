using System.Buffers;
using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Compatibility;
using Metin2.Infrastructure.Networking.Security;
using Metin2.Infrastructure.Networking.Send;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Legacy;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class LegacyPacketOutputTests
{
    [TestMethod]
    public async Task None_mode_queues_plaintext_frames_without_padding()
    {
        var pipe = new Pipe();
        var session = new GameSession(PacketPhase.Login);
        var output = new LegacyPacketOutput(pipe.Writer, session);

        Assert.AreEqual(3, output.Write(new byte[] { 1, 2, 3 }));
        Assert.AreEqual(2, output.Write(new byte[] { 4, 5 }));
        await output.FlushAsync();

        ReadResult read = await pipe.Reader.ReadAsync();
        byte[] bytes = read.Buffer.ToArray();
        pipe.Reader.AdvanceTo(read.Buffer.End);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, bytes);
    }

    [TestMethod]
    public async Task Classic_tea_encrypts_each_packet_with_key_stage_active_when_enqueued()
    {
        LegacyTeaSecurityProfile teaProfile = CreateTeaProfile();
        var compatibility = new LegacyClientCompatibilityProfile(
            "classic-test",
            new LegacySequenceProfile("test", new byte[] { 0xAA }),
            LegacyPacketEncryptionMode.ClassicTea,
            teaProfile);
        var session = new GameSession(PacketPhase.Login, compatibilityProfile: compatibility);
        session.ActivateConfiguredPacketSecurity();
        Assert.IsNotNull(session.TeaSecurityState);
        uint[] initialEncryptionKey = session.TeaSecurityState.EncryptionKey.ToArray();
        var pipe = new Pipe();
        var output = new LegacyPacketOutput(pipe.Writer, session);

        Assert.AreEqual(8, output.Write(new byte[] { 1, 2, 3 }));

        session.RotateConfiguredPacketSecurity(new uint[] { 11, 22, 33, 44 });
        uint[] rotatedEncryptionKey = session.TeaSecurityState.EncryptionKey.ToArray();
        Assert.AreEqual(8, output.Write(new byte[] { 4, 5 }));
        await output.FlushAsync();

        ReadResult read = await pipe.Reader.ReadAsync();
        byte[] ciphertext = read.Buffer.ToArray();
        pipe.Reader.AdvanceTo(read.Buffer.End);
        Assert.AreEqual(16, ciphertext.Length);

        var first = new byte[8];
        var second = new byte[8];
        LegacyTeaCipher.DecryptBlocks(ciphertext.AsSpan(0, 8), first, initialEncryptionKey);
        LegacyTeaCipher.DecryptBlocks(ciphertext.AsSpan(8, 8), second, rotatedEncryptionKey);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 0, 0, 0, 0, 0 }, first);
        CollectionAssert.AreEqual(new byte[] { 4, 5, 0, 0, 0, 0, 0, 0 }, second);
    }

    private static LegacyTeaSecurityProfile CreateTeaProfile() =>
        new("classic-test", "1234abcd5678efgh"u8, "abcdefghijklmnop"u8);
}
