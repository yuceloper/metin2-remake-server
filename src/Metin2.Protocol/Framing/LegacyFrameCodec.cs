using System.Buffers;
using Metin2.Protocol.Generated;

namespace Metin2.Protocol.Framing;

public static class LegacyFrameCodec
{
    public static LegacyFrameDecodeStatus TryDecode(
        ReadOnlySpan<byte> source,
        PacketDirection direction,
        PacketPhase phase,
        out LegacyFrame frame)
    {
        frame = default;

        if (source.IsEmpty)
        {
            return LegacyFrameDecodeStatus.NeedMoreData;
        }

        byte header = source[0];
        if (!TryResolveRegistration(header, direction, phase, out PacketRegistration registration))
        {
            return LegacyFrameDecodeStatus.UnknownPacket;
        }

        LegacyFrameDecodeStatus sizeStatus = TryGetFixedFrameSize(registration, out int frameSize);
        if (sizeStatus != LegacyFrameDecodeStatus.Done)
        {
            return sizeStatus;
        }

        if (source.Length < frameSize)
        {
            return LegacyFrameDecodeStatus.NeedMoreData;
        }

        ReadOnlySpan<byte> payload = source.Slice(1, registration.PayloadSize);
        byte? sequence = registration.HasSequence ? source[frameSize - 1] : null;
        frame = new LegacyFrame(registration, payload, sequence, frameSize);
        return LegacyFrameDecodeStatus.Done;
    }

    public static LegacyFrameDecodeStatus TryDecode(
        in ReadOnlySequence<byte> source,
        PacketDirection direction,
        PacketPhase phase,
        out LegacySequenceFrame frame)
    {
        frame = default;

        var reader = new SequenceReader<byte>(source);
        if (!reader.TryRead(out byte header))
        {
            return LegacyFrameDecodeStatus.NeedMoreData;
        }

        if (!TryResolveRegistration(header, direction, phase, out PacketRegistration registration))
        {
            return LegacyFrameDecodeStatus.UnknownPacket;
        }

        LegacyFrameDecodeStatus sizeStatus = TryGetFixedFrameSize(registration, out int frameSize);
        if (sizeStatus != LegacyFrameDecodeStatus.Done)
        {
            return sizeStatus;
        }

        if (source.Length < frameSize)
        {
            return LegacyFrameDecodeStatus.NeedMoreData;
        }

        ReadOnlySequence<byte> payload = source.Slice(1, registration.PayloadSize);
        byte? sequence = null;
        if (registration.HasSequence)
        {
            var sequenceReader = new SequenceReader<byte>(source.Slice(1L + registration.PayloadSize, 1));
            if (!sequenceReader.TryRead(out byte sequenceValue))
            {
                return LegacyFrameDecodeStatus.NeedMoreData;
            }

            sequence = sequenceValue;
        }

        frame = new LegacySequenceFrame(registration, payload, sequence, frameSize);
        return LegacyFrameDecodeStatus.Done;
    }

    public static LegacyFrameEncodeStatus TryEncode(
        in PacketRegistration registration,
        ReadOnlySpan<byte> payload,
        byte? sequence,
        Span<byte> destination,
        out int written)
    {
        written = 0;

        if (!registration.HasFixedPayloadSize || registration.PayloadSize < 0)
        {
            return LegacyFrameEncodeStatus.UnsupportedPacketShape;
        }

        if (registration.Opcode > byte.MaxValue)
        {
            return LegacyFrameEncodeStatus.UnsupportedOpcode;
        }

        if (payload.Length != registration.PayloadSize)
        {
            return LegacyFrameEncodeStatus.InvalidPayloadLength;
        }

        if (registration.HasSequence && sequence is null)
        {
            return LegacyFrameEncodeStatus.MissingSequence;
        }

        if (!registration.HasSequence && sequence is not null)
        {
            return LegacyFrameEncodeStatus.UnexpectedSequence;
        }

        int frameSize = checked(1 + registration.PayloadSize + (registration.HasSequence ? 1 : 0));
        if (destination.Length < frameSize)
        {
            return LegacyFrameEncodeStatus.InsufficientDestination;
        }

        destination[0] = (byte)registration.Opcode;
        payload.CopyTo(destination.Slice(1, registration.PayloadSize));

        if (registration.HasSequence)
        {
            destination[frameSize - 1] = sequence!.Value;
        }

        written = frameSize;
        return LegacyFrameEncodeStatus.Done;
    }

    private static bool TryResolveRegistration(
        byte header,
        PacketDirection direction,
        PacketPhase phase,
        out PacketRegistration registration) =>
        PacketRegistry.TryGet(header, direction, phase, out registration);

    private static LegacyFrameDecodeStatus TryGetFixedFrameSize(
        in PacketRegistration registration,
        out int frameSize)
    {
        frameSize = 0;
        if (!registration.HasFixedPayloadSize || registration.PayloadSize < 0)
        {
            return LegacyFrameDecodeStatus.UnsupportedPacketShape;
        }

        frameSize = checked(1 + registration.PayloadSize + (registration.HasSequence ? 1 : 0));
        return LegacyFrameDecodeStatus.Done;
    }
}
