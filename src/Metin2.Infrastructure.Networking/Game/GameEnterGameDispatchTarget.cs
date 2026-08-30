using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.World;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Game;

public sealed class GameEnterGameDispatchTarget(
    GameSession session,
    PipeWriter output,
    PlayerRuntimeRegistry runtimeRegistry,
    IServerTimeProvider timeProvider,
    byte channelNo)
{
    private const int PhaseFrameSize = 1 + PhaseCodec.PayloadSize;
    private const int GameTimeFrameSize = 1 + GameTimeCodec.PayloadSize;
    private const int ChannelFrameSize = 1 + ChannelCodec.PayloadSize;
    private const int TotalFrameSize = PhaseFrameSize + GameTimeFrameSize + ChannelFrameSize;

    public async ValueTask HandleAsync(EnterGame packet, CancellationToken cancellationToken)
    {
        if (session.Phase != PacketPhase.Loading ||
            session.SelectedCharacterId is not CharacterId characterId ||
            session.RuntimeEntityId is not EntityId entityId ||
            !runtimeRegistry.TryGet(entityId, out PlayerRuntimeReservation reservation) ||
            reservation.CharacterId != characterId)
        {
            throw new InvalidOperationException("EnterGame requires a valid selected-character runtime reservation.");
        }

        var phase = new Phase((byte)LegacyPhaseCode.Game);
        var gameTime = new GameTime(unchecked((uint)timeProvider.GetMilliseconds()));
        var channel = new Channel(channelNo);

        Memory<byte> memory = output.GetMemory(TotalFrameSize);
        Span<byte> destination = memory.Span;
        int offset = 0;

        Write(in phase, destination[offset..], PhaseFrameSize, ref offset);
        Write(in gameTime, destination[offset..], GameTimeFrameSize, ref offset);
        Write(in channel, destination[offset..], ChannelFrameSize, ref offset);

        output.Advance(offset);
        FlushResult flush = await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        if (flush.IsCanceled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        session.TransitionTo(PacketPhase.Game);
    }

    private static void Write(in Phase packet, Span<byte> destination, int expected, ref int offset)
    {
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, destination, out int written);
        Ensure(status, written, expected, nameof(Phase));
        offset += written;
    }

    private static void Write(in GameTime packet, Span<byte> destination, int expected, ref int offset)
    {
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, destination, out int written);
        Ensure(status, written, expected, nameof(GameTime));
        offset += written;
    }

    private static void Write(in Channel packet, Span<byte> destination, int expected, ref int offset)
    {
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, destination, out int written);
        Ensure(status, written, expected, nameof(Channel));
        offset += written;
    }

    private static void Ensure(PacketFrameWriteStatus status, int written, int expected, string packetName)
    {
        if (status != PacketFrameWriteStatus.Done || written != expected)
        {
            throw new InvalidOperationException(
                $"{packetName} frame could not be written: {status} ({written}/{expected} bytes)." );
        }
    }
}
