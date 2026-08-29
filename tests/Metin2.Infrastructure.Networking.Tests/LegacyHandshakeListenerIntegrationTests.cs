using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Listeners;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Protocol.Generated;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class LegacyHandshakeListenerIntegrationTests
{
    [TestMethod]
    public async Task Listener_sends_handshake_phase_then_handshake_and_announces_auth_on_completion()
    {
        using var cancellation = new CancellationTokenSource();
        var completed = new TaskCompletionSource<PacketPhase>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new LegacyHandshakeSocketHandler(
            new SequenceTimeProvider(1_000, 1_020),
            new FixedTokenSource(0x11223344),
            PacketPhase.Auth,
            (session, _) =>
            {
                completed.TrySetResult(session.Phase);
                return ValueTask.CompletedTask;
            });

        await using var listener = new TcpGameListener(new IPEndPoint(IPAddress.Loopback, 0));
        Task listenerTask = listener.RunAsync(handler, cancellation.Token);
        var endpoint = (IPEndPoint)listener.LocalEndPoint!;

        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await client.ConnectAsync(endpoint);

        byte[] initial = await ReceiveExactAsync(client, 15);
        Assert.AreEqual((byte)0xFD, initial[0]);
        Assert.AreEqual((byte)0x01, initial[1]);
        Assert.AreEqual((byte)0xFF, initial[2]);
        Assert.AreEqual(0x11223344u, BinaryPrimitives.ReadUInt32LittleEndian(initial.AsSpan(3, 4)));
        Assert.AreEqual(1_000u, BinaryPrimitives.ReadUInt32LittleEndian(initial.AsSpan(7, 4)));
        Assert.AreEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(initial.AsSpan(11, 4)));

        await SendAllAsync(client, initial.AsMemory(2, 13));

        byte[] authPhase = await ReceiveExactAsync(client, 2);
        Assert.AreEqual((byte)0xFD, authPhase[0]);
        Assert.AreEqual((byte)0x0A, authPhase[1]);

        PacketPhase phase = await completed.Task;
        Assert.AreEqual(PacketPhase.Auth, phase);

        client.Shutdown(SocketShutdown.Send);
        cancellation.Cancel();
        await listenerTask;
    }

    [TestMethod]
    public async Task Wrong_token_closes_only_that_connection_and_listener_accepts_next_client()
    {
        using var cancellation = new CancellationTokenSource();
        var completed = new TaskCompletionSource<PacketPhase>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new LegacyHandshakeSocketHandler(
            new ConstantTimeProvider(2_000),
            new FixedTokenSource(0xAABBCCDD),
            PacketPhase.Auth,
            (session, _) =>
            {
                completed.TrySetResult(session.Phase);
                return ValueTask.CompletedTask;
            });

        await using var listener = new TcpGameListener(new IPEndPoint(IPAddress.Loopback, 0));
        Task listenerTask = listener.RunAsync(handler, cancellation.Token);
        var endpoint = (IPEndPoint)listener.LocalEndPoint!;

        using (var first = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
        {
            await first.ConnectAsync(endpoint);
            byte[] initial = await ReceiveExactAsync(first, 15);
            BinaryPrimitives.WriteUInt32LittleEndian(initial.AsSpan(3, 4), 0xDEADBEEF);
            await SendAllAsync(first, initial.AsMemory(2, 13));

            byte[] one = new byte[1];
            int received = await first.ReceiveAsync(one);
            Assert.AreEqual(0, received);
        }

        using (var second = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
        {
            await second.ConnectAsync(endpoint);
            byte[] initial = await ReceiveExactAsync(second, 15);
            await SendAllAsync(second, initial.AsMemory(2, 13));

            byte[] authPhase = await ReceiveExactAsync(second, 2);
            Assert.AreEqual((byte)0xFD, authPhase[0]);
            Assert.AreEqual((byte)0x0A, authPhase[1]);

            PacketPhase phase = await completed.Task;
            Assert.AreEqual(PacketPhase.Auth, phase);
            second.Shutdown(SocketShutdown.Send);
        }

        cancellation.Cancel();
        await listenerTask;
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
        ReadOnlyMemory<byte> remaining = data;
        while (!remaining.IsEmpty)
        {
            int sent = await socket.SendAsync(remaining);
            Assert.IsTrue(sent > 0);
            remaining = remaining.Slice(sent);
        }
    }

    private sealed class FixedTokenSource(uint token) : IHandshakeTokenSource
    {
        public uint NextToken() => token;
    }

    private sealed class ConstantTimeProvider(long value) : IServerTimeProvider
    {
        public long GetMilliseconds() => value;
    }

    private sealed class SequenceTimeProvider(params long[] values) : IServerTimeProvider
    {
        private readonly Queue<long> _values = new(values);

        public long GetMilliseconds()
        {
            lock (_values)
            {
                return _values.Dequeue();
            }
        }
    }
}
