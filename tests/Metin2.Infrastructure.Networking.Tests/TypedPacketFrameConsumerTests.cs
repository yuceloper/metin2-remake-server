using System.Buffers.Binary;
using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Receive;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class TypedPacketFrameConsumerTests
{
    [TestMethod]
    public async Task Fragmented_handshake_reaches_typed_target_end_to_end()
    {
        var pipe = new Pipe();
        var session = new GameSession(PacketPhase.Handshake);
        var target = new RecordingTarget();
        var consumer = new TypedPacketFrameConsumer(target);

        ValueTask<LegacyReceiveLoopResult> receive = LegacyPipeReceiveLoop.RunAsync(
            pipe.Reader,
            session,
            PacketDirection.ClientToServer,
            consumer);

        byte[] frame = CreateHandshakeFrame(1, 2, 3);
        await pipe.Writer.WriteAsync(frame.AsMemory(0, 4));
        await pipe.Writer.WriteAsync(frame.AsMemory(4));
        await pipe.Writer.CompleteAsync();

        LegacyReceiveLoopResult result = await receive;

        Assert.AreEqual(LegacyReceiveLoopCompletion.Completed, result.Completion);
        Assert.AreEqual(1L, result.FramesProcessed);
        Assert.AreEqual(nameof(Handshake), target.LastPacketName);
        Assert.AreEqual(1u, target.HandshakePacket.HandshakeValue);
        Assert.AreEqual(2u, target.HandshakePacket.Time);
        Assert.AreEqual(3u, target.HandshakePacket.Delta);
    }

    [TestMethod]
    public async Task Handler_completion_is_awaited_before_frame_is_counted()
    {
        var pipe = new Pipe();
        var session = new GameSession(PacketPhase.Handshake);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var target = new RecordingTarget(gate.Task);
        var consumer = new TypedPacketFrameConsumer(target);

        ValueTask<LegacyReceiveLoopResult> receive = LegacyPipeReceiveLoop.RunAsync(
            pipe.Reader,
            session,
            PacketDirection.ClientToServer,
            consumer);

        await pipe.Writer.WriteAsync(CreateHandshakeFrame(4, 5, 6));
        await pipe.Writer.CompleteAsync();

        await target.Invoked.Task;
        Assert.IsFalse(receive.IsCompleted);

        gate.SetResult();
        LegacyReceiveLoopResult result = await receive;

        Assert.AreEqual(LegacyReceiveLoopCompletion.Completed, result.Completion);
        Assert.AreEqual(1L, result.FramesProcessed);
    }

    [TestMethod]
    public async Task Handler_exception_becomes_consumer_failure_without_counting_frame()
    {
        var pipe = new Pipe();
        var session = new GameSession(PacketPhase.Handshake);
        var target = new RecordingTarget(exception: new InvalidOperationException("handler failed"));
        var consumer = new TypedPacketFrameConsumer(target);

        ValueTask<LegacyReceiveLoopResult> receive = LegacyPipeReceiveLoop.RunAsync(
            pipe.Reader,
            session,
            PacketDirection.ClientToServer,
            consumer);

        await pipe.Writer.WriteAsync(CreateHandshakeFrame(7, 8, 9));
        await pipe.Writer.CompleteAsync();

        LegacyReceiveLoopResult result = await receive;

        Assert.AreEqual(LegacyReceiveLoopCompletion.ConsumerFailure, result.Completion);
        Assert.AreEqual(0L, result.FramesProcessed);
        Assert.AreEqual((byte)0xFF, result.OffendingHeader);
        Assert.IsInstanceOfType<InvalidOperationException>(result.Exception);
    }

    private static byte[] CreateHandshakeFrame(uint handshake, uint time, uint delta)
    {
        var frame = new byte[1 + HandshakeCodec.PayloadSize];
        frame[0] = 0xFF;
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(1, 4), handshake);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(5, 4), time);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(9, 4), delta);
        return frame;
    }

    private sealed class RecordingTarget : IPacketDispatchTarget
    {
        private readonly Task? _completion;
        private readonly Exception? _exception;

        public RecordingTarget(Task? completion = null, Exception? exception = null)
        {
            _completion = completion;
            _exception = exception;
        }

        public TaskCompletionSource Invoked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? LastPacketName { get; private set; }
        public Handshake HandshakePacket { get; private set; }

        public ValueTask HandleAsync(Handshake packet, CancellationToken cancellationToken)
        {
            LastPacketName = nameof(Handshake);
            HandshakePacket = packet;
            Invoked.TrySetResult();

            if (_exception is not null)
            {
                return ValueTask.FromException(_exception);
            }

            return _completion is null ? ValueTask.CompletedTask : new ValueTask(_completion);
        }

        public ValueTask HandleAsync(LoginFailed packet, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask HandleAsync(LoginRequest packet, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask HandleAsync(LoginSuccess packet, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask HandleAsync(Phase packet, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask HandleAsync(TokenLogin packet, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
