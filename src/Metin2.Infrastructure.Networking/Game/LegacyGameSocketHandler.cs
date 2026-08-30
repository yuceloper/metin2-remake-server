using System.Net.Sockets;
using Metin2.Infrastructure.Networking.Connections;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Listeners;
using Metin2.Infrastructure.Networking.Receive;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Game.Application;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Legacy;

namespace Metin2.Infrastructure.Networking.Game;

public sealed class LegacyGameSocketHandler : IAcceptedSocketHandler
{
    private readonly IServerTimeProvider _timeProvider;
    private readonly IHandshakeTokenSource _handshakeTokenSource;
    private readonly LegacySequenceProfile _sequenceProfile;
    private readonly IGameLoginService _loginService;

    public LegacyGameSocketHandler(
        IServerTimeProvider timeProvider,
        IHandshakeTokenSource handshakeTokenSource,
        LegacySequenceProfile sequenceProfile,
        IGameLoginService loginService)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(handshakeTokenSource);
        ArgumentNullException.ThrowIfNull(sequenceProfile);
        ArgumentNullException.ThrowIfNull(loginService);

        _timeProvider = timeProvider;
        _handshakeTokenSource = handshakeTokenSource;
        _sequenceProfile = sequenceProfile;
        _loginService = loginService;
    }

    public async ValueTask HandleAsync(Socket socket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(socket);

        var session = new GameSession(
            PacketPhase.Handshake,
            new LegacySequenceState(_sequenceProfile));

        await using var connection = new SocketConnection(socket, session);
        var handshakeTarget = new LegacyHandshakeDispatchTarget(
            session,
            connection.Output,
            _timeProvider,
            _handshakeTokenSource,
            PacketPhase.Login);
        var loginTarget = new GameTokenLoginDispatchTarget(session, _loginService);
        var target = new GameConnectionDispatchTarget(handshakeTarget, loginTarget);
        var consumer = new TypedPacketFrameConsumer(target);

        ValueTask<long> sendPump = connection.RunSendAsync(cancellationToken);

        try
        {
            await handshakeTarget.StartAsync(cancellationToken).ConfigureAwait(false);

            _ = await connection.RunReceiveAsync(
                PacketDirection.ClientToServer,
                consumer,
                cancellationToken).ConfigureAwait(false);
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
