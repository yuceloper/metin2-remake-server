using Metin2.Protocol.Generated;

namespace Metin2.Protocol.Framing;

public readonly ref struct LegacyFrame
{
    public LegacyFrame(PacketRegistration registration, ReadOnlySpan<byte> payload, byte? sequence, int frameSize)
    {
        Registration = registration;
        Payload = payload;
        Sequence = sequence;
        FrameSize = frameSize;
    }

    public PacketRegistration Registration { get; }

    public ReadOnlySpan<byte> Payload { get; }

    public byte? Sequence { get; }

    public int FrameSize { get; }
}

public enum LegacyFrameDecodeStatus : byte
{
    Done = 0,
    NeedMoreData = 1,
    UnknownPacket = 2,
    UnsupportedPacketShape = 3
}

public enum LegacyFrameEncodeStatus : byte
{
    Done = 0,
    UnsupportedPacketShape = 1,
    UnsupportedOpcode = 2,
    InvalidPayloadLength = 3,
    MissingSequence = 4,
    UnexpectedSequence = 5,
    InsufficientDestination = 6
}
