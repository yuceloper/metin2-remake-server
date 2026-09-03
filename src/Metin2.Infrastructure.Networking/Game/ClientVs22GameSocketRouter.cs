using System.Buffers.Binary;
using System.Net.Sockets;
using Metin2.Infrastructure.Networking.Listeners;

namespace Metin2.Infrastructure.Networking.Game;

/// <summary>
/// Routes the ClientVS22 channel-state probe away from the normal game handshake.
/// The client sends 0xCE to the first channel and expects an unencrypted 0xD2 response.
/// </summary>
public sealed class ClientVs22GameSocketRouter : IAcceptedSocketHandler
{
    public const byte StateCheckerRequestHeader = 0xCE;
    public const byte ChannelStatusResponseHeader = 0xD2;
    public const byte NormalChannelStatus = 1;

    private static readonly TimeSpan DefaultProbeWindow = TimeSpan.FromMilliseconds(150);

    private readonly IAcceptedSocketHandler _gameHandler;
    private readonly short _channelPort;
    private readonly TimeSpan _probeWindow;
    private readonly Action<string>? _diagnosticSink;

    public ClientVs22GameSocketRouter(
        IAcceptedSocketHandler gameHandler,
        ushort channelPort,
        TimeSpan? probeWindow = null,
        Action<string>? diagnosticSink = null)
    {
        ArgumentNullException.ThrowIfNull(gameHandler);
        if (channelPort > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(channelPort),
                "ClientVS22 channel status encodes ports as signed 16-bit values.");
        }

        _gameHandler = gameHandler;
        _channelPort = checked((short)channelPort);
        _probeWindow = probeWindow ?? DefaultProbeWindow;
        _diagnosticSink = diagnosticSink;
        if (_probeWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(probeWindow));
        }
    }

    public async ValueTask HandleAsync(Socket socket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(socket);

        byte[] header = new byte[1];
        using var probeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probeCancellation.CancelAfter(_probeWindow);

        int received;
        try
        {
            received = await socket
                .ReceiveAsync(header, SocketFlags.Peek, probeCancellation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Trace("route=game initial-header=timeout");
            await _gameHandler.HandleAsync(socket, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (received == 0)
        {
            Trace("route=closed-before-header");
            return;
        }

        if (header[0] != StateCheckerRequestHeader)
        {
            Trace($"route=game initial-header=0x{header[0]:X2}");
            await _gameHandler.HandleAsync(socket, cancellationToken).ConfigureAwait(false);
            return;
        }

        Trace("route=state-checker");
        _ = await socket
            .ReceiveAsync(header, SocketFlags.None, cancellationToken)
            .ConfigureAwait(false);

        byte[] response = new byte[sizeof(byte) + sizeof(int) + sizeof(short) + sizeof(byte)];
        response[0] = ChannelStatusResponseHeader;
        BinaryPrimitives.WriteInt32LittleEndian(response.AsSpan(1, sizeof(int)), 1);
        BinaryPrimitives.WriteInt16LittleEndian(
            response.AsSpan(1 + sizeof(int), sizeof(short)),
            _channelPort);
        response[^1] = NormalChannelStatus;

        await SendAllAsync(socket, response, cancellationToken).ConfigureAwait(false);
    }

    private void Trace(string message) => _diagnosticSink?.Invoke($"router {message}");

    private static async ValueTask SendAllAsync(
        Socket socket,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken)
    {
        while (!data.IsEmpty)
        {
            int sent = await socket.SendAsync(data, SocketFlags.None, cancellationToken).ConfigureAwait(false);
            if (sent == 0)
            {
                throw new SocketException((int)SocketError.ConnectionReset);
            }

            data = data[sent..];
        }
    }
}
