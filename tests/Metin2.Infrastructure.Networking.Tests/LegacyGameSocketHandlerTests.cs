using System.Net;
using System.Net.Sockets;
using Metin2.Infrastructure.Networking.Game;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Listeners;
using Metin2.Modules.Game.Application;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Protocol.Legacy;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class LegacyGameSocketHandlerTests
{
    [TestMethod]
    public async Task Handshake_then_sequenced_token_login_reaches_game_login_service()
    {
        using var cancellation = new CancellationTokenSource();
        var service = new RecordingGameLoginService();
        var handler = new LegacyGameSocketHandler(
            new ConstantTimeProvider(1_000),
            new FixedHandshakeTokenSource(0xAABBCCDD),
            new LegacySequenceProfile("test", new byte[] { 0xAA }),
            service);

        await using var listener = new TcpGameListener(new IPEndPoint(IPAddress.Loopback, 0));
        Task listenerTask = listener.RunAsync(handler, cancellation.Token);
        var endpoint = (IPEndPoint)listener.LocalEndPoint!;

        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await client.ConnectAsync(endpoint);

        byte[] initial = await ReceiveExactAsync(client, 15);
        Assert.AreEqual((byte)0xFD, initial[0]);
        Assert.AreEqual((byte)LegacyPhaseCode.Handshake, initial[1]);
        Assert.AreEqual((byte)0xFF, initial[2]);

        await SendAllAsync(client, initial.AsMemory(2, 13));

        byte[] loginPhase = await ReceiveExactAsync(client, 2);
        Assert.AreEqual((byte)0xFD, loginPhase[0]);
        Assert.AreEqual((byte)LegacyPhaseCode.Login, loginPhase[1]);

        var packet = new TokenLogin("player", 0x11223344, new uint[] { 10, 20, 30, 40 });
        var frame = new byte[53];
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, 0xAA, frame, out int written);
        Assert.AreEqual(PacketFrameWriteStatus.Done, status);
        Assert.AreEqual(frame.Length, written);

        await SendAllAsync(client, frame);
        GameLoginRequest request = await service.Request.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(0x11223344u, request.Token);
        Assert.AreEqual("player", request.Username);

        client.Shutdown(SocketShutdown.Send);
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
        while (!data.IsEmpty)
        {
            int sent = await socket.SendAsync(data);
            Assert.IsTrue(sent > 0);
            data = data.Slice(sent);
        }
    }

    private sealed class RecordingGameLoginService : IGameLoginService
    {
        public TaskCompletionSource<GameLoginRequest> Request { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<GameLoginResult> LoginAsync(
            GameLoginRequest request,
            CancellationToken cancellationToken = default)
        {
            Request.TrySetResult(request);
            return ValueTask.FromResult(GameLoginResult.Success(new AccountId(7), "player"));
        }
    }

    private sealed class FixedHandshakeTokenSource(uint token) : IHandshakeTokenSource
    {
        public uint NextToken() => token;
    }

    private sealed class ConstantTimeProvider(long value) : IServerTimeProvider
    {
        public long GetMilliseconds() => value;
    }
}
