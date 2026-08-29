using System.Net;
using Metin2.Infrastructure.Networking.Listeners;

namespace Metin2.Server;

public static class ServerHost
{
    public static Task RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        Console.WriteLine("Metin2 Remake Server bootstrap ready.");
        return Task.CompletedTask;
    }

    public static async Task RunTransportAsync(
        IAcceptedSocketHandler connectionHandler,
        IPEndPoint bindEndPoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionHandler);
        ArgumentNullException.ThrowIfNull(bindEndPoint);

        await using var listener = new TcpGameListener(bindEndPoint);
        await listener.RunAsync(connectionHandler, cancellationToken).ConfigureAwait(false);
    }
}
