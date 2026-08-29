using System.Net;
using Metin2.HandshakeProbe;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Listeners;
using Metin2.Protocol.Generated;

namespace Metin2.HandshakeProbe.Tests;

[TestClass]
public sealed class HandshakeProbeIntegrationTests
{
    [TestMethod]
    public async Task Probe_completes_auth_handshake_against_live_listener()
    {
        await RunAgainstListenerAsync(PacketPhase.Auth, HandshakeProbeExpectedPhase.Auth);
    }

    [TestMethod]
    public async Task Probe_completes_game_handshake_against_live_listener()
    {
        await RunAgainstListenerAsync(PacketPhase.Login, HandshakeProbeExpectedPhase.Login);
    }

    private static async Task RunAgainstListenerAsync(
        PacketPhase nextPhase,
        HandshakeProbeExpectedPhase expectedPhase)
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new LegacyHandshakeSocketHandler(
            new AdvancingTimeProvider(),
            new FixedTokenSource(0x10203040),
            nextPhase);

        await using var listener = new TcpGameListener(new IPEndPoint(IPAddress.Loopback, 0));
        Task listenerTask = listener.RunAsync(handler, cancellation.Token);
        var endpoint = (IPEndPoint)listener.LocalEndPoint!;

        await HandshakeProbeApp.RunProbeAsync(
            new HandshakeProbeOptions(
                "127.0.0.1",
                endpoint.Port,
                expectedPhase,
                MaxRetries: 4,
                Timeout: TimeSpan.FromSeconds(3)));

        cancellation.Cancel();
        await listenerTask;
    }

    private sealed class FixedTokenSource(uint token) : IHandshakeTokenSource
    {
        public uint NextToken() => token;
    }

    private sealed class AdvancingTimeProvider : IServerTimeProvider
    {
        private long _value = 1_000;

        public long GetMilliseconds() => Interlocked.Add(ref _value, 10) - 10;
    }
}
