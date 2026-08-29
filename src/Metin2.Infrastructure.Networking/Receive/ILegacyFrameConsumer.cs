using System.Buffers;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Protocol.Generated;

namespace Metin2.Infrastructure.Networking.Receive;

public interface ILegacyFrameConsumer
{
    ValueTask ConsumeAsync(
        GameSession session,
        PacketRegistration registration,
        ReadOnlySequence<byte> payload,
        byte? sequence,
        CancellationToken cancellationToken);
}
