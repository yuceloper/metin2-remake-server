namespace Metin2.Shared.Results;

public readonly record struct Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public bool IsNone => string.IsNullOrEmpty(Code);

    public override string ToString() => IsNone ? "None" : $"{Code}: {Description}";
}
