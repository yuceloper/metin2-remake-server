namespace Metin2.Infrastructure.Networking.Security;

public sealed class LegacyTeaSecurityState
{
    private uint[]? _decryptionKey;
    private uint[]? _encryptionKey;

    public bool IsPrepared => _decryptionKey is not null && _encryptionKey is not null;

    public bool IsActive { get; private set; }

    public ReadOnlyMemory<uint> DecryptionKey => _decryptionKey ?? ReadOnlyMemory<uint>.Empty;

    public ReadOnlyMemory<uint> EncryptionKey => _encryptionKey ?? ReadOnlyMemory<uint>.Empty;

    public void Prepare(ReadOnlySpan<uint> clientEncryptionKey, LegacyTeaSecurityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (IsPrepared)
        {
            throw new InvalidOperationException("Legacy TEA security state is already prepared.");
        }

        if (clientEncryptionKey.Length != LegacyTeaCipher.KeyWordCount)
        {
            throw new ArgumentException("Client encryption key must contain exactly four uint32 words.", nameof(clientEncryptionKey));
        }

        _decryptionKey = clientEncryptionKey.ToArray();
        _encryptionKey = new uint[LegacyTeaCipher.KeyWordCount];
        profile.DeriveServerEncryptionKey(clientEncryptionKey, _encryptionKey);
    }

    public void Activate()
    {
        if (!IsPrepared)
        {
            throw new InvalidOperationException("Legacy TEA security state must be prepared before activation.");
        }

        if (IsActive)
        {
            throw new InvalidOperationException("Legacy TEA security state is already active.");
        }

        IsActive = true;
    }
}
