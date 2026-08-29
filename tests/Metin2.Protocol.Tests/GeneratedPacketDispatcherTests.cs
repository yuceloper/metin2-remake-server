using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;

namespace Metin2.Protocol.Tests;

[TestClass]
public sealed class GeneratedPacketDispatcherTests
{
    [TestMethod]
    public async Task Handshake_dispatches_to_typed_target_from_segmented_payload()
    {
        Assert.IsTrue(PacketRegistry.TryGet(0xFF, PacketDirection.ClientToServer, PacketPhase.Handshake, out PacketRegistration registration));

        byte[] payload = new byte[HandshakeCodec.PayloadSize];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), 0x11223344);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4, 4), 0x55667788);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8, 4), 0x99AABBCC);

        ReadOnlySequence<byte> sequence = CreateSequence(payload.AsMemory(0, 5), payload.AsMemory(5));
        var target = new RecordingTarget();

        PacketDispatchAttempt attempt = PacketDispatcher.Dispatch(registration, sequence, target);

        Assert.AreEqual(PacketDispatchStatus.Done, attempt.Status);
        await attempt.HandlerCompletion;
        Assert.AreEqual(nameof(Handshake), target.LastPacketName);
        Assert.AreEqual(0x11223344u, target.HandshakePacket.HandshakeValue);
        Assert.AreEqual(0x55667788u, target.HandshakePacket.Time);
        Assert.AreEqual(0x99AABBCCu, target.HandshakePacket.Delta);
    }

    [TestMethod]
    public async Task LoginRequest_dispatches_to_typed_target()
    {
        Assert.IsTrue(PacketRegistry.TryGet(0x6F, PacketDirection.ClientToServer, PacketPhase.Auth, out PacketRegistration registration));

        byte[] payload = new byte[LoginRequestCodec.PayloadSize];
        WriteFixedAscii(payload.AsSpan(0, 31), "yuceloper");
        WriteFixedAscii(payload.AsSpan(31, 17), "secret");
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(48, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(52, 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(56, 4), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(60, 4), 4);

        var target = new RecordingTarget();
        PacketDispatchAttempt attempt = PacketDispatcher.Dispatch(registration, new ReadOnlySequence<byte>(payload), target);

        Assert.AreEqual(PacketDispatchStatus.Done, attempt.Status);
        await attempt.HandlerCompletion;
        Assert.AreEqual(nameof(LoginRequest), target.LastPacketName);
        Assert.AreEqual("yuceloper", target.LoginRequestPacket.Username);
        Assert.AreEqual("secret", target.LoginRequestPacket.Password);
        CollectionAssert.AreEqual(new uint[] { 1, 2, 3, 4 }, target.LoginRequestPacket.EncryptKey.ToArray());
    }

    [TestMethod]
    public async Task TokenLogin_dispatches_to_typed_target()
    {
        Assert.IsTrue(PacketRegistry.TryGet(0x6D, PacketDirection.ClientToServer, PacketPhase.Login, out PacketRegistration registration));

        byte[] payload = new byte[TokenLoginCodec.PayloadSize];
        WriteFixedAscii(payload.AsSpan(0, 31), "player");
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(31, 4), 0xCAFEBABE);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(35, 4), 10);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(39, 4), 20);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(43, 4), 30);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(47, 4), 40);

        var target = new RecordingTarget();
        PacketDispatchAttempt attempt = PacketDispatcher.Dispatch(registration, new ReadOnlySequence<byte>(payload), target);

        Assert.AreEqual(PacketDispatchStatus.Done, attempt.Status);
        await attempt.HandlerCompletion;
        Assert.AreEqual(nameof(TokenLogin), target.LastPacketName);
        Assert.AreEqual("player", target.TokenLoginPacket.Username);
        Assert.AreEqual(0xCAFEBABEu, target.TokenLoginPacket.Key);
        CollectionAssert.AreEqual(new uint[] { 10, 20, 30, 40 }, target.TokenLoginPacket.XteaKey.ToArray());
    }

    [TestMethod]
    public void Wrong_payload_size_does_not_invoke_target()
    {
        Assert.IsTrue(PacketRegistry.TryGet(0xFF, PacketDirection.ClientToServer, PacketPhase.Handshake, out PacketRegistration registration));

        var target = new RecordingTarget();
        var payload = new ReadOnlySequence<byte>(new byte[HandshakeCodec.PayloadSize - 1]);

        PacketDispatchAttempt attempt = PacketDispatcher.Dispatch(registration, payload, target);

        Assert.AreEqual(PacketDispatchStatus.MalformedPayload, attempt.Status);
        Assert.IsNull(target.LastPacketName);
    }

    private static void WriteFixedAscii(Span<byte> destination, string value)
    {
        destination.Clear();
        int written = Encoding.ASCII.GetBytes(value, destination);
        Assert.IsTrue(written < destination.Length);
    }

    private static ReadOnlySequence<byte> CreateSequence(ReadOnlyMemory<byte> first, ReadOnlyMemory<byte> second)
    {
        var firstSegment = new SequenceSegment(first);
        SequenceSegment lastSegment = firstSegment.Append(second);
        return new ReadOnlySequence<byte>(firstSegment, 0, lastSegment, lastSegment.Memory.Length);
    }

    private sealed class SequenceSegment : ReadOnlySequenceSegment<byte>
    {
        public SequenceSegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        public SequenceSegment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new SequenceSegment(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };
            Next = next;
            return next;
        }
    }

    private sealed class RecordingTarget : IPacketDispatchTarget
    {
        public string? LastPacketName { get; private set; }
        public Handshake HandshakePacket { get; private set; }
        public LoginRequest LoginRequestPacket { get; private set; }
        public TokenLogin TokenLoginPacket { get; private set; }

        public ValueTask HandleAsync(Handshake packet, CancellationToken cancellationToken)
        {
            LastPacketName = nameof(Handshake);
            HandshakePacket = packet;
            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(LoginFailed packet, CancellationToken cancellationToken)
        {
            LastPacketName = nameof(LoginFailed);
            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(LoginRequest packet, CancellationToken cancellationToken)
        {
            LastPacketName = nameof(LoginRequest);
            LoginRequestPacket = packet;
            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(LoginSuccess packet, CancellationToken cancellationToken)
        {
            LastPacketName = nameof(LoginSuccess);
            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(Phase packet, CancellationToken cancellationToken)
        {
            LastPacketName = nameof(Phase);
            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(TokenLogin packet, CancellationToken cancellationToken)
        {
            LastPacketName = nameof(TokenLogin);
            TokenLoginPacket = packet;
            return ValueTask.CompletedTask;
        }
    }
}
