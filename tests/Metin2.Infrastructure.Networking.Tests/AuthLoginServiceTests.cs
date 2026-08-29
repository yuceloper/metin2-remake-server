using Metin2.Modules.Auth.Application;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class AuthLoginServiceTests
{
    [TestMethod]
    public async Task Successful_verification_issues_token()
    {
        var verifier = new StubVerifier(CredentialVerificationResult.Success(new AccountId(42), "player"));
        var issuer = new StubIssuer(0x11223344);
        var service = new AuthLoginService(verifier, issuer);

        AuthLoginResult result = await service.LoginAsync(new AuthLoginRequest("player", "secret"));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(0x11223344u, result.Token);
        Assert.AreEqual(new AccountId(42), issuer.AccountId);
        Assert.AreEqual("player", issuer.Username);
    }

    [TestMethod]
    public async Task Invalid_credentials_do_not_issue_token()
    {
        var verifier = new StubVerifier(CredentialVerificationResult.InvalidCredentials());
        var issuer = new StubIssuer(0x11223344);
        var service = new AuthLoginService(verifier, issuer);

        AuthLoginResult result = await service.LoginAsync(new AuthLoginRequest("player", "wrong"));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AuthLoginFailure.InvalidCredentials, result.Failure);
        Assert.AreEqual(0, issuer.Calls);
    }

    private sealed class StubVerifier(CredentialVerificationResult result) : IAccountCredentialVerifier
    {
        public ValueTask<CredentialVerificationResult> VerifyAsync(
            string username,
            string password,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(result);
    }

    private sealed class StubIssuer(uint token) : IAuthTokenIssuer
    {
        public int Calls { get; private set; }
        public AccountId AccountId { get; private set; }
        public string? Username { get; private set; }

        public ValueTask<uint> IssueAsync(
            AccountId accountId,
            string username,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            AccountId = accountId;
            Username = username;
            return ValueTask.FromResult(token);
        }
    }
}
