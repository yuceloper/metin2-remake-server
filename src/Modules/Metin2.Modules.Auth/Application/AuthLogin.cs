using Metin2.Shared.Identity;

namespace Metin2.Modules.Auth.Application;

public readonly record struct AuthLoginRequest(string Username, string Password);

public readonly record struct CredentialVerificationResult(
    bool IsSuccess,
    AccountId AccountId,
    string Username)
{
    public static CredentialVerificationResult Success(AccountId accountId, string username) =>
        new(true, accountId, username);

    public static CredentialVerificationResult InvalidCredentials() =>
        new(false, default, string.Empty);
}

public interface IAccountCredentialVerifier
{
    ValueTask<CredentialVerificationResult> VerifyAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);
}

public interface IAuthTokenIssuer
{
    ValueTask<uint> IssueAsync(
        AccountId accountId,
        string username,
        CancellationToken cancellationToken = default);
}

public enum AuthLoginFailure : byte
{
    InvalidCredentials = 1
}

public readonly record struct AuthLoginResult(
    bool IsSuccess,
    uint Token,
    AuthLoginFailure? Failure)
{
    public static AuthLoginResult Success(uint token) => new(true, token, null);

    public static AuthLoginResult InvalidCredentials() =>
        new(false, 0, AuthLoginFailure.InvalidCredentials);
}

public interface IAuthLoginService
{
    ValueTask<AuthLoginResult> LoginAsync(
        AuthLoginRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class AuthLoginService : IAuthLoginService
{
    private readonly IAccountCredentialVerifier _credentialVerifier;
    private readonly IAuthTokenIssuer _tokenIssuer;

    public AuthLoginService(
        IAccountCredentialVerifier credentialVerifier,
        IAuthTokenIssuer tokenIssuer)
    {
        ArgumentNullException.ThrowIfNull(credentialVerifier);
        ArgumentNullException.ThrowIfNull(tokenIssuer);

        _credentialVerifier = credentialVerifier;
        _tokenIssuer = tokenIssuer;
    }

    public async ValueTask<AuthLoginResult> LoginAsync(
        AuthLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthLoginResult.InvalidCredentials();
        }

        CredentialVerificationResult verification = await _credentialVerifier
            .VerifyAsync(request.Username, request.Password, cancellationToken)
            .ConfigureAwait(false);

        if (!verification.IsSuccess)
        {
            return AuthLoginResult.InvalidCredentials();
        }

        uint token = await _tokenIssuer
            .IssueAsync(verification.AccountId, verification.Username, cancellationToken)
            .ConfigureAwait(false);

        return AuthLoginResult.Success(token);
    }
}
