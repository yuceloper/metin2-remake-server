using Metin2.Shared.Identity;

namespace Metin2.Modules.Auth.Application;

public readonly record struct AuthLoginRequest(string Username, string Password);

public enum CredentialVerificationFailure : byte
{
    InvalidCredentials = 1,
    LoginDenied = 2
}

public readonly record struct CredentialVerificationResult(
    bool IsSuccess,
    AccountId AccountId,
    string Username,
    CredentialVerificationFailure? Failure)
{
    public static CredentialVerificationResult Success(AccountId accountId, string username) =>
        new(true, accountId, username, null);

    public static CredentialVerificationResult InvalidCredentials() =>
        new(false, default, string.Empty, CredentialVerificationFailure.InvalidCredentials);

    public static CredentialVerificationResult LoginDenied() =>
        new(false, default, string.Empty, CredentialVerificationFailure.LoginDenied);
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
    InvalidCredentials = 1,
    LoginDenied = 2
}

public readonly record struct AuthLoginResult(
    bool IsSuccess,
    uint Token,
    AuthLoginFailure? Failure)
{
    public static AuthLoginResult Success(uint token) => new(true, token, null);

    public static AuthLoginResult Failed(AuthLoginFailure failure) => new(false, 0, failure);
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
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Username);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);

        CredentialVerificationResult verification = await _credentialVerifier
            .VerifyAsync(request.Username, request.Password, cancellationToken)
            .ConfigureAwait(false);

        if (!verification.IsSuccess)
        {
            AuthLoginFailure failure = verification.Failure switch
            {
                CredentialVerificationFailure.LoginDenied => AuthLoginFailure.LoginDenied,
                _ => AuthLoginFailure.InvalidCredentials
            };

            return AuthLoginResult.Failed(failure);
        }

        uint token = await _tokenIssuer
            .IssueAsync(verification.AccountId, verification.Username, cancellationToken)
            .ConfigureAwait(false);

        return AuthLoginResult.Success(token);
    }
}
