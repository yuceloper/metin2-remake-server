using System.Buffers;
using Metin2.Protocol.Generated;

namespace Metin2.Protocol.Framing;

public readonly record struct LegacySequenceFrame(
    PacketRegistration Registration,
    ReadOnlySequence<byte> Payload,
    byte? Sequence,
    long FrameSize);
