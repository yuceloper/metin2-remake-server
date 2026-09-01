using Metin2.Infrastructure.Networking.Security;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Legacy;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Sessions;

public sealed class GameSession
{
    private uint[]? _clientSecurityKey;

    public GameSession(
        PacketPhase initialPhase = PacketPhase.Handshake,
        LegacySequenceState? sequenceState = null,
        LegacyTeaSecurityState? teaSecurityState = null)
    {
        Phase = initialPhase;
        SequenceState = sequenceState;
        TeaSecurityState = teaSecurityState;
    }

    public PacketPhase Phase { get; private set; }

    public LegacySequenceState? SequenceState { get; private set; }

    public LegacyTeaSecurityState? TeaSecurityState { get; private set; }

    public bool IsAuthenticated => AccountId.HasValue;

    public AccountId? AccountId { get; private set; }

    public string? Username { get; private set; }

    public CharacterId? SelectedCharacterId { get; private set; }

    public EntityId? RuntimeEntityId { get; private set; }

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

    public void ConfigureClassicTeaSecurity(LegacyTeaSecurityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (TeaSecurityState is not null)
        {
            throw new InvalidOperationException("Legacy TEA security is already configured for this session.");
        }

        var state = new LegacyTeaSecurityState();
        state.ActivateInitial(profile);
        TeaSecurityState = state;
    }

    public void RotateClassicTeaSecurity(ReadOnlySpan<uint> clientSecurityKey, LegacyTeaSecurityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        LegacyTeaSecurityState state = TeaSecurityState
            ?? throw new InvalidOperationException("Legacy TEA security must be configured before client key rotation.");
        state.RotateFromClientKey(clientSecurityKey, profile);
    }

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

    public void BindRuntimeEntity(EntityId entityId)
    {
        if (!SelectedCharacterId.HasValue)
        {
            throw new InvalidOperationException("A runtime entity cannot be bound before character selection.");
        }

        if (entityId.Value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entityId), "Runtime entity id 0 is reserved.");
        }

        if (RuntimeEntityId.HasValue)
        {
            throw new InvalidOperationException("A runtime entity is already bound to this session.");
        }

        RuntimeEntityId = entityId;
    }

    public EntityId? ClearRuntimeEntity()
    {
        EntityId? previous = RuntimeEntityId;
        RuntimeEntityId = null;
        return previous;
    }
}
