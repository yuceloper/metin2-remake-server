namespace Metin2.Shared.Identity;

public readonly record struct ItemId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
