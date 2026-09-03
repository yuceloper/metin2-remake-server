using Metin2.Infrastructure.Networking.Auth;
using Metin2.Infrastructure.Networking.Compatibility;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Listeners;
using Metin2.Infrastructure.Persistence.Postgres.Auth;
using Metin2.Infrastructure.Persistence.Postgres.Security;
using Metin2.Modules.Auth.Application;
using Npgsql;

namespace Metin2.Server;

public static class ServerAuthComposition
{
    public static IAcceptedSocketHandler CreateClientVs22_28249(
        NpgsqlDataSource dataSource,
        IServerTimeProvider? timeProvider = null,
        IHandshakeTokenSource? tokenSource = null,
        Action<string>? diagnosticSink = null,
        LegacyPacketEncryptionMode encryptionMode = LegacyPacketEncryptionMode.ImprovedPacketEncryption)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        var loginService = new AuthLoginService(
            new PostgresAccountCredentialVerifier(dataSource, new Pbkdf2PasswordHasher()),
            new PostgresAuthTokenIssuer(dataSource));

        return LegacyAuthSocketHandler.CreateClientVs22_28249(
            timeProvider ?? new StopwatchServerTimeProvider(),
            tokenSource ?? new RandomHandshakeTokenSource(),
            loginService,
            encryptionMode: encryptionMode,
            diagnosticSink: diagnosticSink);
    }
}
