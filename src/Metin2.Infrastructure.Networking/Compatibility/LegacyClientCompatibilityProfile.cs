using Metin2.Infrastructure.Networking.Security;
using Metin2.Protocol.Legacy;

namespace Metin2.Infrastructure.Networking.Compatibility;

public enum LegacyPacketEncryptionMode : byte
{
    None = 0,
    ClassicTea = 1,
    ImprovedPacketEncryption = 2
}

public sealed class LegacyClientCompatibilityProfile
{
    public LegacyClientCompatibilityProfile(
        string name,
        LegacySequenceProfile sequence,
        LegacyPacketEncryptionMode encryptionMode,
        LegacyTeaSecurityProfile? classicTea = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(sequence);

        if (encryptionMode == LegacyPacketEncryptionMode.ClassicTea && classicTea is null)
        {
            throw new ArgumentException("Classic TEA mode requires a TEA security profile.", nameof(classicTea));
        }

        if (encryptionMode != LegacyPacketEncryptionMode.ClassicTea && classicTea is not null)
        {
            throw new ArgumentException("A TEA security profile is only valid for ClassicTea mode.", nameof(classicTea));
        }

        Name = name;
        Sequence = sequence;
        EncryptionMode = encryptionMode;
        ClassicTea = classicTea;
    }

    public string Name { get; }

    public LegacySequenceProfile Sequence { get; }

    public LegacyPacketEncryptionMode EncryptionMode { get; }

    public LegacyTeaSecurityProfile? ClassicTea { get; }

    /// <summary>
    /// Indicates whether the server has a live transport implementation for the selected mode.
    /// This does not claim that a specific stock client profile is capture-verified or that every
    /// improved cipher selector is supported by the configured provider.
    /// </summary>
    public bool IsEncryptionImplemented =>
        EncryptionMode is
            LegacyPacketEncryptionMode.None or
            LegacyPacketEncryptionMode.ClassicTea or
            LegacyPacketEncryptionMode.ImprovedPacketEncryption;
}
