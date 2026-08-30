using System.Net.Sockets;
using Metin2.Infrastructure.Networking.Connections;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Listeners;
using Metin2.Infrastructure.Networking.Receive;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Characters.Application;
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
    private readonly CharacterSelectionService _selectionService;
    private readonly CharacterSelectService _characterSelectService;
    private readonly ILegacyCharacterSelectionWireContextProvider _selectionWireContextProvider;

    public LegacyGameSocketHandler(
        IServerTimeProvider timeProvider,
        IHandshakeTokenSource handshakeTokenSource,
        LegacySequenceProfile sequenceProfile,
        IGameLoginService loginService,
        CharacterSelectionService selectionService,
        CharacterSelectService characterSelectService,
        ILegacyCharacterSelectionWireContextProvider selectionWireContextProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(handshakeTokenSource);
        ArgumentNullException.ThrowIfNull(sequenceProfile);
        ArgumentNullException.ThrowIfNull(loginService);
        ArgumentNullException.ThrowIfNull(selectionService);
        ArgumentNullException.ThrowIfNull(characterSelectService);
        ArgumentNullException.ThrowIfNull(selectionWireContextProvider);

        _timeProvider = timeProvider;
        _handshakeTokenSource = handshakeTokenSource;
        _sequenceProfile = sequenceProfile;
        _loginService = loginService;
        _selectionService = selectionService;
        _characterSelectService = characterSelectService;
        _selectionWireContextProvider = selectionWireContextProvider;
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
        var selectionPublisher = new LegacyCharacterSelectionPublisher(
            connection.Output,
            _selectionService,
            _selectionWireContextProvider);
        var loginTarget = new GameTokenLoginDispatchTarget(
            session,
            _loginService,
            selectionPublisher);
        var characterSelectTarget = new GameCharacterSelectDispatchTarget(
            session,
            connection.Output,
            _characterSelectService);
        var target = new GameConnectionDispatchTarget(
            handshakeTarget,
            loginTarget,
            characterSelectTarget);
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
