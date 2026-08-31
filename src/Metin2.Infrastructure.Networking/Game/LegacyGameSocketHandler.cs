using System.Net.Sockets;
using Metin2.Infrastructure.Networking.Connections;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Listeners;
using Metin2.Infrastructure.Networking.Receive;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Characters.Application;
using Metin2.Modules.Game.Application;
using Metin2.Modules.World;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Legacy;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Game;

public sealed class LegacyGameSocketHandler : IAcceptedSocketHandler
{
    private readonly IServerTimeProvider _timeProvider;
    private readonly IHandshakeTokenSource _handshakeTokenSource;
    private readonly LegacySequenceProfile _sequenceProfile;
    private readonly IGameLoginService _loginService;
    private readonly CharacterSelectionService _selectionService;
    private readonly CharacterSelectService _characterSelectService;
    private readonly CharacterBootstrapService _bootstrapService;
    private readonly ILegacyCharacterSelectionWireContextProvider _selectionWireContextProvider;
    private readonly ILegacyCharacterBootstrapRuntimeContextProvider _bootstrapRuntimeContextProvider;
    private readonly PlayerRuntimeRegistry _runtimeRegistry;

    public LegacyGameSocketHandler(
        IServerTimeProvider timeProvider,
        IHandshakeTokenSource handshakeTokenSource,
        LegacySequenceProfile sequenceProfile,
        IGameLoginService loginService,
        CharacterSelectionService selectionService,
        CharacterSelectService characterSelectService,
        CharacterBootstrapService bootstrapService,
        ILegacyCharacterSelectionWireContextProvider selectionWireContextProvider,
        ILegacyCharacterBootstrapRuntimeContextProvider bootstrapRuntimeContextProvider,
        PlayerRuntimeRegistry runtimeRegistry)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(handshakeTokenSource);
        ArgumentNullException.ThrowIfNull(sequenceProfile);
        ArgumentNullException.ThrowIfNull(loginService);
        ArgumentNullException.ThrowIfNull(selectionService);
        ArgumentNullException.ThrowIfNull(characterSelectService);
        ArgumentNullException.ThrowIfNull(bootstrapService);
        ArgumentNullException.ThrowIfNull(selectionWireContextProvider);
        ArgumentNullException.ThrowIfNull(bootstrapRuntimeContextProvider);
        ArgumentNullException.ThrowIfNull(runtimeRegistry);

        _timeProvider = timeProvider;
        _handshakeTokenSource = handshakeTokenSource;
        _sequenceProfile = sequenceProfile;
        _loginService = loginService;
        _selectionService = selectionService;
        _characterSelectService = characterSelectService;
        _bootstrapService = bootstrapService;
        _selectionWireContextProvider = selectionWireContextProvider;
        _bootstrapRuntimeContextProvider = bootstrapRuntimeContextProvider;
        _runtimeRegistry = runtimeRegistry;
    }

    public async ValueTask HandleAsync(Socket socket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(socket);

        var session = new GameSession(PacketPhase.Handshake, new LegacySequenceState(_sequenceProfile));
        await using var connection = new SocketConnection(socket, session);
        var handshakeTarget = new LegacyHandshakeDispatchTarget(session, connection.Output, _timeProvider, _handshakeTokenSource, PacketPhase.Login);
        var selectionPublisher = new LegacyCharacterSelectionPublisher(connection.Output, _selectionService, _selectionWireContextProvider);
        var loginTarget = new GameTokenLoginDispatchTarget(session, _loginService, selectionPublisher);
        var bootstrapPublisher = new LegacyCharacterBootstrapPublisher(connection.Output, _bootstrapService, _bootstrapRuntimeContextProvider, _runtimeRegistry);
        var characterSelectTarget = new GameCharacterSelectDispatchTarget(session, connection.Output, _characterSelectService, bootstrapPublisher);
        var target = new GameConnectionDispatchTarget(handshakeTarget, loginTarget, characterSelectTarget);
        var consumer = new TypedPacketFrameConsumer(target);
        ValueTask<long> sendPump = connection.RunSendAsync(cancellationToken);

        try
        {
            await handshakeTarget.StartAsync(cancellationToken).ConfigureAwait(false);
            _ = await connection.RunReceiveAsync(PacketDirection.ClientToServer, consumer, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (session.ClearRuntimeEntity() is EntityId runtimeEntityId)
            {
                _runtimeRegistry.Release(runtimeEntityId);
            }

            await connection.CompleteOutputAsync().ConfigureAwait(false);
            try { _ = await sendPump.ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (SocketException) { }
        }
    }
}
