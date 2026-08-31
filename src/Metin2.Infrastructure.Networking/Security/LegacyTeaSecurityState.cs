namespace Metin2.Infrastructure.Networking.Security;

public enum LegacyTeaSecurityStage : byte
{
    Plaintext = 0,
    InitialKey = 1,
    RotatedClientKey = 2
}

public sealed class LegacyTeaSecurityState
{
    private uint[]? _decryptionKey;
    private uint[]? _encryptionKey;

    public LegacyTeaSecurityStage Stage { get; private set; }

    public bool IsActive => Stage != LegacyTeaSecurityStage.Plaintext;

    public ReadOnlyMemory<uint> DecryptionKey => _decryptionKey ?? ReadOnlyMemory<uint>.Empty;

    public ReadOnlyMemory<uint> EncryptionKey => _encryptionKey ?? ReadOnlyMemory<uint>.Empty;

    public void ActivateInitial(LegacyTeaSecurityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (Stage != LegacyTeaSecurityStage.Plaintext)
        {
            throw new InvalidOperationException("Initial legacy TEA key can only be activated from plaintext state.");
        }

        _decryptionKey = new uint[LegacyTeaCipher.KeyWordCount];
        _encryptionKey = new uint[LegacyTeaCipher.KeyWordCount];
        profile.GetInitialTransportKey(_decryptionKey);
        profile.GetInitialTransportKey(_encryptionKey);
        Stage = LegacyTeaSecurityStage.InitialKey;
    }

    public void RotateFromClientKey(ReadOnlySpan<uint> clientEncryptionKey, LegacyTeaSecurityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (Stage != LegacyTeaSecurityStage.InitialKey)
        {
            throw new InvalidOperationException("Client-derived legacy TEA keys can only replace the initial transport key.");
        }

        if (clientEncryptionKey.Length != LegacyTeaCipher.KeyWordCount)
        {
            throw new ArgumentException("Client encryption key must contain exactly four uint32 words.", nameof(clientEncryptionKey));
        }

        _decryptionKey = clientEncryptionKey.ToArray();
        _encryptionKey = new uint[LegacyTeaCipher.KeyWordCount];
        profile.DeriveServerEncryptionKey(clientEncryptionKey, _encryptionKey);
        Stage = LegacyTeaSecurityStage.RotatedClientKey;
    }
}
