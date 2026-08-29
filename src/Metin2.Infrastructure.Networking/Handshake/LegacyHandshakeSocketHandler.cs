using System.Net.Sockets;
using Metin2.Infrastructure.Networking.Connections;
using Metin2.Infrastructure.Networking.Listeners;
using Metin2.Infrastructure.Networking.Receive;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Protocol.Generated;

namespace Metin2.Infrastructure.Networking.Handshake;

public sealed class LegacyHandshakeSocketHandler : IAcceptedSocketHandler
{
    private readonly IServerTimeProvider _timeProvider;
    private readonly IHandshakeTokenSource _tokenSource;
    private readonly PacketPhase _nextPhase;
    private readonly Func<GameSession, CancellationToken, ValueTask>? _onHandshakeCompleted;

    public LegacyHandshakeSocketHandler(
        IServerTimeProvider timeProvider,
        IHandshakeTokenSource tokenSource,
        PacketPhase nextPhase,
        Func<GameSession, CancellationToken, ValueTask>? onHandshakeCompleted = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(tokenSource);

        if (nextPhase is PacketPhase.Handshake or PacketPhase.Any)
        {
            throw new ArgumentOutOfRangeException(nameof(nextPhase));
        }

        _timeProvider = timeProvider;
        _tokenSource = tokenSource;
        _nextPhase = nextPhase;
        _onHandshakeCompleted = onHandshakeCompleted;
    }

    public async ValueTask HandleAsync(Socket socket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(socket);

        var session = new GameSession(PacketPhase.Handshake);
        await using var connection = new SocketConnection(socket, session);
        var target = new LegacyHandshakeDispatchTarget(
            session,
            connection.Output,
            _timeProvider,
            _tokenSource,
            _nextPhase,
            _onHandshakeCompleted);
        var consumer = new TypedPacketFrameConsumer(target);

        ValueTask<long> sendPump = connection.RunSendAsync(cancellationToken);

        try
        {
            await target.StartAsync(cancellationToken).ConfigureAwait(false);

            LegacyReceiveLoopResult receiveResult = await connection.RunReceiveAsync(
                PacketDirection.ClientToServer,
                consumer,
                cancellationToken).ConfigureAwait(false);

            if (receiveResult.Completion is not LegacyReceiveLoopCompletion.Completed)
            {
                return;
            }
        }
        finally
        {
            await connection.CompleteOutputAsync().ConfigureAwait(false);

            try
            {
                _ = await sendPump.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (SocketException)
            {
            }
        }
    }
}
