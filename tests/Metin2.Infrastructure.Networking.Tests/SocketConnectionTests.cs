using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Metin2.Infrastructure.Networking.Connections;
using Metin2.Infrastructure.Networking.Receive;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using HandshakePacketModel = Metin2.Protocol.Generated.Packets.Handshake;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class SocketConnectionTests
{
    [TestMethod]
    public async Task Loopback_socket_bytes_reach_typed_handshake_handler()
    {
        (Socket peer, Socket accepted) = await CreateConnectedPairAsync();
        using (peer)
        await using (var connection = new SocketConnection(accepted))
        {
            var target = new RecordingTarget();
            var consumer = new TypedPacketFrameConsumer(target);

            ValueTask<LegacyReceiveLoopResult> receive = connection.RunReceiveAsync(
                PacketDirection.ClientToServer,
                consumer);

            byte[] frame = CreateHandshakeFrame(11, 22, 33);
            await SendAllAsync(peer, frame.AsMemory(0, 3));
            await SendAllAsync(peer, frame.AsMemory(3));
            peer.Shutdown(SocketShutdown.Send);

            LegacyReceiveLoopResult result = await receive;

            Assert.AreEqual(LegacyReceiveLoopCompletion.Completed, result.Completion);
            Assert.AreEqual(1L, result.FramesProcessed);
            Assert.AreEqual(nameof(HandshakePacketModel), target.LastPacketName);
            Assert.AreEqual(11u, target.HandshakePacket.HandshakeValue);
            Assert.AreEqual(22u, target.HandshakePacket.Time);
            Assert.AreEqual(33u, target.HandshakePacket.Delta);
        }
    }

    [TestMethod]
    public async Task Outbound_pipe_bytes_reach_peer_exactly()
    {
        (Socket peer, Socket accepted) = await CreateConnectedPairAsync();
        using (peer)
        await using (var connection = new SocketConnection(accepted))
        {
            byte[] expected = Enumerable.Range(0, 8192).Select(static value => (byte)(value % 251)).ToArray();

            ValueTask<long> send = connection.RunSendAsync();
            await connection.Output.WriteAsync(expected);
            await connection.CompleteOutputAsync();

            byte[] actual = new byte[expected.Length];
            int received = 0;
            while (received < actual.Length)
            {
                int count = await peer.ReceiveAsync(actual.AsMemory(received));
                Assert.IsTrue(count > 0);
                received += count;
            }

            long sent = await send;

            Assert.AreEqual((long)expected.Length, sent);
            CollectionAssert.AreEqual(expected, actual);
        }
    }

    [TestMethod]
    public async Task Peer_send_shutdown_completes_empty_receive_path_cleanly()
    {
        (Socket peer, Socket accepted) = await CreateConnectedPairAsync();
        using (peer)
        await using (var connection = new SocketConnection(accepted))
        {
            var target = new RecordingTarget();
            var consumer = new TypedPacketFrameConsumer(target);

            ValueTask<LegacyReceiveLoopResult> receive = connection.RunReceiveAsync(
                PacketDirection.ClientToServer,
                consumer);

            peer.Shutdown(SocketShutdown.Send);
            LegacyReceiveLoopResult result = await receive;

            Assert.AreEqual(LegacyReceiveLoopCompletion.Completed, result.Completion);
            Assert.AreEqual(0L, result.FramesProcessed);
            Assert.IsNull(target.LastPacketName);
        }
    }

    private static async Task<(Socket Peer, Socket Accepted)> CreateConnectedPairAsync()
    {
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);

        var peer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Task<Socket> acceptTask = listener.AcceptAsync();
        await peer.ConnectAsync(listener.LocalEndPoint!);
        Socket accepted = await acceptTask;
        return (peer, accepted);
    }

    private static async ValueTask SendAllAsync(Socket socket, ReadOnlyMemory<byte> data)
    {
        ReadOnlyMemory<byte> remaining = data;
        while (!remaining.IsEmpty)
        {
            int sent = await socket.SendAsync(remaining);
            Assert.IsTrue(sent > 0);
            remaining = remaining.Slice(sent);
        }
    }

    private static byte[] CreateHandshakeFrame(uint handshake, uint time, uint delta)
    {
        var frame = new byte[1 + HandshakeCodec.PayloadSize];
        frame[0] = 0xFF;
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(1, 4), handshake);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(5, 4), time);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(9, 4), delta);
        return frame;
    }

    private sealed class RecordingTarget : IPacketDispatchTarget
    {
        public string? LastPacketName { get; private set; }
        public HandshakePacketModel HandshakePacket { get; private set; }

        public ValueTask HandleAsync(HandshakePacketModel packet, CancellationToken cancellationToken)
        {
            LastPacketName = nameof(HandshakePacketModel);
            HandshakePacket = packet;
            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(LoginFailed packet, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask HandleAsync(LoginRequest packet, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask HandleAsync(LoginSuccess packet, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask HandleAsync(Phase packet, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask HandleAsync(TokenLogin packet, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
