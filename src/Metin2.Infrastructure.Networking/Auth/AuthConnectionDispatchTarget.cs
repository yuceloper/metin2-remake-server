using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Security;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using HandshakePacket = Metin2.Protocol.Generated.Packets.Handshake;

namespace Metin2.Infrastructure.Networking.Auth;

public sealed class AuthConnectionDispatchTarget : IPacketDispatchTarget
{
    private readonly LegacyHandshakeDispatchTarget _handshake;
    private readonly AuthLoginDispatchTarget _auth;
    private readonly ImprovedKeyAgreementDispatchTarget? _improvedKeyAgreement;

    public AuthConnectionDispatchTarget(
        LegacyHandshakeDispatchTarget handshake,
        AuthLoginDispatchTarget auth,
        ImprovedKeyAgreementDispatchTarget? improvedKeyAgreement = null)
    {
        ArgumentNullException.ThrowIfNull(handshake);
        ArgumentNullException.ThrowIfNull(auth);
        _handshake = handshake;
        _auth = auth;
        _improvedKeyAgreement = improvedKeyAgreement;
    }

    public ValueTask HandleAsync(HandshakePacket packet, CancellationToken cancellationToken) =>
        _handshake.HandleAsync(packet, cancellationToken);

    public ValueTask HandleAsync(KeyAgreement packet, CancellationToken cancellationToken) =>
        _improvedKeyAgreement?.HandleAsync(packet, cancellationToken)
        ?? Unsupported(packet);

    public ValueTask HandleAsync(LoginRequest packet, CancellationToken cancellationToken) =>
        _auth.HandleAsync(packet, cancellationToken);

    public ValueTask HandleAsync(LoginFailed packet, CancellationToken cancellationToken) => Unsupported(packet);
    public ValueTask HandleAsync(LoginSuccess packet, CancellationToken cancellationToken) => Unsupported(packet);
    public ValueTask HandleAsync(Phase packet, CancellationToken cancellationToken) => Unsupported(packet);
    public ValueTask HandleAsync(TokenLogin packet, CancellationToken cancellationToken) => Unsupported(packet);

    private static ValueTask Unsupported<TPacket>(TPacket packet) =>
        ValueTask.FromException(new InvalidOperationException(
            $"Packet '{typeof(TPacket).Name}' is not valid on an auth client connection."));
}
