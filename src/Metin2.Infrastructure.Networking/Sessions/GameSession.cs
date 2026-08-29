using Metin2.Protocol.Generated;
using Metin2.Protocol.Legacy;

namespace Metin2.Infrastructure.Networking.Sessions;

public sealed class GameSession
{
    public GameSession(
        PacketPhase initialPhase = PacketPhase.Handshake,
        LegacySequenceState? sequenceState = null)
    {
        Phase = initialPhase;
        SequenceState = sequenceState;
    }

    public PacketPhase Phase { get; private set; }

    public LegacySequenceState? SequenceState { get; private set; }

    public void TransitionTo(PacketPhase nextPhase)
    {
        Phase = nextPhase;
    }

    public void ConfigureSequence(LegacySequenceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        SequenceState = new LegacySequenceState(profile);
    }

    public void ClearSequence() => SequenceState = null;
}
