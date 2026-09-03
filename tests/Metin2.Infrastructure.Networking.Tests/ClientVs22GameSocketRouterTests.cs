using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Metin2.Infrastructure.Networking.Game;
using Metin2.Infrastructure.Networking.Listeners;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class ClientVs22GameSocketRouterTests
{
    [TestMethod]
    public async Task State_checker_request_receives_normal_status_without_entering_game_handler()
    {
        using var cancellation = new CancellationTokenSource();
        var gameHandler = new RecordingHandler();
        var router = new ClientVs22GameSocketRouter(gameHandler, 13000);
        await using var listener = new TcpGameListener(new IPEndPoint(IPAddress.Loopback, 0));
        Task listenerTask = listener.RunAsync(router, cancellation.Token);

        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await client.ConnectAsync((IPEndPoint)listener.LocalEndPoint!);
        await client.SendAsync(new byte[] { ClientVs22GameSocketRouter.StateCheckerRequestHeader });

        byte[] response = await ReceiveExactAsync(client, 8);

        Assert.AreEqual(ClientVs22GameSocketRouter.ChannelStatusResponseHeader, response[0]);
        Assert.AreEqual(1, BinaryPrimitives.ReadInt32LittleEndian(response.AsSpan(1, sizeof(int))));
        Assert.AreEqual(13000, BinaryPrimitives.ReadInt16LittleEndian(response.AsSpan(5, sizeof(short))));
        Assert.AreEqual(ClientVs22GameSocketRouter.NormalChannelStatus, response[7]);
        Assert.AreEqual(0, gameHandler.CallCount);

        cancellation.Cancel();
        await listenerTask;
    }

    [TestMethod]
    public async Task Silent_login_connection_is_delegated_after_probe_window()
    {
        using var cancellation = new CancellationTokenSource();
        var gameHandler = new RecordingHandler();
        var router = new ClientVs22GameSocketRouter(
            gameHandler,
            13000,
            TimeSpan.FromMilliseconds(20));
        await using var listener = new TcpGameListener(new IPEndPoint(IPAddress.Loopback, 0));
        Task listenerTask = listener.RunAsync(router, cancellation.Token);

        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await client.ConnectAsync((IPEndPoint)listener.LocalEndPoint!);
        await gameHandler.Called.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(1, gameHandler.CallCount);

        cancellation.Cancel();
        await listenerTask;
    }

    private static async Task<byte[]> ReceiveExactAsync(Socket socket, int length)
    {
        var result = new byte[length];
        int offset = 0;
        while (offset < result.Length)
        {
            int received = await socket.ReceiveAsync(result.AsMemory(offset));
            Assert.IsTrue(received > 0);
            offset += received;
        }

        return result;
    }

    private sealed class RecordingHandler : IAcceptedSocketHandler
    {
        public int CallCount { get; private set; }
        public TaskCompletionSource Called { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask HandleAsync(Socket socket, CancellationToken cancellationToken)
        {
            CallCount++;
            Called.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }
}
