using System.IO.Pipelines;
using System.Net.Sockets;
using Metin2.Infrastructure.Networking.Security;
using Metin2.Infrastructure.Networking.Sessions;

namespace Metin2.Infrastructure.Networking.Transport;

public static class SocketReceivePump
{
    private const int MinimumReadBufferSize = 4096;

    public static async ValueTask<long> RunAsync(
        Socket socket,
        PipeWriter destination,
        GameSession? session = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(destination);

        long totalBytes = 0;
        Exception? completionException = null;

        try
        {
            while (true)
            {
                Memory<byte> memory = destination.GetMemory(MinimumReadBufferSize);
                int received = await socket.ReceiveAsync(
                    memory,
                    SocketFlags.None,
                    cancellationToken).ConfigureAwait(false);

                if (received == 0)
                {
                    break;
                }

                ImprovedPacketSecuritySession? improved = session?.ImprovedSecuritySession;
                if (improved is { IsActive: true })
                {
                    improved.DecryptInbound(memory.Span[..received]);
                }

                destination.Advance(received);
                totalBytes += received;

                FlushResult flushResult = await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                if (flushResult.IsCanceled)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    break;
                }

                if (flushResult.IsCompleted)
                {
                    break;
                }
            }

            return totalBytes;
        }
        catch (Exception exception)
        {
            completionException = exception;
            throw;
        }
        finally
        {
            await destination.CompleteAsync(completionException).ConfigureAwait(false);
        }
    }
}
