using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Protocol.Generated.Packets;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class LegacyHandshakeStateTests
{
    [TestMethod]
    public void Matching_token_inside_acceptance_window_completes()
    {
        var state = new LegacyHandshakeState(0x12345678);
        _ = state.Start(1_000);
        var packet = new Handshake(0x12345678, 1_000, 0);

        LegacyHandshakeResult result = state.Handle(in packet, 1_025);

        Assert.AreEqual(LegacyHandshakeDecision.Completed, result.Decision);
        Assert.IsFalse(state.IsActive);
        Assert.IsNull(result.Response);
    }

    [TestMethod]
    public void Wrong_token_is_rejected()
    {
        var state = new LegacyHandshakeState(10);
        _ = state.Start(1_000);
        var packet = new Handshake(11, 1_000, 0);

        LegacyHandshakeResult result = state.Handle(in packet, 1_010);

        Assert.AreEqual(LegacyHandshakeDecision.RejectedWrongToken, result.Decision);
        Assert.IsFalse(state.IsActive);
    }

    [TestMethod]
    public void Out_of_window_response_retries_with_reference_delta()
    {
        var state = new LegacyHandshakeState(7);
        _ = state.Start(1_000);
        var packet = new Handshake(7, 800, 0);

        LegacyHandshakeResult result = state.Handle(in packet, 1_200);

        Assert.AreEqual(LegacyHandshakeDecision.Retry, result.Decision);
        Assert.IsNotNull(result.Response);
        Assert.AreEqual(7u, result.Response.Value.HandshakeValue);
        Assert.AreEqual(1_200u, result.Response.Value.Time);
        Assert.AreEqual(200u, result.Response.Value.Delta);
        Assert.AreEqual(1_200L, state.LastHandshakeTime);
    }

    [TestMethod]
    public void Negative_primary_delta_uses_last_send_time_fallback()
    {
        var state = new LegacyHandshakeState(9);
        _ = state.Start(1_000);
        var packet = new Handshake(9, 1_500, 0);

        LegacyHandshakeResult result = state.Handle(in packet, 1_100);

        Assert.AreEqual(LegacyHandshakeDecision.Retry, result.Decision);
        Assert.IsNotNull(result.Response);
        Assert.AreEqual(50u, result.Response.Value.Delta);
    }
}
