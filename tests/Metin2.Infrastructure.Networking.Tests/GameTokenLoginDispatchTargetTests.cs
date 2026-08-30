using Metin2.Infrastructure.Networking.Game;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Game.Application;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class GameTokenLoginDispatchTargetTests
{
    [TestMethod]
    public async Task Successful_login_authenticates_session_and_copies_client_key()
    {
        var session = new GameSession(PacketPhase.Login);
        var service = new FixedGameLoginService(GameLoginResult.Success(new AccountId(7), "Player"));
        var target = new GameTokenLoginDispatchTarget(session, service);
        var sourceKey = new uint[] { 1, 2, 3, 4 };
        var packet = new TokenLogin("player", 0x11223344, sourceKey);

        await target.HandleAsync(packet, CancellationToken.None);
        sourceKey[0] = 999;

        Assert.IsTrue(session.IsAuthenticated);
        Assert.AreEqual(new AccountId(7), session.AccountId);
        Assert.AreEqual("Player", session.Username);
        CollectionAssert.AreEqual(new uint[] { 1, 2, 3, 4 }, session.ClientSecurityKey.ToArray());
        Assert.AreEqual(PacketPhase.Login, session.Phase);
    }

    [TestMethod]
    public async Task Rejected_login_does_not_authenticate_session()
    {
        var session = new GameSession(PacketPhase.Login);
        var target = new GameTokenLoginDispatchTarget(session, new FixedGameLoginService(GameLoginResult.InvalidToken()));
        var packet = new TokenLogin("player", 0x11223344, new uint[] { 1, 2, 3, 4 });

        await Assert.ThrowsExactlyAsync<GameLoginRejectedException>(
            async () => await target.HandleAsync(packet, CancellationToken.None));

        Assert.IsFalse(session.IsAuthenticated);
    }

    private sealed class FixedGameLoginService(GameLoginResult result) : IGameLoginService
    {
        public ValueTask<GameLoginResult> LoginAsync(
            GameLoginRequest request,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(result);
    }
}
