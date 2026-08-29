using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Metin2.Infrastructure.Networking.Connections;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Receive;
using Metin2.Protocol.Generated;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class LegacyHandshakePhaseAnnouncementTests
{
    [TestMethod]
    public async Task Game_handshake_announces_login_phase_after_success()
    {
        (Socket peer, Socket accepted) = await CreateConnectedPairAsync();
        using (peer)
        await using (var connection = new SocketConnection(accepted))
        {
            var target = new LegacyHandshakeDispatchTarget(
                connection.Session,
                connection.Output,
                new SequenceTimeProvider(5_000, 5_010),
                new FixedTokenSource(0x12345678),
                PacketPhase.Login);
            var consumer = new TypedPacketFrameConsumer(target);

            ValueTask<long> send = connection.RunSendAsync();
            ValueTask<LegacyReceiveLoopResult> receive = connection.RunReceiveAsync(
                PacketDirection.ClientToServer,
                consumer);

            await target.StartAsync();
            byte[] initial = await ReceiveExactAsync(peer, 15);
            Assert.AreEqual((byte)0xFD, initial[0]);
            Assert.AreEqual((byte)0x01, initial[1]);

            await SendAllAsync(peer, initial.AsMemory(2, 13));

            byte[] loginPhase = await ReceiveExactAsync(peer, 2);
            Assert.AreEqual((byte)0xFD, loginPhase[0]);
            Assert.AreEqual((byte)0x02, loginPhase[1]);
            Assert.AreEqual(PacketPhase.Login, connection.Session.Phase);

            peer.Shutdown(SocketShutdown.Send);
            _ = await receive;
            await connection.CompleteOutputAsync();
            _ = await send;
        }
    }

    [TestMethod]
    public async Task Retry_emits_only_handshake_frame_without_phase_change()
    {
        (Socket peer, Socket accepted) = await CreateConnectedPairAsync();
        using (peer)
        await using (var connection = new SocketConnection(accepted))
        {
            var target = new LegacyHandshakeDispatchTarget(
                connection.Session,
                connection.Output,
                new SequenceTimeProvider(1_000, 1_300),
                new FixedTokenSource(0xCAFEBABE),
                PacketPhase.Auth);
            var consumer = new TypedPacketFrameConsumer(target);

            ValueTask<long> send = connection.RunSendAsync();
            ValueTask<LegacyReceiveLoopResult> receive = connection.RunReceiveAsync(
                PacketDirection.ClientToServer,
                consumer);

            await target.StartAsync();
            byte[] initial = await ReceiveExactAsync(peer, 15);

            byte[] echo = initial.AsSpan(2, 13).ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(echo.AsSpan(5, 4), 800u);
            await SendAllAsync(peer, echo);

            byte[] retry = await ReceiveExactAsync(peer, 13);
            Assert.AreEqual((byte)0xFF, retry[0]);
            Assert.AreEqual(0xCAFEBABEu, BinaryPrimitives.ReadUInt32LittleEndian(retry.AsSpan(1, 4)));
            Assert.AreEqual(1_300u, BinaryPrimitives.ReadUInt32LittleEndian(retry.AsSpan(5, 4)));
            Assert.AreEqual(250u, BinaryPrimitives.ReadUInt32LittleEndian(retry.AsSpan(9, 4)));
            Assert.AreEqual(PacketPhase.Handshake, connection.Session.Phase);

            peer.Shutdown(SocketShutdown.Send);
            _ = await receive;
            await connection.CompleteOutputAsync();
            _ = await send;
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

    private static async Task<byte[]> ReceiveExactAsync(Socket socket, int length)
    {
        var buffer = new byte[length];
        int offset = 0;
        while (offset < buffer.Length)
        {
            int received = await socket.ReceiveAsync(buffer.AsMemory(offset));
            Assert.IsTrue(received > 0);
            offset += received;
        }

        return buffer;
    }

    private static async ValueTask SendAllAsync(Socket socket, ReadOnlyMemory<byte> data)
    {
        while (!data.IsEmpty)
        {
            int sent = await socket.SendAsync(data);
            Assert.IsTrue(sent > 0);
            data = data.Slice(sent);
        }
    }

    private sealed class SequenceTimeProvider(params long[] values) : IServerTimeProvider
    {
        private readonly Queue<long> _values = new(values);
        public long GetMilliseconds() => _values.Dequeue();
    }

    private sealed class FixedTokenSource(uint token) : IHandshakeTokenSource
    {
        public uint NextToken() => token;
    }
}
