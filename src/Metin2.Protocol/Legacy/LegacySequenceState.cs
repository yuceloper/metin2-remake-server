namespace Metin2.Protocol.Legacy;

public sealed class LegacySequenceState
{
    private readonly LegacySequenceProfile _profile;

    public LegacySequenceState(LegacySequenceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _profile = profile;
    }

    public LegacySequenceProfile Profile => _profile;

    public int Index { get; private set; }

    public byte Expected => _profile[Index];

    public bool TryAccept(byte received)
    {
        if (received != Expected)
        {
            return false;
        }

        Index++;
        if (Index == _profile.Length)
        {
            Index = 0;
        }

        return true;
    }

    public void Reset() => Index = 0;
}
