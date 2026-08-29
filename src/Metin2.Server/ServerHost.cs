using System.Net;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Listeners;
using Metin2.Protocol.Generated;

namespace Metin2.Server;

public static class ServerHost
{
    public static Task RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        Console.WriteLine("Metin2 Remake Server bootstrap ready.");
        return Task.CompletedTask;
    }

    public static IAcceptedSocketHandler CreateAuthHandshakeHandler(
        IServerTimeProvider? timeProvider = null,
        IHandshakeTokenSource? tokenSource = null) =>
        new LegacyHandshakeSocketHandler(
            timeProvider ?? new StopwatchServerTimeProvider(),
            tokenSource ?? new RandomHandshakeTokenSource(),
            PacketPhase.Auth);

    public static IAcceptedSocketHandler CreateGameHandshakeHandler(
        IServerTimeProvider? timeProvider = null,
        IHandshakeTokenSource? tokenSource = null) =>
        new LegacyHandshakeSocketHandler(
            timeProvider ?? new StopwatchServerTimeProvider(),
            tokenSource ?? new RandomHandshakeTokenSource(),
            PacketPhase.Login);

    public static Task RunAuthHandshakeTransportAsync(
        IPEndPoint bindEndPoint,
        CancellationToken cancellationToken = default) =>
        RunTransportAsync(CreateAuthHandshakeHandler(), bindEndPoint, cancellationToken);

    public static Task RunGameHandshakeTransportAsync(
        IPEndPoint bindEndPoint,
        CancellationToken cancellationToken = default) =>
        RunTransportAsync(CreateGameHandshakeHandler(), bindEndPoint, cancellationToken);

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
