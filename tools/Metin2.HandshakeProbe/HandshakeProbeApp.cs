using System.Net;
using System.Net.Sockets;
using Metin2.Protocol.Generated.Packets;
using Metin2.Protocol.IO;
using Metin2.Protocol.Legacy;
using HandshakePacket = Metin2.Protocol.Generated.Packets.Handshake;

namespace Metin2.HandshakeProbe;

public enum HandshakeProbeExpectedPhase : byte
{
    Auth = 1,
    Login = 2
}

public readonly record struct HandshakeProbeOptions(
    string Host,
    int Port,
    HandshakeProbeExpectedPhase ExpectedPhase,
    int MaxRetries = 8,
    TimeSpan? Timeout = null);

public static class HandshakeProbeApp
{
    public static async Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (!TryParseArguments(args, out HandshakeProbeOptions options, out string? error, out bool showHelp))
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.Error.WriteLine(error);
                Console.Error.WriteLine();
            }

            Console.Error.WriteLine(Usage);
            return showHelp && error is null ? 0 : 2;
        }

        try
        {
            await RunProbeAsync(options).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine($"Handshake probe failed: {exception.Message}");
            return 1;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Handshake probe timed out or was cancelled.");
            return 1;
        }
    }

    public static async Task RunProbeAsync(
        HandshakeProbeOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Host);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Port, 65535);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxRetries);

        TimeSpan timeout = options.Timeout ?? TimeSpan.FromSeconds(5);
        using var timeoutCancellation = new CancellationTokenSource(timeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellation.Token);

        IPAddress[] addresses = await Dns.GetHostAddressesAsync(options.Host, linkedCancellation.Token)
            .ConfigureAwait(false);
        IPAddress address = addresses.FirstOrDefault(static value => value.AddressFamily == AddressFamily.InterNetwork)
            ?? addresses.First();

        using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(new IPEndPoint(address, options.Port), linkedCancellation.Token)
            .ConfigureAwait(false);

        byte[] initialPhase = await ReceiveExactAsync(socket, 2, linkedCancellation.Token).ConfigureAwait(false);
        Require(initialPhase[0] == 0xFD, $"Expected initial Phase header 0xFD but received 0x{initialPhase[0]:X2}.");
        Require(initialPhase[1] == (byte)LegacyPhaseCode.Handshake,
            $"Expected initial Handshake phase 0x01 but received 0x{initialPhase[1]:X2}.");
        Console.WriteLine("Initial phase: Handshake (FD 01)");

        byte[] frame = await ReceiveExactAsync(socket, 13, linkedCancellation.Token).ConfigureAwait(false);
        uint? expectedToken = null;

        for (int attempt = 1; attempt <= options.MaxRetries; attempt++)
        {
            Require(frame[0] == 0xFF, $"Expected Handshake header 0xFF but received 0x{frame[0]:X2}.");

            var reader = new PacketReader(frame.AsSpan(1));
            Require(HandshakeCodec.TryRead(ref reader, out HandshakePacket handshake), "Handshake payload could not be decoded.");
            Require(reader.Remaining == 0, "Handshake payload contained unexpected trailing bytes.");

            expectedToken ??= handshake.HandshakeValue;
            Require(handshake.HandshakeValue == expectedToken.Value, "Handshake token changed unexpectedly during retry flow.");

            Console.WriteLine(
                $"Handshake #{attempt}: token=0x{handshake.HandshakeValue:X8}, time={handshake.Time}, delta={handshake.Delta}");

            await SendAllAsync(socket, frame, linkedCancellation.Token).ConfigureAwait(false);

            byte[] header = await ReceiveExactAsync(socket, 1, linkedCancellation.Token).ConfigureAwait(false);
            if (header[0] == 0xFF)
            {
                byte[] retryPayload = await ReceiveExactAsync(socket, 12, linkedCancellation.Token).ConfigureAwait(false);
                frame = new byte[13];
                frame[0] = 0xFF;
                retryPayload.CopyTo(frame, 1);
                continue;
            }

            Require(header[0] == 0xFD, $"Expected Phase header 0xFD or retry Handshake 0xFF but received 0x{header[0]:X2}.");
            byte[] phasePayload = await ReceiveExactAsync(socket, 1, linkedCancellation.Token).ConfigureAwait(false);
            LegacyPhaseCode expected = options.ExpectedPhase == HandshakeProbeExpectedPhase.Auth
                ? LegacyPhaseCode.Auth
                : LegacyPhaseCode.Login;

            Require(phasePayload[0] == (byte)expected,
                $"Expected final phase {(byte)expected} ({expected}) but received 0x{phasePayload[0]:X2}.");

            Console.WriteLine($"Handshake complete. Final phase: {expected} (FD {phasePayload[0]:X2})");
            return;
        }

        throw new InvalidOperationException($"Handshake did not complete within {options.MaxRetries} attempts.");
    }

    public static string Usage =>
        "Usage:\n" +
        "  dotnet run --project tools/Metin2.HandshakeProbe -- --host 127.0.0.1 --port <port> --expect auth\n" +
        "  dotnet run --project tools/Metin2.HandshakeProbe -- --host 127.0.0.1 --port <port> --expect login\n";

    internal static bool TryParseArguments(
        string[] args,
        out HandshakeProbeOptions options,
        out string? error,
        out bool showHelp)
    {
        string host = "127.0.0.1";
        string? portValue = null;
        string? expectValue = null;
        int maxRetries = 8;

        if (args.Length == 0)
        {
            options = default;
            error = null;
            showHelp = true;
            return false;
        }

        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            if (option is "--help" or "-h")
            {
                options = default;
                error = null;
                showHelp = true;
                return false;
            }

            if (index + 1 >= args.Length)
            {
                options = default;
                error = $"Missing value for option '{option}'.";
                showHelp = false;
                return false;
            }

            string value = args[++index];
            switch (option)
            {
                case "--host": host = value; break;
                case "--port": portValue = value; break;
                case "--expect": expectValue = value; break;
                case "--max-retries":
                    if (!int.TryParse(value, out maxRetries) || maxRetries <= 0)
                    {
                        options = default;
                        error = "--max-retries must be a positive integer.";
                        showHelp = false;
                        return false;
                    }
                    break;
                default:
                    options = default;
                    error = $"Unknown option '{option}'.";
                    showHelp = false;
                    return false;
            }
        }

        if (!int.TryParse(portValue, out int port) || port is < 1 or > 65535)
        {
            options = default;
            error = "--port is required and must be between 1 and 65535.";
            showHelp = false;
            return false;
        }

        HandshakeProbeExpectedPhase expectedPhase;
        if (string.Equals(expectValue, "auth", StringComparison.OrdinalIgnoreCase))
        {
            expectedPhase = HandshakeProbeExpectedPhase.Auth;
        }
        else if (string.Equals(expectValue, "login", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(expectValue, "game", StringComparison.OrdinalIgnoreCase))
        {
            expectedPhase = HandshakeProbeExpectedPhase.Login;
        }
        else
        {
            options = default;
            error = "--expect is required and must be either 'auth' or 'login'.";
            showHelp = false;
            return false;
        }

        options = new HandshakeProbeOptions(host, port, expectedPhase, maxRetries);
        error = null;
        showHelp = false;
        return true;
    }

    private static async Task<byte[]> ReceiveExactAsync(
        Socket socket,
        int length,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        int offset = 0;
        while (offset < buffer.Length)
        {
            int received = await socket.ReceiveAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (received == 0)
            {
                throw new IOException($"Peer closed the connection while {buffer.Length - offset} bytes were still expected.");
            }

            offset += received;
        }

        return buffer;
    }

    private static async ValueTask SendAllAsync(
        Socket socket,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken)
    {
        while (!data.IsEmpty)
        {
            int sent = await socket.SendAsync(data, SocketFlags.None, cancellationToken).ConfigureAwait(false);
            if (sent <= 0)
            {
                throw new IOException("Socket send completed without progress.");
            }

            data = data.Slice(sent);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
