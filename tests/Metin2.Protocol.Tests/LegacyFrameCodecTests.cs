using Metin2.Protocol.Framing;
using Metin2.Protocol.Generated;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Metin2.Protocol.Tests;

[TestClass]
public sealed class LegacyFrameCodecTests
{
    [TestMethod]
    public void Decode_Handshake_ReturnsZeroCopyPayloadView()
    {
        byte[] bytes =
        {
            0xFF,
            0x78, 0x56, 0x34, 0x12,
            0x04, 0x03, 0x02, 0x01,
            0x00, 0x00, 0x00, 0x00
        };

        LegacyFrameDecodeStatus status = LegacyFrameCodec.TryDecode(
            bytes,
            PacketDirection.ClientToServer,
            PacketPhase.Handshake,
            out LegacyFrame frame);

        Assert.AreEqual(LegacyFrameDecodeStatus.Done, status);
        Assert.AreEqual(PacketId.Handshake, frame.Registration.Id);
        Assert.AreEqual(12, frame.Payload.Length);
        Assert.AreEqual(13, frame.FrameSize);
        Assert.IsNull(frame.Sequence);
        Assert.AreEqual((byte)0x78, frame.Payload[0]);
    }

    [TestMethod]
    public void Decode_LoginRequest_ExposesTrailingSequenceByte()
    {
        byte[] bytes = new byte[66];
        bytes[0] = 0x6F;
        bytes[^1] = 0x42;

        LegacyFrameDecodeStatus status = LegacyFrameCodec.TryDecode(
            bytes,
            PacketDirection.ClientToServer,
            PacketPhase.Auth,
            out LegacyFrame frame);

        Assert.AreEqual(LegacyFrameDecodeStatus.Done, status);
        Assert.AreEqual(PacketId.LoginRequest, frame.Registration.Id);
        Assert.AreEqual(64, frame.Payload.Length);
        Assert.AreEqual((byte)0x42, frame.Sequence);
        Assert.AreEqual(66, frame.FrameSize);
    }

    [TestMethod]
    public void Decode_TruncatedLoginRequest_ReturnsNeedMoreData()
    {
        byte[] bytes = new byte[65];
        bytes[0] = 0x6F;

        LegacyFrameDecodeStatus status = LegacyFrameCodec.TryDecode(
            bytes,
            PacketDirection.ClientToServer,
            PacketPhase.Auth,
            out LegacyFrame frame);

        Assert.AreEqual(LegacyFrameDecodeStatus.NeedMoreData, status);
        Assert.AreEqual(0, frame.FrameSize);
    }

    [TestMethod]
    public void Decode_KnownHeaderInWrongPhase_ReturnsUnknownPacket()
    {
        byte[] bytes = new byte[66];
        bytes[0] = 0x6F;

        LegacyFrameDecodeStatus status = LegacyFrameCodec.TryDecode(
            bytes,
            PacketDirection.ClientToServer,
            PacketPhase.Game,
            out _);

        Assert.AreEqual(LegacyFrameDecodeStatus.UnknownPacket, status);
    }

    [TestMethod]
    public void Decode_UnknownHeader_ReturnsUnknownPacket()
    {
        byte[] bytes = { 0xEE };

        LegacyFrameDecodeStatus status = LegacyFrameCodec.TryDecode(
            bytes,
            PacketDirection.ClientToServer,
            PacketPhase.Handshake,
            out _);

        Assert.AreEqual(LegacyFrameDecodeStatus.UnknownPacket, status);
    }

    [TestMethod]
    public void Encode_LoginRequest_WritesHeaderPayloadAndSequence()
    {
        bool found = PacketRegistry.TryGet(
            0x6F,
            PacketDirection.ClientToServer,
            PacketPhase.Auth,
            out PacketRegistration registration);
        Assert.IsTrue(found);

        byte[] payload = new byte[64];
        payload[0] = 0x41;
        payload[^1] = 0x99;
        byte[] destination = new byte[66];

        LegacyFrameEncodeStatus status = LegacyFrameCodec.TryEncode(
            registration,
            payload,
            0x5A,
            destination,
            out int written);

        Assert.AreEqual(LegacyFrameEncodeStatus.Done, status);
        Assert.AreEqual(66, written);
        Assert.AreEqual((byte)0x6F, destination[0]);
        Assert.AreEqual((byte)0x41, destination[1]);
        Assert.AreEqual((byte)0x99, destination[64]);
        Assert.AreEqual((byte)0x5A, destination[65]);
    }

    [TestMethod]
    public void Encode_InvalidPayloadLength_DoesNotModifyDestination()
    {
        bool found = PacketRegistry.TryGet(
            0x6F,
            PacketDirection.ClientToServer,
            PacketPhase.Auth,
            out PacketRegistration registration);
        Assert.IsTrue(found);

        byte[] payload = new byte[63];
        byte[] destination = Enumerable.Repeat((byte)0xCC, 66).ToArray();

        LegacyFrameEncodeStatus status = LegacyFrameCodec.TryEncode(
            registration,
            payload,
            0x5A,
            destination,
            out int written);

        Assert.AreEqual(LegacyFrameEncodeStatus.InvalidPayloadLength, status);
        Assert.AreEqual(0, written);
        Assert.IsTrue(destination.All(static value => value == 0xCC));
    }

    [TestMethod]
    public void Encode_InsufficientDestination_DoesNotPartiallyWrite()
    {
        bool found = PacketRegistry.TryGet(
            0xFF,
            PacketDirection.ServerToClient,
            PacketPhase.Handshake,
            out PacketRegistration registration);
        Assert.IsTrue(found);

        byte[] payload = new byte[12];
        byte[] destination = Enumerable.Repeat((byte)0xCC, 12).ToArray();

        LegacyFrameEncodeStatus status = LegacyFrameCodec.TryEncode(
            registration,
            payload,
            null,
            destination,
            out int written);

        Assert.AreEqual(LegacyFrameEncodeStatus.InsufficientDestination, status);
        Assert.AreEqual(0, written);
        Assert.IsTrue(destination.All(static value => value == 0xCC));
    }
}
