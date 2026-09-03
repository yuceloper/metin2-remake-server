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
    public async Task Successful_login_authenticates_session_copies_client_key_and_publishes_selection()
    {
        var session = new GameSession(PacketPhase.Login);
        var service = new FixedGameLoginService(GameLoginResult.Success(new AccountId(7), "Player"));
        var publisher = new RecordingSelectionPublisher();
        var target = new GameTokenLoginDispatchTarget(session, service, publisher);
        var sourceKey = new uint[] { 1, 2, 3, 4 };
        var packet = new TokenLogin("player", 0x11223344, sourceKey);

        await target.HandleAsync(packet, CancellationToken.None);
        sourceKey[0] = 999;

        Assert.IsTrue(session.IsAuthenticated);
        Assert.AreEqual(new AccountId(7), session.AccountId);
        Assert.AreEqual("Player", session.Username);
        CollectionAssert.AreEqual(new uint[] { 1, 2, 3, 4 }, session.ClientSecurityKey.ToArray());
        Assert.AreEqual(1, publisher.PublishCount);
        Assert.IsTrue(publisher.SawAuthenticatedSession);
    }

    [TestMethod]
    public async Task Rejected_login_does_not_authenticate_or_publish_selection()
    {
        var session = new GameSession(PacketPhase.Login);
        var publisher = new RecordingSelectionPublisher();
        bool? reportedSuccess = null;
        var target = new GameTokenLoginDispatchTarget(
            session,
            new FixedGameLoginService(GameLoginResult.InvalidToken()),
            publisher,
            success => reportedSuccess = success);
        var packet = new TokenLogin("player", 0x11223344, new uint[] { 1, 2, 3, 4 });

        GameLoginRejectedException exception = await Assert.ThrowsExactlyAsync<GameLoginRejectedException>(
            async () => await target.HandleAsync(packet, CancellationToken.None));

        Assert.IsFalse(reportedSuccess);
        Assert.DoesNotContain("player", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsFalse(session.IsAuthenticated);
        Assert.AreEqual(0, publisher.PublishCount);
    }

    private sealed class FixedGameLoginService(GameLoginResult result) : IGameLoginService
    {
        public ValueTask<GameLoginResult> LoginAsync(
            GameLoginRequest request,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(result);
    }

    private sealed class RecordingSelectionPublisher : ILegacyCharacterSelectionPublisher
    {
        public int PublishCount { get; private set; }
        public bool SawAuthenticatedSession { get; private set; }

        public ValueTask PublishAsync(GameSession session, CancellationToken cancellationToken = default)
        {
            PublishCount++;
            SawAuthenticatedSession = session.IsAuthenticated;
            return ValueTask.CompletedTask;
        }
    }
}
