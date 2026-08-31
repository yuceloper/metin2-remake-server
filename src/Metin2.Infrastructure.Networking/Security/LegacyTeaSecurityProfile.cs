using System.Buffers.Binary;

namespace Metin2.Infrastructure.Networking.Security;

public sealed class LegacyTeaSecurityProfile
{
    private readonly byte[] _derivationKey;

    public LegacyTeaSecurityProfile(string name, ReadOnlySpan<byte> derivationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (derivationKey.Length != 16)
        {
            throw new ArgumentException("Legacy TEA derivation key must contain exactly 16 bytes.", nameof(derivationKey));
        }

        Name = name;
        _derivationKey = derivationKey.ToArray();
    }

    public string Name { get; }

    public ReadOnlyMemory<byte> DerivationKey => _derivationKey;

    public void DeriveServerEncryptionKey(ReadOnlySpan<uint> clientEncryptionKey, Span<uint> destination)
    {
        if (clientEncryptionKey.Length != LegacyTeaCipher.KeyWordCount)
        {
            throw new ArgumentException("Client encryption key must contain exactly four uint32 words.", nameof(clientEncryptionKey));
        }

        if (destination.Length < LegacyTeaCipher.KeyWordCount)
        {
            throw new ArgumentException("Destination must contain room for four uint32 words.", nameof(destination));
        }

        Span<uint> derivationWords = stackalloc uint[LegacyTeaCipher.KeyWordCount];
        for (int i = 0; i < derivationWords.Length; i++)
        {
            derivationWords[i] = BinaryPrimitives.ReadUInt32LittleEndian(
                _derivationKey.AsSpan(i * sizeof(uint), sizeof(uint)));
        }

        Span<byte> clientBytes = stackalloc byte[16];
        for (int i = 0; i < clientEncryptionKey.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                clientBytes.Slice(i * sizeof(uint), sizeof(uint)),
                clientEncryptionKey[i]);
        }

        for (int offset = 0; offset < clientBytes.Length; offset += LegacyTeaCipher.BlockSize)
        {
            LegacyTeaCipher.EncryptBlock(clientBytes.Slice(offset, LegacyTeaCipher.BlockSize), derivationWords);
        }

        for (int i = 0; i < LegacyTeaCipher.KeyWordCount; i++)
        {
            destination[i] = BinaryPrimitives.ReadUInt32LittleEndian(
                clientBytes.Slice(i * sizeof(uint), sizeof(uint)));
        }
    }
}
