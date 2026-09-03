using System.Net;
using Metin2.Infrastructure.Networking.Game;\nusing Metin2.Infrastructure.Networking.Compatibility;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Listeners;
using Metin2.Infrastructure.Persistence.Postgres.Auth;
using Metin2.Infrastructure.Persistence.Postgres.Characters;
using Metin2.Modules.Characters.Application;
using Metin2.Modules.Game.Application;
using Npgsql;

namespace Metin2.Server;

public static class ServerGameComposition
{
    public const string ConnectionStringEnvironmentVariable = "METIN2_POSTGRES_CONNECTION_STRING";
    public const string AdvertisedAddressEnvironmentVariable = "METIN2_ADVERTISED_ADDRESS";\n    public const string EncryptionModeEnvironmentVariable = "METIN2_PACKET_ENCRYPTION";

    public static IAcceptedSocketHandler CreateClientVs22_28249(
        NpgsqlDataSource dataSource,
        IPEndPoint advertisedEndPoint,
        IServerTimeProvider? timeProvider = null,
        IHandshakeTokenSource? tokenSource = null,
        Action<string>? diagnosticSink = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(advertisedEndPoint);

        if (advertisedEndPoint.Port is < 1 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(advertisedEndPoint));
        }

        var characterListRepository = new PostgresCharacterListRepository(dataSource);
        var characterListService = new CharacterListService(characterListRepository);
        var selectionService = new CharacterSelectionService(
            new PostgresAccountEmpireRepository(dataSource),
            characterListService);

        LegacyGameSocketHandler gameHandler = LegacyGameSocketHandler.CreateClientVs22_28249(
            timeProvider ?? new StopwatchServerTimeProvider(),
            tokenSource ?? new RandomHandshakeTokenSource(),
            new GameLoginService(new PostgresAuthTokenConsumer(dataSource)),
            selectionService,
            new CharacterSelectService(new PostgresCharacterSelectionRepository(dataSource)),
            new CharacterBootstrapService(new PostgresCharacterBootstrapRepository(dataSource)),
            new ConfiguredLegacyCharacterSelectionWireContextProvider(
                advertisedEndPoint.Address,
                checked((ushort)advertisedEndPoint.Port)),
            new DefaultLegacyCharacterBootstrapRuntimeContextProvider(),
            diagnosticSink: diagnosticSink);

        return new ClientVs22GameSocketRouter(
            gameHandler,
            checked((ushort)advertisedEndPoint.Port));
    }
}
