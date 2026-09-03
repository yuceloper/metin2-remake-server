using System.Net;
using Metin2.Infrastructure.Networking.Compatibility;
using Metin2.Infrastructure.Networking.Game;
using Metin2.Protocol.Legacy;
using Npgsql;

namespace Metin2.Server.Tests;

[TestClass]
public sealed class ServerGameCompositionTests
{
    [TestMethod]
    public async Task ClientVs22_composition_builds_full_game_socket_handler_without_opening_database()
    {
        await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Database=metin2;Username=test;Password=test;Timeout=1");

        var handler = ServerGameComposition.CreateClientVs22_28249(
            dataSource,
            new IPEndPoint(IPAddress.Loopback, 13000));

        Assert.IsInstanceOfType<LegacyGameSocketHandler>(handler);
    }

    [TestMethod]
    public async Task ClientVs22_auth_composition_builds_full_auth_socket_handler_without_opening_database()
    {
        await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Database=metin2;Username=test;Password=test;Timeout=1");

        var handler = ServerAuthComposition.CreateClientVs22_28249(dataSource);

        Assert.IsInstanceOfType<Metin2.Infrastructure.Networking.Auth.LegacyAuthSocketHandler>(handler);
    }

    [TestMethod]
    public void Selection_wire_provider_uses_ipv4_wire_order_and_profile_sequence()
    {
        var profile = ClientVs22_28249CompatibilityProfile.Create();
        var session = new Metin2.Infrastructure.Networking.Sessions.GameSession(
            compatibilityProfile: profile);
        var provider = new ConfiguredLegacyCharacterSelectionWireContextProvider(
            IPAddress.Parse("1.2.3.4"),
            13000);

        LegacyCharacterSelectionWireContext context = provider.Get(session);

        Assert.AreEqual(0x04030201, context.AddressWireValue);
        Assert.AreEqual((ushort)13000, context.Port);
        Assert.AreEqual(profile.Sequence[0], context.EmpireSequence);
        Assert.AreNotEqual(0u, context.Handle);
        Assert.AreNotEqual(0u, context.RandomKey);
    }
}
