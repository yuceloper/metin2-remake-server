using HandshakePacket = Metin2.Protocol.Generated.Packets.Handshake;

namespace Metin2.Infrastructure.Networking.Handshake;

public enum LegacyHandshakeDecision : byte
{
    Retry = 1,
    Completed = 2,
    RejectedWrongToken = 3,
    RejectedUnexpectedPacket = 4
}

public readonly record struct LegacyHandshakeResult(
    LegacyHandshakeDecision Decision,
    HandshakePacket? Response = null);

public sealed class LegacyHandshakeState
{
    public const long AcceptanceWindowMilliseconds = 50;

    public LegacyHandshakeState(uint token)
    {
        Token = token;
        IsActive = true;
    }

    public uint Token { get; }

    public bool IsActive { get; private set; }

    public long LastHandshakeTime { get; private set; }

    public HandshakePacket Start(long serverTime)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Handshake state is no longer active.");
        }

        LastHandshakeTime = serverTime;
        return new HandshakePacket(Token, unchecked((uint)serverTime), 0);
    }

    public LegacyHandshakeResult Handle(in HandshakePacket packet, long serverTime)
    {
        if (!IsActive)
        {
            return new LegacyHandshakeResult(LegacyHandshakeDecision.RejectedUnexpectedPacket);
        }

        if (packet.HandshakeValue != Token)
        {
            IsActive = false;
            return new LegacyHandshakeResult(LegacyHandshakeDecision.RejectedWrongToken);
        }

        long difference = serverTime - ((long)packet.Time + packet.Delta);
        if (difference is >= 0 and <= AcceptanceWindowMilliseconds)
        {
            IsActive = false;
            return new LegacyHandshakeResult(LegacyHandshakeDecision.Completed);
        }

        long delta = (serverTime - packet.Time) / 2;
        if (delta < 0)
        {
            delta = (serverTime - LastHandshakeTime) / 2;
        }

        LastHandshakeTime = serverTime;
        var response = new HandshakePacket(
            Token,
            unchecked((uint)serverTime),
            unchecked((uint)delta));

        return new LegacyHandshakeResult(LegacyHandshakeDecision.Retry, response);
    }
}
