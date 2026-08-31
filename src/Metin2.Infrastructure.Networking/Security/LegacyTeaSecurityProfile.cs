using System.Buffers.Binary;

namespace Metin2.Infrastructure.Networking.Security;

public sealed class LegacyTeaSecurityProfile
{
    private readonly byte[] _initialTransportKey;
    private readonly byte[] _derivationKey;

    public LegacyTeaSecurityProfile(
        string name,
        ReadOnlySpan<byte> initialTransportKey,
        ReadOnlySpan<byte> derivationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ValidateKeyBytes(initialTransportKey, nameof(initialTransportKey));
        ValidateKeyBytes(derivationKey, nameof(derivationKey));

        Name = name;
        _initialTransportKey = initialTransportKey.ToArray();
        _derivationKey = derivationKey.ToArray();
    }

    public string Name { get; }

    public ReadOnlyMemory<byte> InitialTransportKey => _initialTransportKey;

    public ReadOnlyMemory<byte> DerivationKey => _derivationKey;

    public void GetInitialTransportKey(Span<uint> destination)
    {
        if (destination.Length < LegacyTeaCipher.KeyWordCount)
        {
            throw new ArgumentException("Destination must contain room for four uint32 words.", nameof(destination));
        }

        ReadWords(_initialTransportKey, destination);
    }

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
        ReadWords(_derivationKey, derivationWords);

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

        ReadWords(clientBytes, destination);
    }

    private static void ReadWords(ReadOnlySpan<byte> bytes, Span<uint> destination)
    {
        for (int i = 0; i < LegacyTeaCipher.KeyWordCount; i++)
        {
            destination[i] = BinaryPrimitives.ReadUInt32LittleEndian(
                bytes.Slice(i * sizeof(uint), sizeof(uint)));
        }
    }

    private static void ValidateKeyBytes(ReadOnlySpan<byte> value, string parameterName)
    {
        if (value.Length != 16)
        {
            throw new ArgumentException("Legacy TEA key material must contain exactly 16 bytes.", parameterName);
        }
    }
}
