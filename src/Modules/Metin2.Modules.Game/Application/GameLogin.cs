using Metin2.Modules.Auth.Application;
using Metin2.Shared.Identity;

namespace Metin2.Modules.Game.Application;

public readonly record struct GameLoginRequest(uint Token, string Username);

public readonly record struct GameLoginResult(
    bool IsSuccess,
    AccountId AccountId,
    string Username)
{
    public static GameLoginResult Success(AccountId accountId, string username) =>
        new(true, accountId, username);

    public static GameLoginResult InvalidToken() =>
        new(false, default, string.Empty);
}

public interface IGameLoginService
{
    ValueTask<GameLoginResult> LoginAsync(
        GameLoginRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class GameLoginService : IGameLoginService
{
    private readonly IAuthTokenConsumer _tokenConsumer;

    public GameLoginService(IAuthTokenConsumer tokenConsumer)
    {
        ArgumentNullException.ThrowIfNull(tokenConsumer);
        _tokenConsumer = tokenConsumer;
    }

    public async ValueTask<GameLoginResult> LoginAsync(
        GameLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Token == 0 || string.IsNullOrWhiteSpace(request.Username))
        {
            return GameLoginResult.InvalidToken();
        }

        AuthTokenPrincipal? principal = await _tokenConsumer
            .ConsumeAsync(request.Token, request.Username, cancellationToken)
            .ConfigureAwait(false);

        return principal is { } authenticated
            ? GameLoginResult.Success(authenticated.AccountId, authenticated.Username)
            : GameLoginResult.InvalidToken();
    }
}
