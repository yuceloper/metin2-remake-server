namespace Metin2.Shared.Identity;

public readonly record struct EntityId(uint Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
