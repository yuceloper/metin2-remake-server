using System.Net;

namespace Metin2.Server;

internal enum ServerRunMode : byte
{
    Auth = 1,
    Game = 2
}

internal readonly record struct ServerRunOptions(
    ServerRunMode Mode,
    IPAddress BindAddress,
    int Port);

internal readonly record struct ServerCommandLineResult(
    ServerRunOptions? Options,
    bool ShowHelp,
    string? Error)
{
    public bool IsValid => Error is null;
}

internal static class ServerCommandLine
{
    public static ServerCommandLineResult Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0 || IsHelp(args[0]))
        {
            return new ServerCommandLineResult(null, ShowHelp: true, Error: null);
        }

        if (!string.Equals(args[0], "serve", StringComparison.OrdinalIgnoreCase))
        {
            return Error($"Unknown command '{args[0]}'.");
        }

        string? modeValue = null;
        string bindValue = "127.0.0.1";
        string? portValue = null;

        for (int index = 1; index < args.Length; index++)
        {
            string option = args[index];
            if (IsHelp(option))
            {
                return new ServerCommandLineResult(null, ShowHelp: true, Error: null);
            }

            if (index + 1 >= args.Length)
            {
                return Error($"Missing value for option '{option}'.");
            }

            string value = args[++index];
            switch (option)
            {
                case "--mode":
                    modeValue = value;
                    break;
                case "--bind":
                    bindValue = value;
                    break;
                case "--port":
                    portValue = value;
                    break;
                default:
                    return Error($"Unknown option '{option}'.");
            }
        }

        if (!TryParseMode(modeValue, out ServerRunMode mode))
        {
            return Error("--mode is required and must be either 'auth' or 'game'.");
        }

        if (!IPAddress.TryParse(bindValue, out IPAddress? bindAddress))
        {
            return Error($"Invalid bind address '{bindValue}'. Use an IP address such as 127.0.0.1 or 0.0.0.0.");
        }

        if (!int.TryParse(portValue, out int port) || port is < 1 or > 65535)
        {
            return Error("--port is required and must be between 1 and 65535.");
        }

        return new ServerCommandLineResult(
            new ServerRunOptions(mode, bindAddress, port),
            ShowHelp: false,
            Error: null);
    }

    public static string Usage =>
        "Usage:\n" +
        "  dotnet run --project src/Metin2.Server -- serve --mode auth --port <port> [--bind 127.0.0.1]\n" +
        "  dotnet run --project src/Metin2.Server -- serve --mode game --port <port> [--bind 127.0.0.1]\n\n" +
        "No canonical Metin2 port is assumed; --port is always explicit.";

    private static bool TryParseMode(string? value, out ServerRunMode mode)
    {
        if (string.Equals(value, "auth", StringComparison.OrdinalIgnoreCase))
        {
            mode = ServerRunMode.Auth;
            return true;
        }

        if (string.Equals(value, "game", StringComparison.OrdinalIgnoreCase))
        {
            mode = ServerRunMode.Game;
            return true;
        }

        mode = default;
        return false;
    }

    private static bool IsHelp(string value) =>
        value is "--help" or "-h" || string.Equals(value, "help", StringComparison.OrdinalIgnoreCase);

    private static ServerCommandLineResult Error(string message) =>
        new(null, ShowHelp: true, Error: message);
}
