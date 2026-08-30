using Metin2.Protocol.Generated;
using Metin2.Protocol.Legacy;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Sessions;

public sealed class GameSession
{
    private uint[]? _clientSecurityKey;

    public GameSession(
        PacketPhase initialPhase = PacketPhase.Handshake,
        LegacySequenceState? sequenceState = null)
    {
        Phase = initialPhase;
        SequenceState = sequenceState;
    }

    public PacketPhase Phase { get; private set; }

    public LegacySequenceState? SequenceState { get; private set; }

    public bool IsAuthenticated => AccountId.HasValue;

    public AccountId? AccountId { get; private set; }

    public string? Username { get; private set; }

    public CharacterId? SelectedCharacterId { get; private set; }

    public ReadOnlyMemory<uint> ClientSecurityKey => _clientSecurityKey ?? ReadOnlyMemory<uint>.Empty;

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

    public void Authenticate(AccountId accountId, string username, ReadOnlySpan<uint> clientSecurityKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        if (clientSecurityKey.Length != 4)
        {
            throw new ArgumentException("Legacy client security key must contain exactly four uint32 values.", nameof(clientSecurityKey));
        }

        if (IsAuthenticated)
        {
            throw new InvalidOperationException("Session is already authenticated.");
        }

        AccountId = accountId;
        Username = username;
        _clientSecurityKey = clientSecurityKey.ToArray();
    }

    public void SelectCharacter(CharacterId characterId)
    {
        if (!IsAuthenticated)
        {
            throw new InvalidOperationException("A character cannot be selected before authentication.");
        }

        if (SelectedCharacterId.HasValue)
        {
            throw new InvalidOperationException("A character is already selected for this session.");
        }

        SelectedCharacterId = characterId;
    }
}
