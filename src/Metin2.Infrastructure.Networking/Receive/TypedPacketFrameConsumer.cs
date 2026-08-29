using System.Buffers;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Protocol.Generated;

namespace Metin2.Infrastructure.Networking.Receive;

public sealed class TypedPacketFrameConsumer : ILegacyFrameConsumer
{
    private readonly IPacketDispatchTarget _target;

    public TypedPacketFrameConsumer(IPacketDispatchTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _target = target;
    }

    public ValueTask ConsumeAsync(
        GameSession session,
        PacketRegistration registration,
        ReadOnlySequence<byte> payload,
        byte? sequence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        PacketDispatchAttempt attempt = PacketDispatcher.Dispatch(
            registration,
            payload,
            _target,
            cancellationToken);

        return attempt.Status == PacketDispatchStatus.Done
            ? attempt.HandlerCompletion
            : ValueTask.FromException(new PacketDispatchException(registration.Id, attempt.Status));
    }
}

public sealed class PacketDispatchException : Exception
{
    public PacketDispatchException(PacketId packetId, PacketDispatchStatus status)
        : base($"Packet '{packetId}' could not be dispatched: {status}.")
    {
        PacketId = packetId;
        Status = status;
    }

    public PacketId PacketId { get; }

    public PacketDispatchStatus Status { get; }
}
