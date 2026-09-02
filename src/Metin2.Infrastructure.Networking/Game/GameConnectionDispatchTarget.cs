using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Security;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using HandshakePacket = Metin2.Protocol.Generated.Packets.Handshake;

namespace Metin2.Infrastructure.Networking.Game;

public sealed class GameConnectionDispatchTarget : IPacketDispatchTarget
{
    private readonly LegacyHandshakeDispatchTarget _handshake;
    private readonly GameTokenLoginDispatchTarget _login;
    private readonly GameCharacterSelectDispatchTarget _characterSelect;
    private readonly GameEnterGameDispatchTarget _enterGame;
    private readonly ImprovedKeyAgreementDispatchTarget? _improvedKeyAgreement;

    public GameConnectionDispatchTarget(
        LegacyHandshakeDispatchTarget handshake,
        GameTokenLoginDispatchTarget login,
        GameCharacterSelectDispatchTarget characterSelect,
        GameEnterGameDispatchTarget enterGame,
        ImprovedKeyAgreementDispatchTarget? improvedKeyAgreement = null)
    {
        ArgumentNullException.ThrowIfNull(handshake);
        ArgumentNullException.ThrowIfNull(login);
        ArgumentNullException.ThrowIfNull(characterSelect);
        ArgumentNullException.ThrowIfNull(enterGame);
        _handshake = handshake;
        _login = login;
        _characterSelect = characterSelect;
        _enterGame = enterGame;
        _improvedKeyAgreement = improvedKeyAgreement;
    }

    public ValueTask HandleAsync(HandshakePacket packet, CancellationToken cancellationToken) =>
        _handshake.HandleAsync(packet, cancellationToken);

    public ValueTask HandleAsync(KeyAgreement packet, CancellationToken cancellationToken) =>
        _improvedKeyAgreement?.HandleAsync(packet, cancellationToken)
        ?? Unsupported(packet);

    public ValueTask HandleAsync(TokenLogin packet, CancellationToken cancellationToken) =>
        _login.HandleAsync(packet, cancellationToken);

    public ValueTask HandleAsync(SelectCharacter packet, CancellationToken cancellationToken) =>
        _characterSelect.HandleAsync(packet, cancellationToken);

    public ValueTask HandleAsync(EnterGame packet, CancellationToken cancellationToken) =>
        _enterGame.HandleAsync(packet, cancellationToken);

    public ValueTask HandleAsync(LoginRequest packet, CancellationToken cancellationToken) => Unsupported(packet);
    public ValueTask HandleAsync(LoginFailed packet, CancellationToken cancellationToken) => Unsupported(packet);
    public ValueTask HandleAsync(LoginSuccess packet, CancellationToken cancellationToken) => Unsupported(packet);
    public ValueTask HandleAsync(Phase packet, CancellationToken cancellationToken) => Unsupported(packet);

    private static ValueTask Unsupported<TPacket>(TPacket packet) =>
        ValueTask.FromException(new InvalidOperationException(
            $"Packet '{typeof(TPacket).Name}' is not valid on this Game connection state."));
}
