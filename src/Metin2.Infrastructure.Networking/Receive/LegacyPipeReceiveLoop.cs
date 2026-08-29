using System.Buffers;
using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Protocol.Framing;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Legacy;

namespace Metin2.Infrastructure.Networking.Receive;

public enum LegacyReceiveLoopCompletion : byte
{
    Completed = 0,
    TruncatedFrame = 1,
    ProtocolViolation = 2,
    UnsupportedPacketShape = 3,
    DispatchFailure = 4,
    ConsumerFailure = 5,
    SequenceMismatch = 6
}

public readonly record struct LegacyReceiveLoopResult(
    LegacyReceiveLoopCompletion Completion,
    long FramesProcessed,
    byte? OffendingHeader = null,
    Exception? Exception = null);

public static class LegacyPipeReceiveLoop
{
    public static async ValueTask<LegacyReceiveLoopResult> RunAsync(
        PipeReader reader,
        GameSession session,
        PacketDirection inboundDirection,
        ILegacyFrameConsumer consumer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(consumer);

        long framesProcessed = 0;

        try
        {
            while (true)
            {
                ReadResult readResult = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                ReadOnlySequence<byte> buffer = readResult.Buffer;

                while (!buffer.IsEmpty)
                {
                    LegacyFrameDecodeStatus decodeStatus = LegacyFrameCodec.TryDecode(
                        buffer,
                        inboundDirection,
                        session.Phase,
                        out LegacySequenceFrame frame);

                    if (decodeStatus == LegacyFrameDecodeStatus.Done)
                    {
                        LegacySequenceState? sequenceState = session.SequenceState;
                        if (frame.Registration.HasSequence && sequenceState is not null)
                        {
                            if (!frame.Sequence.HasValue || !sequenceState.TryAccept(frame.Sequence.Value))
                            {
                                reader.AdvanceTo(buffer.Start, buffer.End);
                                return new LegacyReceiveLoopResult(
                                    LegacyReceiveLoopCompletion.SequenceMismatch,
                                    framesProcessed,
                                    TryPeekHeader(buffer));
                            }
                        }

                        try
                        {
                            await consumer.ConsumeAsync(
                                session,
                                frame.Registration,
                                frame.Payload,
                                frame.Sequence,
                                cancellationToken).ConfigureAwait(false);
                        }
                        catch (PacketDispatchException exception)
                        {
                            reader.AdvanceTo(buffer.Start, buffer.End);
                            return new LegacyReceiveLoopResult(
                                LegacyReceiveLoopCompletion.DispatchFailure,
                                framesProcessed,
                                TryPeekHeader(buffer),
                                exception);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            reader.AdvanceTo(buffer.Start, buffer.End);
                            return new LegacyReceiveLoopResult(
                                LegacyReceiveLoopCompletion.ConsumerFailure,
                                framesProcessed,
                                TryPeekHeader(buffer),
                                exception);
                        }

                        buffer = buffer.Slice(frame.FrameSize);
                        framesProcessed++;
                        continue;
                    }

                    if (decodeStatus == LegacyFrameDecodeStatus.NeedMoreData)
                    {
                        if (readResult.IsCompleted)
                        {
                            reader.AdvanceTo(buffer.Start, buffer.End);
                            return new LegacyReceiveLoopResult(
                                LegacyReceiveLoopCompletion.TruncatedFrame,
                                framesProcessed,
                                TryPeekHeader(buffer));
                        }

                        break;
                    }

                    reader.AdvanceTo(buffer.Start, buffer.End);
                    return new LegacyReceiveLoopResult(
                        decodeStatus == LegacyFrameDecodeStatus.UnsupportedPacketShape
                            ? LegacyReceiveLoopCompletion.UnsupportedPacketShape
                            : LegacyReceiveLoopCompletion.ProtocolViolation,
                        framesProcessed,
                        TryPeekHeader(buffer));
                }

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (readResult.IsCompleted)
                {
                    return new LegacyReceiveLoopResult(
                        LegacyReceiveLoopCompletion.Completed,
                        framesProcessed);
                }
            }
        }
        finally
        {
            await reader.CompleteAsync().ConfigureAwait(false);
        }
    }

    private static byte? TryPeekHeader(in ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);
        return reader.TryRead(out byte header) ? header : null;
    }
}
