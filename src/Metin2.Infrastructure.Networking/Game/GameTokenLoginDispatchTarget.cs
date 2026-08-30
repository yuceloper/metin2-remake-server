using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Game.Application;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using HandshakePacket = Metin2.Protocol.Generated.Packets.Handshake;

namespace Metin2.Infrastructure.Networking.Game;

public sealed class GameTokenLoginDispatchTarget : IPacketDispatchTarget
{
    private readonly GameSession _session;
    private readonly IGameLoginService _loginService;
    private readonly ILegacyCharacterSelectionPublisher _selectionPublisher;

    public GameTokenLoginDispatchTarget(
        GameSession session,
        IGameLoginService loginService,
        ILegacyCharacterSelectionPublisher selectionPublisher)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(loginService);
        ArgumentNullException.ThrowIfNull(selectionPublisher);
        _session = session;
        _loginService = loginService;
        _selectionPublisher = selectionPublisher;
    }

    public async ValueTask HandleAsync(TokenLogin packet, CancellationToken cancellationToken)
    {
        GameLoginResult result = await _loginService
            .LoginAsync(new GameLoginRequest(packet.Key, packet.Username), cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            throw new GameLoginRejectedException(packet.Username);
        }

        _session.Authenticate(result.AccountId, result.Username, packet.XteaKey.Span);
        await _selectionPublisher.PublishAsync(_session, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask HandleAsync(HandshakePacket packet, CancellationToken cancellationToken) => Unsupported(packet);
    public ValueTask HandleAsync(LoginRequest packet, CancellationToken cancellationToken) => Unsupported(packet);
    public ValueTask HandleAsync(LoginFailed packet, CancellationToken cancellationToken) => Unsupported(packet);
    public ValueTask HandleAsync(LoginSuccess packet, CancellationToken cancellationToken) => Unsupported(packet);
    public ValueTask HandleAsync(Phase packet, CancellationToken cancellationToken) => Unsupported(packet);

    private static ValueTask Unsupported<TPacket>(TPacket packet) =>
        ValueTask.FromException(new InvalidOperationException(
            $"Packet '{typeof(TPacket).Name}' is not handled by the Game login target."));
}

public sealed class GameLoginRejectedException : Exception
{
    public GameLoginRejectedException(string username)
        : base($"Game login token was rejected for user '{username}'.")
    {
    }
}
