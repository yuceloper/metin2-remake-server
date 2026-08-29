using Metin2.Protocol.Generated;

namespace Metin2.Infrastructure.Networking.Sessions;

public sealed class GameSession
{
    public GameSession(PacketPhase initialPhase = PacketPhase.Handshake)
    {
        Phase = initialPhase;
    }

    public PacketPhase Phase { get; private set; }

    public void TransitionTo(PacketPhase nextPhase)
    {
        Phase = nextPhase;
    }
}
