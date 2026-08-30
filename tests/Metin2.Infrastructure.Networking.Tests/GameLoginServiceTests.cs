using Metin2.Modules.Auth.Application;
using Metin2.Modules.Game.Application;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class GameLoginServiceTests
{
    [TestMethod]
    public async Task Valid_token_returns_authenticated_principal()
    {
        var consumer = new StubConsumer(new AuthTokenPrincipal(new AccountId(42), "Player"));
        var service = new GameLoginService(consumer);

        GameLoginResult result = await service.LoginAsync(new GameLoginRequest(0x11223344, "player"));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(new AccountId(42), result.AccountId);
        Assert.AreEqual("Player", result.Username);
        Assert.AreEqual(0x11223344u, consumer.Token);
        Assert.AreEqual("player", consumer.Username);
    }

    [TestMethod]
    public async Task Missing_token_is_rejected()
    {
        var consumer = new StubConsumer(null);
        var service = new GameLoginService(consumer);

        GameLoginResult result = await service.LoginAsync(new GameLoginRequest(0x11223344, "player"));

        Assert.IsFalse(result.IsSuccess);
    }

    [TestMethod]
    public async Task Zero_token_does_not_hit_consumer()
    {
        var consumer = new StubConsumer(new AuthTokenPrincipal(new AccountId(42), "player"));
        var service = new GameLoginService(consumer);

        GameLoginResult result = await service.LoginAsync(new GameLoginRequest(0, "player"));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(0, consumer.Calls);
    }

    private sealed class StubConsumer(AuthTokenPrincipal? result) : IAuthTokenConsumer
    {
        public int Calls { get; private set; }
        public uint Token { get; private set; }
        public string? Username { get; private set; }

        public ValueTask<AuthTokenPrincipal?> ConsumeAsync(
            uint token,
            string username,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Token = token;
            Username = username;
            return ValueTask.FromResult(result);
        }
    }
}
