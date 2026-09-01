using Metin2.Infrastructure.Networking.Game;
using Metin2.Infrastructure.Networking.Security;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Game.Application;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class GameTokenLoginSecurityTests
{
    [TestMethod]
    public async Task Successful_token_login_rotates_initial_tea_key_to_client_key()
    {
        LegacyTeaSecurityProfile profile = CreateProfile();
        var session = new GameSession(PacketPhase.Login);
        session.ConfigureClassicTeaSecurity(profile);
        var publisher = new RecordingSelectionPublisher();
        var target = new GameTokenLoginDispatchTarget(
            session,
            new SuccessfulLoginService(),
            publisher,
            profile);
        uint[] clientKey = [0x11223344, 0x55667788, 0x99AABBCC, 0xDDEEFF00];
        var packet = new TokenLogin("player", 0x12345678, clientKey);

        await target.HandleAsync(packet, CancellationToken.None);

        Assert.IsNotNull(session.TeaSecurityState);
        Assert.AreEqual(LegacyTeaSecurityStage.RotatedClientKey, session.TeaSecurityState.Stage);
        CollectionAssert.AreEqual(clientKey, session.TeaSecurityState.DecryptionKey.ToArray());
        Assert.IsTrue(session.IsAuthenticated);
        Assert.AreEqual(new AccountId(7), session.AccountId);
        Assert.AreEqual("player", session.Username);
        Assert.IsTrue(publisher.Called);
    }

    [TestMethod]
    public async Task Plaintext_profile_keeps_existing_token_login_behavior()
    {
        var session = new GameSession(PacketPhase.Login);
        var publisher = new RecordingSelectionPublisher();
        var target = new GameTokenLoginDispatchTarget(
            session,
            new SuccessfulLoginService(),
            publisher);
        var packet = new TokenLogin("player", 0x12345678, new uint[] { 1, 2, 3, 4 });

        await target.HandleAsync(packet, CancellationToken.None);

        Assert.IsNull(session.TeaSecurityState);
        Assert.IsTrue(session.IsAuthenticated);
        Assert.IsTrue(publisher.Called);
    }

    private static LegacyTeaSecurityProfile CreateProfile() =>
        new(
            "test-classic",
            "1234abcd5678efgh"u8,
            "abcdefghijklmnop"u8);

    private sealed class SuccessfulLoginService : IGameLoginService
    {
        public ValueTask<GameLoginResult> LoginAsync(
            GameLoginRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(GameLoginResult.Success(new AccountId(7), "player"));
    }

    private sealed class RecordingSelectionPublisher : ILegacyCharacterSelectionPublisher
    {
        public bool Called { get; private set; }

        public ValueTask PublishAsync(GameSession session, CancellationToken cancellationToken = default)
        {
            Called = true;
            return ValueTask.CompletedTask;
        }
    }
}
