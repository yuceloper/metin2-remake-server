using Metin2.Infrastructure.Networking.Compatibility;
using Metin2.Infrastructure.Networking.Game;
using Metin2.Infrastructure.Networking.Security;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Game.Application;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Protocol.Legacy;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class LegacyCompatibilitySecurityLifecycleTests
{
    [TestMethod]
    public async Task Classic_profile_moves_from_initial_to_client_rotated_key_on_token_login()
    {
        LegacyTeaSecurityProfile tea = CreateTeaProfile();
        var compatibility = new LegacyClientCompatibilityProfile(
            "classic-test",
            new LegacySequenceProfile("test", new byte[] { 0xAA }),
            LegacyPacketEncryptionMode.ClassicTea,
            tea);
        var session = new GameSession(PacketPhase.Login, compatibilityProfile: compatibility);
        session.ActivateConfiguredPacketSecurity();

        Assert.IsNotNull(session.TeaSecurityState);
        Assert.AreEqual(LegacyTeaSecurityStage.InitialKey, session.TeaSecurityState.Stage);

        var publisher = new RecordingPublisher();
        var target = new GameTokenLoginDispatchTarget(session, new SuccessfulLoginService(), publisher);
        uint[] clientKey = [0x11223344, 0x55667788, 0x99AABBCC, 0xDDEEFF00];

        await target.HandleAsync(
            new TokenLogin("player", 0x12345678, clientKey),
            CancellationToken.None);

        Assert.AreEqual(LegacyTeaSecurityStage.RotatedClientKey, session.TeaSecurityState.Stage);
        CollectionAssert.AreEqual(clientKey, session.TeaSecurityState.DecryptionKey.ToArray());
        Assert.IsTrue(publisher.Called);
    }

    [TestMethod]
    public async Task None_profile_preserves_plaintext_login_path()
    {
        var compatibility = new LegacyClientCompatibilityProfile(
            "plain-test",
            new LegacySequenceProfile("test", new byte[] { 0xAA }),
            LegacyPacketEncryptionMode.None);
        var session = new GameSession(PacketPhase.Login, compatibilityProfile: compatibility);
        session.ActivateConfiguredPacketSecurity();
        var publisher = new RecordingPublisher();
        var target = new GameTokenLoginDispatchTarget(session, new SuccessfulLoginService(), publisher);

        await target.HandleAsync(
            new TokenLogin("player", 0x12345678, new uint[] { 1, 2, 3, 4 }),
            CancellationToken.None);

        Assert.IsNull(session.TeaSecurityState);
        Assert.IsTrue(session.IsAuthenticated);
        Assert.IsTrue(publisher.Called);
    }

    private static LegacyTeaSecurityProfile CreateTeaProfile() =>
        new("classic-test", "1234abcd5678efgh"u8, "abcdefghijklmnop"u8);

    private sealed class SuccessfulLoginService : IGameLoginService
    {
        public ValueTask<GameLoginResult> LoginAsync(
            GameLoginRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(GameLoginResult.Success(new AccountId(7), "player"));
    }

    private sealed class RecordingPublisher : ILegacyCharacterSelectionPublisher
    {
        public bool Called { get; private set; }

        public ValueTask PublishAsync(GameSession session, CancellationToken cancellationToken = default)
        {
            Called = true;
            return ValueTask.CompletedTask;
        }
    }
}
