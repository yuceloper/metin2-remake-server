using System.Buffers;
using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Receive;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Protocol.Generated;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class LegacyPipeReceiveLoopTests
{
    [TestMethod]
    public async Task FragmentedHandshake_IsDecodedAfterSecondFlush()
    {
        var pipe = new Pipe();
        var session = new GameSession();
        var consumer = new RecordingConsumer();

        Task<LegacyReceiveLoopResult> receiveTask = LegacyPipeReceiveLoop.RunAsync(
            pipe.Reader,
            session,
            PacketDirection.ClientToServer,
            consumer).AsTask();

        byte[] frame = CreateHandshakeFrame(0x12345678);
        await pipe.Writer.WriteAsync(frame.AsMemory(0, 5));
        await pipe.Writer.FlushAsync();
        await pipe.Writer.WriteAsync(frame.AsMemory(5));
        await pipe.Writer.CompleteAsync();

        LegacyReceiveLoopResult result = await receiveTask;

        Assert.AreEqual(LegacyReceiveLoopCompletion.Completed, result.Completion);
        Assert.AreEqual(1L, result.FramesProcessed);
        CollectionAssert.AreEqual(new[] { PacketId.Handshake }, consumer.PacketIds);
        Assert.AreEqual(12, consumer.Payloads.Single().Length);
    }

    [TestMethod]
    public async Task MultipleFramesInSingleBuffer_AreDrainedInOrder()
    {
        var pipe = new Pipe();
        var session = new GameSession();
        var consumer = new RecordingConsumer();

        Task<LegacyReceiveLoopResult> receiveTask = LegacyPipeReceiveLoop.RunAsync(
            pipe.Reader,
            session,
            PacketDirection.ClientToServer,
            consumer).AsTask();

        byte[] first = CreateHandshakeFrame(1);
        byte[] second = CreateHandshakeFrame(2);
        byte[] combined = new byte[first.Length + second.Length];
        first.CopyTo(combined, 0);
        second.CopyTo(combined, first.Length);

        await pipe.Writer.WriteAsync(combined);
        await pipe.Writer.CompleteAsync();

        LegacyReceiveLoopResult result = await receiveTask;

        Assert.AreEqual(LegacyReceiveLoopCompletion.Completed, result.Completion);
        Assert.AreEqual(2L, result.FramesProcessed);
        CollectionAssert.AreEqual(
            new[] { PacketId.Handshake, PacketId.Handshake },
            consumer.PacketIds);
    }

    [TestMethod]
    public async Task CompletedPipeWithPartialFrame_ReturnsTruncatedFrame()
    {
        var pipe = new Pipe();
        var session = new GameSession();
        var consumer = new RecordingConsumer();

        Task<LegacyReceiveLoopResult> receiveTask = LegacyPipeReceiveLoop.RunAsync(
            pipe.Reader,
            session,
            PacketDirection.ClientToServer,
            consumer).AsTask();

        byte[] partial = CreateHandshakeFrame(1)[..5];
        await pipe.Writer.WriteAsync(partial);
        await pipe.Writer.CompleteAsync();

        LegacyReceiveLoopResult result = await receiveTask;

        Assert.AreEqual(LegacyReceiveLoopCompletion.TruncatedFrame, result.Completion);
        Assert.AreEqual(0L, result.FramesProcessed);
        Assert.AreEqual((byte)0xFF, result.OffendingHeader);
        Assert.AreEqual(0, consumer.PacketIds.Count);
    }

    [TestMethod]
    public async Task PacketInvalidForCurrentPhase_ReturnsProtocolViolation()
    {
        var pipe = new Pipe();
        var session = new GameSession(PacketPhase.Handshake);
        var consumer = new RecordingConsumer();

        Task<LegacyReceiveLoopResult> receiveTask = LegacyPipeReceiveLoop.RunAsync(
            pipe.Reader,
            session,
            PacketDirection.ClientToServer,
            consumer).AsTask();

        byte[] loginRequest = new byte[66];
        loginRequest[0] = 0x6F;
        loginRequest[^1] = 0x01;
        await pipe.Writer.WriteAsync(loginRequest);
        await pipe.Writer.CompleteAsync();

        LegacyReceiveLoopResult result = await receiveTask;

        Assert.AreEqual(LegacyReceiveLoopCompletion.ProtocolViolation, result.Completion);
        Assert.AreEqual((byte)0x6F, result.OffendingHeader);
        Assert.AreEqual(0L, result.FramesProcessed);
    }

    [TestMethod]
    public async Task ConsumerCanTransitionPhaseBeforeNextBufferedFrame()
    {
        var pipe = new Pipe();
        var session = new GameSession(PacketPhase.Handshake);
        var consumer = new PhaseTransitionConsumer();

        Task<LegacyReceiveLoopResult> receiveTask = LegacyPipeReceiveLoop.RunAsync(
            pipe.Reader,
            session,
            PacketDirection.ClientToServer,
            consumer).AsTask();

        byte[] handshake = CreateHandshakeFrame(1);
        byte[] tokenLogin = new byte[53];
        tokenLogin[0] = 0x6D;
        tokenLogin[^1] = 0x05;
        byte[] combined = new byte[handshake.Length + tokenLogin.Length];
        handshake.CopyTo(combined, 0);
        tokenLogin.CopyTo(combined, handshake.Length);

        await pipe.Writer.WriteAsync(combined);
        await pipe.Writer.CompleteAsync();

        LegacyReceiveLoopResult result = await receiveTask;

        Assert.AreEqual(LegacyReceiveLoopCompletion.Completed, result.Completion);
        Assert.AreEqual(2L, result.FramesProcessed);
        Assert.AreEqual(PacketPhase.Login, session.Phase);
        CollectionAssert.AreEqual(
            new[] { PacketId.Handshake, PacketId.TokenLogin },
            consumer.PacketIds);
    }

    private static byte[] CreateHandshakeFrame(uint token)
    {
        byte[] frame = new byte[13];
        frame[0] = 0xFF;
        frame[1] = (byte)token;
        frame[2] = (byte)(token >> 8);
        frame[3] = (byte)(token >> 16);
        frame[4] = (byte)(token >> 24);
        return frame;
    }

    private sealed class RecordingConsumer : ILegacyFrameConsumer
    {
        public List<PacketId> PacketIds { get; } = new();

        public List<byte[]> Payloads { get; } = new();

        public ValueTask ConsumeAsync(
            GameSession session,
            PacketRegistration registration,
            ReadOnlySequence<byte> payload,
            byte? sequence,
            CancellationToken cancellationToken)
        {
            PacketIds.Add(registration.Id);
            Payloads.Add(payload.ToArray());
            return ValueTask.CompletedTask;
        }
    }

    private sealed class PhaseTransitionConsumer : ILegacyFrameConsumer
    {
        public List<PacketId> PacketIds { get; } = new();

        public ValueTask ConsumeAsync(
            GameSession session,
            PacketRegistration registration,
            ReadOnlySequence<byte> payload,
            byte? sequence,
            CancellationToken cancellationToken)
        {
            PacketIds.Add(registration.Id);
            if (registration.Id == PacketId.Handshake)
            {
                session.TransitionTo(PacketPhase.Login);
            }

            return ValueTask.CompletedTask;
        }
    }
}
