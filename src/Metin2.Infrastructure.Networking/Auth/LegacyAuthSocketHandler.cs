using System.Net.Sockets;
using Metin2.Infrastructure.Networking.Compatibility;
using Metin2.Infrastructure.Networking.Connections;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Listeners;
using Metin2.Infrastructure.Networking.Receive;
using Metin2.Infrastructure.Networking.Security;
using Metin2.Infrastructure.Networking.Send;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Auth.Application;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Legacy;

namespace Metin2.Infrastructure.Networking.Auth;

public sealed class LegacyAuthSocketHandler : IAcceptedSocketHandler
{
    private readonly IServerTimeProvider _timeProvider;
    private readonly IHandshakeTokenSource _handshakeTokenSource;
    private readonly LegacySequenceProfile _sequenceProfile;
    private readonly IAuthLoginService _loginService;
    private readonly LegacyClientCompatibilityProfile? _compatibilityProfile;
    private readonly IImprovedCipherProvider? _improvedCipherProvider;

    public static LegacyAuthSocketHandler CreateClientVs22_28249(
        IServerTimeProvider timeProvider,
        IHandshakeTokenSource handshakeTokenSource,
        IAuthLoginService loginService,
        IImprovedCipherProvider? improvedCipherProvider = null)
    {
        LegacyClientCompatibilityProfile profile = ClientVs22_28249CompatibilityProfile.Create();
        return new LegacyAuthSocketHandler(
            timeProvider,
            handshakeTokenSource,
            profile.Sequence,
            loginService,
            profile,
            improvedCipherProvider);
    }

    public LegacyAuthSocketHandler(
        IServerTimeProvider timeProvider,
        IHandshakeTokenSource handshakeTokenSource,
        LegacySequenceProfile sequenceProfile,
        IAuthLoginService loginService,
        LegacyClientCompatibilityProfile? compatibilityProfile = null,
        IImprovedCipherProvider? improvedCipherProvider = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(handshakeTokenSource);
        ArgumentNullException.ThrowIfNull(sequenceProfile);
        ArgumentNullException.ThrowIfNull(loginService);

        _timeProvider = timeProvider;
        _handshakeTokenSource = handshakeTokenSource;
        _sequenceProfile = sequenceProfile;
        if (compatibilityProfile?.EncryptionMode == LegacyPacketEncryptionMode.ImprovedPacketEncryption &&
            improvedCipherProvider is null)
        {
            improvedCipherProvider = new BouncyCastleImprovedCipherProvider();
        }

        _loginService = loginService;
        _compatibilityProfile = compatibilityProfile;
        _improvedCipherProvider = improvedCipherProvider;
    }

    public async ValueTask HandleAsync(Socket socket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(socket);

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
                PacketPhase.Auth);
            improvedKeyAgreementTarget = improvedTarget;
            handshakeCompleted = (_, ct) => improvedTarget.StartAsync(ct);
            deferPostHandshakePhase = true;
        }

        var handshakeTarget = new LegacyHandshakeDispatchTarget(
            session,
            packetOutput,
            _timeProvider,
            _handshakeTokenSource,
            PacketPhase.Auth,
            handshakeCompleted,
            deferPostHandshakePhase);
        var authTarget = new AuthLoginDispatchTarget(packetOutput, _loginService);
        var target = new AuthConnectionDispatchTarget(
            handshakeTarget,
            authTarget,
            improvedKeyAgreementTarget);
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
