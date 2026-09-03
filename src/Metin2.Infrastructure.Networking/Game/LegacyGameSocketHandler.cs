using System.Net.Sockets;
using Metin2.Infrastructure.Networking.Compatibility;
using Metin2.Infrastructure.Networking.Connections;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Listeners;
using Metin2.Infrastructure.Networking.Receive;
using Metin2.Infrastructure.Networking.Security;
using Metin2.Infrastructure.Networking.Send;
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
    private readonly byte _channelNumber;
    private readonly LegacyClientCompatibilityProfile? _compatibilityProfile;
    private readonly IImprovedCipherProvider? _improvedCipherProvider;
    private readonly Action<string>? _diagnosticSink;

    public static LegacyGameSocketHandler CreateClientVs22_28249(
        IServerTimeProvider timeProvider,
        IHandshakeTokenSource handshakeTokenSource,
        IGameLoginService loginService,
        CharacterSelectionService selectionService,
        CharacterSelectService characterSelectService,
        CharacterBootstrapService bootstrapService,
        ILegacyCharacterSelectionWireContextProvider selectionWireContextProvider,
        ILegacyCharacterBootstrapRuntimeContextProvider bootstrapRuntimeContextProvider,
        PlayerRuntimeRegistry? runtimeRegistry = null,
        byte channelNumber = 1,
        LegacyPacketEncryptionMode encryptionMode = LegacyPacketEncryptionMode.ImprovedPacketEncryption,
        IImprovedCipherProvider? improvedCipherProvider = null,
        Action<string>? diagnosticSink = null)
    {
        LegacyClientCompatibilityProfile profile = ClientVs22_28249CompatibilityProfile.Create(encryptionMode);
        return new LegacyGameSocketHandler(
            timeProvider,
            handshakeTokenSource,
            profile.Sequence,
            loginService,
            selectionService,
            characterSelectService,
            bootstrapService,
            selectionWireContextProvider,
            bootstrapRuntimeContextProvider,
            runtimeRegistry,
            channelNumber,
            profile,
            improvedCipherProvider,
            diagnosticSink);
    }

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
        PlayerRuntimeRegistry? runtimeRegistry = null,
        byte channelNumber = 1,
        LegacyClientCompatibilityProfile? compatibilityProfile = null,
        IImprovedCipherProvider? improvedCipherProvider = null,
        Action<string>? diagnosticSink = null)
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

        if (compatibilityProfile?.EncryptionMode == LegacyPacketEncryptionMode.ImprovedPacketEncryption &&
            improvedCipherProvider is null)
        {
            improvedCipherProvider = new BouncyCastleImprovedCipherProvider();
        }

        _timeProvider = timeProvider;
        _handshakeTokenSource = handshakeTokenSource;
        _sequenceProfile = sequenceProfile;
        _loginService = loginService;
        _selectionService = selectionService;
        _characterSelectService = characterSelectService;
        _bootstrapService = bootstrapService;
        _selectionWireContextProvider = selectionWireContextProvider;
        _bootstrapRuntimeContextProvider = bootstrapRuntimeContextProvider;
        _runtimeRegistry = runtimeRegistry ?? new PlayerRuntimeRegistry(new MonotonicEntityIdAllocator());
        _channelNumber = channelNumber;
        _compatibilityProfile = compatibilityProfile;
        _improvedCipherProvider = improvedCipherProvider;
        _diagnosticSink = diagnosticSink;
    }

    public async ValueTask HandleAsync(Socket socket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(socket);
        string connectionId = Guid.NewGuid().ToString("N")[..8];
        Trace(connectionId, "connection-open");

        LegacySequenceProfile sequenceProfile = _compatibilityProfile?.Sequence ?? _sequenceProfile;
        var session = new GameSession(
            PacketPhase.Handshake,
            new LegacySequenceState(sequenceProfile),
            _compatibilityProfile);
        await using var connection = new SocketConnection(socket, session);
        var packetOutput = new LegacyPacketOutput(connection.Output, session);

        ImprovedKeyAgreementDispatchTarget? improvedKeyAgreementTarget = null;
        Func<GameSession, CancellationToken, ValueTask>? handshakeCompleted = null;
        bool deferPostHandshakePhase = false;

        if (_compatibilityProfile?.EncryptionMode == LegacyPacketEncryptionMode.ImprovedPacketEncryption)
        {
            IImprovedCipherProvider cipherProvider = _improvedCipherProvider
                ?? throw new InvalidOperationException("Improved cipher provider was not configured.");
            var improvedSecurity = new ImprovedPacketSecuritySession(
                new ImprovedDh2KeyAgreement(),
                cipherProvider);
            session.ConfigureImprovedSecurity(improvedSecurity);
            var improvedTarget = new ImprovedKeyAgreementDispatchTarget(
                session,
                packetOutput,
                improvedSecurity,
                PacketPhase.Login);
            improvedKeyAgreementTarget = improvedTarget;
            handshakeCompleted = async (_, ct) =>
            {
                await improvedTarget.StartAsync(ct).ConfigureAwait(false);
                Trace(connectionId, "improved-offer-sent");
            };
            deferPostHandshakePhase = true;
        }
        else if (_compatibilityProfile?.EncryptionMode == LegacyPacketEncryptionMode.ClassicTea)
        {
            // Reference boundary: the Login phase packet is plaintext, then the initial classic key activates.
            handshakeCompleted = (completedSession, _) =>
            {
                completedSession.ActivateConfiguredPacketSecurity();
                return ValueTask.CompletedTask;
            };
        }

        var handshakeTarget = new LegacyHandshakeDispatchTarget(
            session,
            packetOutput,
            _timeProvider,
            _handshakeTokenSource,
            PacketPhase.Login,
            handshakeCompleted,
            deferPostHandshakePhase);
        var selectionPublisher = new LegacyCharacterSelectionPublisher(connection.Output, _selectionService, _selectionWireContextProvider);
        var loginTarget = new GameTokenLoginDispatchTarget(
            session,
            _loginService,
            selectionPublisher,
            success => Trace(connectionId, success ? "game-login-success" : "game-login-rejected"));
        var bootstrapPublisher = new LegacyCharacterBootstrapPublisher(connection.Output, _bootstrapService, _bootstrapRuntimeContextProvider, _runtimeRegistry);
        var characterSelectTarget = new GameCharacterSelectDispatchTarget(session, connection.Output, _characterSelectService, bootstrapPublisher);
        var enterGameTarget = new GameEnterGameDispatchTarget(
            session,
            connection.Output,
            _runtimeRegistry,
            _timeProvider,
            _bootstrapService,
            _bootstrapRuntimeContextProvider,
            _channelNumber);
        var target = new GameConnectionDispatchTarget(
            handshakeTarget,
            loginTarget,
            characterSelectTarget,
            enterGameTarget,
            improvedKeyAgreementTarget);
        var consumer = new TypedPacketFrameConsumer(target);
        ValueTask<long> sendPump = connection.RunSendAsync(cancellationToken);

        try
        {
            await handshakeTarget.StartAsync(cancellationToken).ConfigureAwait(false);
            Trace(connectionId, "handshake-sent");
            _ = await connection.RunReceiveAsync(PacketDirection.ClientToServer, consumer, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Trace(connectionId, $"connection-failed type={exception.GetType().Name} phase={session.Phase}");
            throw;
        }
        finally
        {
            Trace(connectionId, $"connection-close phase={session.Phase}");
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

    private void Trace(string connectionId, string message) =>
        _diagnosticSink?.Invoke($"connection={connectionId} {message}");
}
