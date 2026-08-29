using System.Net;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Listeners;
using Metin2.Protocol.Generated;

namespace Metin2.Server;

public static class ServerHost
{
    public static async Task RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ServerCommandLineResult command = ServerCommandLine.Parse(args);
        if (!command.IsValid)
        {
            Console.Error.WriteLine(command.Error);
            Console.Error.WriteLine();
            Console.Error.WriteLine(ServerCommandLine.Usage);
            Environment.ExitCode = 2;
            return;
        }

        if (command.ShowHelp || command.Options is null)
        {
            Console.WriteLine("Metin2 Remake Server bootstrap ready.");
            Console.WriteLine();
            Console.WriteLine(ServerCommandLine.Usage);
            return;
        }

        ServerRunOptions options = command.Options.Value;
        var endpoint = new IPEndPoint(options.BindAddress, options.Port);
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;
        try
        {
            Console.WriteLine($"Starting {options.Mode} handshake server on {endpoint}...");
            Console.WriteLine("Press Ctrl+C to stop.");

            if (options.Mode == ServerRunMode.Auth)
            {
                await RunAuthHandshakeTransportAsync(endpoint, cancellation.Token).ConfigureAwait(false);
            }
            else
            {
                await RunGameHandshakeTransportAsync(endpoint, cancellation.Token).ConfigureAwait(false);
            }
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
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
