using System.IO.Pipelines;
using System.Net.Sockets;
using Metin2.Infrastructure.Networking.Receive;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Infrastructure.Networking.Transport;
using Metin2.Protocol.Generated;

namespace Metin2.Infrastructure.Networking.Connections;

public sealed class SocketConnection : IAsyncDisposable
{
    private readonly Socket _socket;
    private readonly Pipe _input = new();
    private readonly Pipe _output = new();
    private bool _outputCompleted;
    private bool _disposed;

    public SocketConnection(Socket socket, GameSession? session = null)
    {
        ArgumentNullException.ThrowIfNull(socket);
        _socket = socket;
        Session = session ?? new GameSession();
    }

    public GameSession Session { get; }

    public PipeWriter Output => _output.Writer;

    public async ValueTask<LegacyReceiveLoopResult> RunReceiveAsync(
        PacketDirection inboundDirection,
        ILegacyFrameConsumer consumer,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(consumer);

        using var transportCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<long> receivePump = SocketReceivePump.RunAsync(
            _socket,
            _input.Writer,
            Session,
            transportCancellation.Token).AsTask();

        try
        {
            LegacyReceiveLoopResult result = await LegacyPipeReceiveLoop.RunAsync(
                _input.Reader,
                Session,
                inboundDirection,
                consumer,
                cancellationToken).ConfigureAwait(false);

            transportCancellation.Cancel();
            await ObservePumpAfterInternalCancellationAsync(receivePump, transportCancellation.Token).ConfigureAwait(false);
            return result;
        }
        catch
        {
            transportCancellation.Cancel();
            await ObservePumpAfterInternalCancellationAsync(receivePump, transportCancellation.Token).ConfigureAwait(false);
            throw;
        }
    }

    public ValueTask<long> RunSendAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return SocketSendPump.RunAsync(_socket, _output.Reader, cancellationToken);
    }

    public async ValueTask CompleteOutputAsync(Exception? exception = null)
    {
        if (_outputCompleted)
        {
            return;
        }

        _outputCompleted = true;
        await _output.Writer.CompleteAsync(exception).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await CompleteOutputAsync().ConfigureAwait(false);

        try
        {
            if (_socket.Connected)
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        _socket.Dispose();
    }

    private static async Task ObservePumpAfterInternalCancellationAsync(
        Task receivePump,
        CancellationToken internalCancellationToken)
    {
        try
        {
            await receivePump.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (internalCancellationToken.IsCancellationRequested)
        {
        }
    }
}
