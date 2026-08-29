using System.Buffers;
using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Receive;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Protocol.Legacy;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class LegacySequenceValidationTests
{
    [TestMethod]
    public void Sequence_state_advances_wraps_and_resets_only_on_matches()
    {
        var profile = new LegacySequenceProfile("test", new byte[] { 0xAA, 0xBB });
        var state = new LegacySequenceState(profile);

        Assert.AreEqual(0xAA, state.Expected);
        Assert.IsFalse(state.TryAccept(0xCC));
        Assert.AreEqual(0, state.Index);
        Assert.AreEqual(0xAA, state.Expected);

        Assert.IsTrue(state.TryAccept(0xAA));
        Assert.AreEqual(1, state.Index);
        Assert.AreEqual(0xBB, state.Expected);

        Assert.IsTrue(state.TryAccept(0xBB));
        Assert.AreEqual(0, state.Index);
        Assert.AreEqual(0xAA, state.Expected);

        Assert.IsTrue(state.TryAccept(0xAA));
        state.Reset();
        Assert.AreEqual(0, state.Index);
    }

    [TestMethod]
    public async Task Correct_sequence_is_validated_before_consumer_and_advances_state()
    {
        var profile = new LegacySequenceProfile("test", new byte[] { 0xAA, 0xBB });
        var session = new GameSession(PacketPhase.Auth, new LegacySequenceState(profile));
        var consumer = new RecordingConsumer();
        var pipe = new Pipe();

        byte[] frame = CreateLoginRequestFrame(0xAA);
        await pipe.Writer.WriteAsync(frame);
        await pipe.Writer.CompleteAsync();

        LegacyReceiveLoopResult result = await LegacyPipeReceiveLoop.RunAsync(
            pipe.Reader,
            session,
            PacketDirection.ClientToServer,
            consumer);

        Assert.AreEqual(LegacyReceiveLoopCompletion.Completed, result.Completion);
        Assert.AreEqual(1L, result.FramesProcessed);
        Assert.AreEqual(1, consumer.Count);
        Assert.AreEqual(1, session.SequenceState!.Index);
        Assert.AreEqual((byte)0xBB, session.SequenceState.Expected);
    }

    [TestMethod]
    public async Task Wrong_sequence_rejects_before_consumer_without_advancing_state()
    {
        var profile = new LegacySequenceProfile("test", new byte[] { 0xAA, 0xBB });
        var session = new GameSession(PacketPhase.Auth, new LegacySequenceState(profile));
        var consumer = new RecordingConsumer();
        var pipe = new Pipe();

        byte[] frame = CreateLoginRequestFrame(0xCC);
        await pipe.Writer.WriteAsync(frame);
        await pipe.Writer.CompleteAsync();

        LegacyReceiveLoopResult result = await LegacyPipeReceiveLoop.RunAsync(
            pipe.Reader,
            session,
            PacketDirection.ClientToServer,
            consumer);

        Assert.AreEqual(LegacyReceiveLoopCompletion.SequenceMismatch, result.Completion);
        Assert.AreEqual(0L, result.FramesProcessed);
        Assert.AreEqual(0, consumer.Count);
        Assert.AreEqual(0, session.SequenceState!.Index);
        Assert.AreEqual((byte)0xAA, session.SequenceState.Expected);
    }

    [TestMethod]
    public async Task Non_sequenced_packet_does_not_consume_sequence_state()
    {
        var profile = new LegacySequenceProfile("test", new byte[] { 0xAA, 0xBB });
        var session = new GameSession(PacketPhase.Handshake, new LegacySequenceState(profile));
        var consumer = new RecordingConsumer();
        var pipe = new Pipe();
        var packet = new Handshake(1, 2, 3);
        var frame = new byte[13];
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, frame, out int written);
        Assert.AreEqual(PacketFrameWriteStatus.Done, status);
        Assert.AreEqual(13, written);

        await pipe.Writer.WriteAsync(frame);
        await pipe.Writer.CompleteAsync();

        LegacyReceiveLoopResult result = await LegacyPipeReceiveLoop.RunAsync(
            pipe.Reader,
            session,
            PacketDirection.ClientToServer,
            consumer);

        Assert.AreEqual(LegacyReceiveLoopCompletion.Completed, result.Completion);
        Assert.AreEqual(1, consumer.Count);
        Assert.AreEqual(0, session.SequenceState!.Index);
    }

    private static byte[] CreateLoginRequestFrame(byte sequence)
    {
        var packet = new LoginRequest(
            "player",
            "password",
            new uint[] { 1, 2, 3, 4 });
        var frame = new byte[66];
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, sequence, frame, out int written);
        Assert.AreEqual(PacketFrameWriteStatus.Done, status);
        Assert.AreEqual(frame.Length, written);
        return frame;
    }

    private sealed class RecordingConsumer : ILegacyFrameConsumer
    {
        public int Count { get; private set; }

        public ValueTask ConsumeAsync(
            GameSession session,
            PacketRegistration registration,
            ReadOnlySequence<byte> payload,
            byte? sequence,
            CancellationToken cancellationToken)
        {
            Count++;
            return ValueTask.CompletedTask;
        }
    }
}
