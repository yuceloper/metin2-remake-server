using System.Buffers.Binary;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;

namespace Metin2.Protocol.Tests;

[TestClass]
public sealed class ImprovedKeyAgreementPacketTests
{
    [TestMethod]
    public void KeyAgreement_writes_exact_fixed_261_byte_frame()
    {
        byte[] data = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        var packet = new KeyAgreement(128, 256, data);
        var frame = new byte[261];

        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, frame, out int written);

        Assert.AreEqual(PacketFrameWriteStatus.Done, status);
        Assert.AreEqual(261, written);
        Assert.AreEqual((byte)0xFB, frame[0]);
        Assert.AreEqual((ushort)128, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(1, 2)));
        Assert.AreEqual((ushort)256, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(3, 2)));
        CollectionAssert.AreEqual(data, frame.AsSpan(5, 256).ToArray());
    }

    [TestMethod]
    public void KeyAgreementCompleted_writes_exact_four_byte_plain_transition_frame()
    {
        var packet = new KeyAgreementCompleted(new byte[3]);
        var frame = new byte[4];

        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, frame, out int written);

        Assert.AreEqual(PacketFrameWriteStatus.Done, status);
        Assert.AreEqual(4, written);
        CollectionAssert.AreEqual(new byte[] { 0xFA, 0, 0, 0 }, frame);
    }
}
