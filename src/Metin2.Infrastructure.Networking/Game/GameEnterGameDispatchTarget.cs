using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.World;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Protocol.Legacy;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Game;

public sealed class GameEnterGameDispatchTarget(
    GameSession session,
    PipeWriter output,
    PlayerRuntimeRegistry runtimeRegistry,
    IServerTimeProvider timeProvider,
    byte channelNumber = 1)
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
            reservation.CharacterId != characterId ||
            runtimeRegistry.IsSpawned(entityId))
        {
            throw new EnterGameRejectedException();
        }

        var phase = new Phase((byte)LegacyPhaseCode.Game);
        var gameTime = new GameTime(checked((uint)timeProvider.GetMilliseconds()));
        var channel = new Channel(channelNumber);

        Memory<byte> memory = output.GetMemory(TotalFrameSize);
        Span<byte> destination = memory.Span;
        int offset = 0;

        PacketFrameWriteStatus phaseStatus = PacketFrameWriter.TryWrite(in phase, destination[offset..], out int phaseWritten);
        EnsureWritten(nameof(Phase), phaseStatus, phaseWritten, PhaseFrameSize);
        offset += phaseWritten;

        PacketFrameWriteStatus timeStatus = PacketFrameWriter.TryWrite(in gameTime, destination[offset..], out int timeWritten);
        EnsureWritten(nameof(GameTime), timeStatus, timeWritten, GameTimeFrameSize);
        offset += timeWritten;

        PacketFrameWriteStatus channelStatus = PacketFrameWriter.TryWrite(in channel, destination[offset..], out int channelWritten);
        EnsureWritten(nameof(Channel), channelStatus, channelWritten, ChannelFrameSize);
        offset += channelWritten;

        output.Advance(offset);
        FlushResult flush = await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        if (flush.IsCanceled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (!runtimeRegistry.TryPromoteToSpawned(entityId, characterId))
        {
            throw new InvalidOperationException("Runtime reservation could not be promoted after EnterGame publication.");
        }

        session.TransitionTo(PacketPhase.Game);
    }

    private static void EnsureWritten(string packetName, PacketFrameWriteStatus status, int written, int expected)
    {
        if (status != PacketFrameWriteStatus.Done || written != expected)
        {
            throw new InvalidOperationException($"{packetName} frame could not be written: {status} ({written}/{expected} bytes)." );
        }
    }
}

public sealed class EnterGameRejectedException : Exception
{
    public EnterGameRejectedException()
        : base("EnterGame was rejected because the session has no valid Loading runtime reservation.")
    {
    }
}
