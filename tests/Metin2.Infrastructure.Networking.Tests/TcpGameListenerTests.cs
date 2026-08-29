using System.Net;
using System.Net.Sockets;
using Metin2.Infrastructure.Networking.Listeners;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class TcpGameListenerTests
{
    [TestMethod]
    public async Task Listener_accepts_client_and_delivers_socket_to_handler()
    {
        using var cancellation = new CancellationTokenSource();
        await using var listener = new TcpGameListener(new IPEndPoint(IPAddress.Loopback, 0));
        var handler = new ReadingHandler();

        Task run = listener.RunAsync(handler, cancellation.Token);
        var endpoint = (IPEndPoint)listener.LocalEndPoint!;

        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await client.ConnectAsync(endpoint);
        await client.SendAsync(new byte[] { 0x5A });

        byte received = await handler.Received.Task;
        Assert.AreEqual((byte)0x5A, received);

        cancellation.Cancel();
        await run;
    }

    [TestMethod]
    public async Task Handler_failure_is_isolated_and_accepted_socket_is_disposed()
    {
        using var cancellation = new CancellationTokenSource();
        var observedFailure = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var listener = new TcpGameListener(
            new IPEndPoint(IPAddress.Loopback, 0),
            connectionErrorHandler: exception => observedFailure.TrySetResult(exception));
        var handler = new ThrowingHandler();

        Task run = listener.RunAsync(handler, cancellation.Token);
        var endpoint = (IPEndPoint)listener.LocalEndPoint!;

        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await client.ConnectAsync(endpoint);

        Exception failure = await observedFailure.Task;
        Assert.IsTrue(failure is InvalidOperationException);

        byte[] buffer = new byte[1];
        int received = await client.ReceiveAsync(buffer);
        Assert.AreEqual(0, received);

        cancellation.Cancel();
        await run;
    }

    private sealed class ReadingHandler : IAcceptedSocketHandler
    {
        public TaskCompletionSource<byte> Received { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask HandleAsync(Socket socket, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[1];
            int received = await socket.ReceiveAsync(buffer, cancellationToken);
            if (received == 1)
            {
                Received.TrySetResult(buffer[0]);
            }
        }
    }

    private sealed class ThrowingHandler : IAcceptedSocketHandler
    {
        public ValueTask HandleAsync(Socket socket, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("connection handler failed");
        }
    }
}
