using System.Buffers.Binary;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;

namespace Metin2.Protocol.Tests;

[TestClass]
public sealed class GeneratedPacketFrameWriterTests
{
    [TestMethod]
    public void Handshake_writes_exact_legacy_frame()
    {
        var packet = new Handshake(0x11223344, 0x55667788, 0x99AABBCC);
        Span<byte> destination = stackalloc byte[13];

        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, destination, out int written);

        Assert.AreEqual(PacketFrameWriteStatus.Done, status);
        Assert.AreEqual(13, written);
        Assert.AreEqual((byte)0xFF, destination[0]);
        Assert.AreEqual(0x11223344u, BinaryPrimitives.ReadUInt32LittleEndian(destination.Slice(1, 4)));
        Assert.AreEqual(0x55667788u, BinaryPrimitives.ReadUInt32LittleEndian(destination.Slice(5, 4)));
        Assert.AreEqual(0x99AABBCCu, BinaryPrimitives.ReadUInt32LittleEndian(destination.Slice(9, 4)));
    }

    [TestMethod]
    public void Phase_writes_exact_reference_frame()
    {
        var packet = new Phase(10);
        Span<byte> destination = stackalloc byte[2];

        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, destination, out int written);

        Assert.AreEqual(PacketFrameWriteStatus.Done, status);
        Assert.AreEqual(2, written);
        Assert.AreEqual((byte)0xFD, destination[0]);
        Assert.AreEqual((byte)10, destination[1]);
    }

    [TestMethod]
    public void Sequenced_token_login_appends_supplied_sequence_byte()
    {
        var packet = new TokenLogin(
            "player",
            0xCAFEBABE,
            new uint[] { 1, 2, 3, 4 });
        Span<byte> destination = stackalloc byte[53];

        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, 0x7C, destination, out int written);

        Assert.AreEqual(PacketFrameWriteStatus.Done, status);
        Assert.AreEqual(53, written);
        Assert.AreEqual((byte)0x6D, destination[0]);
        Assert.AreEqual((byte)0x7C, destination[52]);
        Assert.AreEqual(0xCAFEBABEu, BinaryPrimitives.ReadUInt32LittleEndian(destination.Slice(32, 4)));
    }

    [TestMethod]
    public void Invalid_fixed_string_does_not_publish_frame_header()
    {
        var packet = new TokenLogin(
            new string('x', 31),
            1,
            new uint[] { 1, 2, 3, 4 });
        Span<byte> destination = stackalloc byte[53];
        destination.Fill(0xCC);

        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, 0x01, destination, out int written);

        Assert.AreEqual(PacketFrameWriteStatus.InvalidPacket, status);
        Assert.AreEqual(0, written);
        Assert.AreEqual((byte)0xCC, destination[0]);
    }

    [TestMethod]
    public void Undersized_destination_is_rejected_before_writing()
    {
        var packet = new Handshake(1, 2, 3);
        Span<byte> destination = stackalloc byte[12];
        destination.Fill(0xAA);

        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, destination, out int written);

        Assert.AreEqual(PacketFrameWriteStatus.InsufficientDestination, status);
        Assert.AreEqual(0, written);
        Assert.AreEqual((byte)0xAA, destination[0]);
    }
}
