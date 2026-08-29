namespace Metin2.Protocol.Legacy;

public sealed class LegacySequenceProfile
{
    private readonly byte[] _table;

    public LegacySequenceProfile(string name, ReadOnlySpan<byte> table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (table.IsEmpty)
        {
            throw new ArgumentException("Sequence table must contain at least one byte.", nameof(table));
        }

        Name = name;
        _table = table.ToArray();
    }

    public string Name { get; }

    public int Length => _table.Length;

    public byte this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _table.Length);
            return _table[index];
        }
    }
}
