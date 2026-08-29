using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace Metin2.Infrastructure.Networking.Transport;

public static class SocketSendPump
{
    public static async ValueTask<long> RunAsync(
        Socket socket,
        PipeReader source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(source);

        long totalBytes = 0;
        Exception? completionException = null;

        try
        {
            while (true)
            {
                ReadResult readResult = await source.ReadAsync(cancellationToken).ConfigureAwait(false);
                ReadOnlySequence<byte> buffer = readResult.Buffer;

                foreach (ReadOnlyMemory<byte> segment in buffer)
                {
                    ReadOnlyMemory<byte> remaining = segment;
                    while (!remaining.IsEmpty)
                    {
                        int sent = await socket.SendAsync(
                            remaining,
                            SocketFlags.None,
                            cancellationToken).ConfigureAwait(false);

                        if (sent <= 0)
                        {
                            throw new IOException("Connected socket returned zero bytes from SendAsync.");
                        }

                        totalBytes += sent;
                        remaining = remaining.Slice(sent);
                    }
                }

                source.AdvanceTo(buffer.End);

                if (readResult.IsCompleted)
                {
                    return totalBytes;
                }
            }
        }
        catch (Exception exception)
        {
            completionException = exception;
            throw;
        }
        finally
        {
            await source.CompleteAsync(completionException).ConfigureAwait(false);
        }
    }
}
