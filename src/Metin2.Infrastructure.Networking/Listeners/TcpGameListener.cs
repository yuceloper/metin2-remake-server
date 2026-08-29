using System.Net;
using System.Net.Sockets;

namespace Metin2.Infrastructure.Networking.Listeners;

public sealed class TcpGameListener : IAsyncDisposable
{
    private readonly Socket _listener;
    private readonly IPEndPoint _bindEndPoint;
    private readonly int _backlog;
    private readonly HashSet<Task> _connections = [];
    private bool _started;
    private bool _disposed;

    public TcpGameListener(IPEndPoint bindEndPoint, int backlog = 512)
    {
        ArgumentNullException.ThrowIfNull(bindEndPoint);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(backlog);

        _bindEndPoint = bindEndPoint;
        _backlog = backlog;
        _listener = new Socket(bindEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
    }

    public EndPoint? LocalEndPoint => _listener.LocalEndPoint;

    public async Task RunAsync(
        IAcceptedSocketHandler handler,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(handler);

        if (_started)
        {
            throw new InvalidOperationException("The TCP listener has already been started.");
        }

        _started = true;
        _listener.Bind(_bindEndPoint);
        _listener.Listen(_backlog);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                PruneCompletedConnections();

                Socket accepted;
                try
                {
                    accepted = await _listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                Task connectionTask = HandleAcceptedAsync(accepted, handler, cancellationToken);
                _connections.Add(connectionTask);
            }
        }
        finally
        {
            _listener.Dispose();

            if (_connections.Count != 0)
            {
                await Task.WhenAll(_connections).ConfigureAwait(false);
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _listener.Dispose();
        return ValueTask.CompletedTask;
    }

    private static async Task HandleAcceptedAsync(
        Socket socket,
        IAcceptedSocketHandler handler,
        CancellationToken cancellationToken)
    {
        using (socket)
        {
            await handler.HandleAsync(socket, cancellationToken).ConfigureAwait(false);
        }
    }

    private void PruneCompletedConnections()
    {
        _connections.RemoveWhere(static task => task.IsCompletedSuccessfully);
    }
}
