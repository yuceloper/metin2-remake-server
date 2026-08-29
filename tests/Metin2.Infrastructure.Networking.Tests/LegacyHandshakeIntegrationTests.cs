using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Metin2.Infrastructure.Networking.Connections;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Receive;
using Metin2.Protocol.Generated;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class LegacyHandshakeIntegrationTests
{
    [TestMethod]
    public async Task Initial_server_handshake_and_matching_client_echo_transition_session()
    {
        (Socket peer, Socket accepted) = await CreateConnectedPairAsync();
        using (peer)
        await using (var connection = new SocketConnection(accepted))
        {
            var clock = new SequenceTimeProvider(1_000, 1_010);
            var tokens = new FixedTokenSource(0x11223344);
            var target = new LegacyHandshakeDispatchTarget(
                connection.Session,
                connection.Output,
                clock,
                tokens,
                PacketPhase.Auth);
            var consumer = new TypedPacketFrameConsumer(target);

            ValueTask<long> send = connection.RunSendAsync();
            ValueTask<LegacyReceiveLoopResult> receive = connection.RunReceiveAsync(
                PacketDirection.ClientToServer,
                consumer);

            await target.StartAsync();

            byte[] initial = new byte[13];
            await ReceiveExactlyAsync(peer, initial);

            Assert.AreEqual((byte)0xFF, initial[0]);
            Assert.AreEqual(0x11223344u, BinaryPrimitives.ReadUInt32LittleEndian(initial.AsSpan(1, 4)));
            Assert.AreEqual(1_000u, BinaryPrimitives.ReadUInt32LittleEndian(initial.AsSpan(5, 4)));
            Assert.AreEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(initial.AsSpan(9, 4)));

            await SendAllAsync(peer, initial);
            peer.Shutdown(SocketShutdown.Send);

            LegacyReceiveLoopResult receiveResult = await receive;
            Assert.AreEqual(LegacyReceiveLoopCompletion.Completed, receiveResult.Completion);
            Assert.AreEqual(1L, receiveResult.FramesProcessed);
            Assert.AreEqual(PacketPhase.Auth, connection.Session.Phase);
            Assert.IsTrue(target.IsCompleted);

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

    private static async ValueTask ReceiveExactlyAsync(Socket socket, Memory<byte> destination)
    {
        int received = 0;
        while (received < destination.Length)
        {
            int count = await socket.ReceiveAsync(destination.Slice(received));
            Assert.IsTrue(count > 0);
            received += count;
        }
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
