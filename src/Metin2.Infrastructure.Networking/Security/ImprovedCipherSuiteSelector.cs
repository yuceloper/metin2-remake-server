namespace Metin2.Infrastructure.Networking.Security;

public enum ImprovedBlockCipherAlgorithm : byte
{
    TwofishDefault = 0,
    Rc6 = 1,
    Mars = 2,
    Twofish = 3,
    Serpent = 4,
    Cast256 = 5,
    Idea = 6,
    TripleDes2Key = 7,
    Camellia = 8,
    Seed = 9,
    Rc5 = 10,
    Blowfish = 11,
    Tea = 12,
    Shacal2 = 13
}

public readonly record struct ImprovedCipherMaterial(
    ImprovedBlockCipherAlgorithm Algorithm,
    ReadOnlyMemory<byte> Key,
    ReadOnlyMemory<byte> Iv);

public readonly record struct ImprovedServerCipherSuite(
    ImprovedCipherMaterial Outbound,
    ImprovedCipherMaterial Inbound);

public static class ImprovedCipherSuiteSelector
{
    private const int AlgorithmCount = 14;

    public static ImprovedServerCipherSuite SelectForServer(ReadOnlySpan<byte> sharedSecret)
    {
        if (sharedSecret.Length != ImprovedDh2KeyAgreement.AgreedValueLength)
        {
            throw new ArgumentException(
                $"Improved cipher shared secret must contain exactly {ImprovedDh2KeyAgreement.AgreedValueLength} bytes.",
                nameof(sharedSecret));
        }

        ImprovedBlockCipherAlgorithm algorithm0 = SelectAlgorithm(sharedSecret, sharedSecret[0]);
        ImprovedBlockCipherAlgorithm algorithm1 = SelectAlgorithm(sharedSecret, sharedSecret[1]);

        (int keyLength0, int ivLength0) = GetSizes(algorithm0);
        (int keyLength1, int ivLength1) = GetSizes(algorithm1);

        byte[] key0 = sharedSecret[..keyLength0].ToArray();
        int key1Offset = Math.Min(keyLength0, sharedSecret.Length - keyLength1);
        byte[] key1 = sharedSecret.Slice(key1Offset, keyLength1).ToArray();

        int iv0Offset = sharedSecret.Length - ivLength0;
        byte[] iv0 = sharedSecret.Slice(iv0Offset, ivLength0).ToArray();
        int iv1Offset = iv0Offset < ivLength1 ? 0 : iv0Offset - ivLength1;
        byte[] iv1 = sharedSecret.Slice(iv1Offset, ivLength1).ToArray();

        // Crypto++ server polarity is false: encoder_ = algorithm_0, decoder_ = algorithm_1.
        return new ImprovedServerCipherSuite(
            new ImprovedCipherMaterial(algorithm0, key0, iv0),
            new ImprovedCipherMaterial(algorithm1, key1, iv1));
    }

    private static ImprovedBlockCipherAlgorithm SelectAlgorithm(ReadOnlySpan<byte> sharedSecret, byte hintIndex) =>
        (ImprovedBlockCipherAlgorithm)(sharedSecret[hintIndex % sharedSecret.Length] % AlgorithmCount);

    public static (int KeyLength, int IvLength) GetSizes(ImprovedBlockCipherAlgorithm algorithm) =>
        algorithm switch
        {
            ImprovedBlockCipherAlgorithm.TwofishDefault => (16, 16),
            ImprovedBlockCipherAlgorithm.Rc6 => (16, 16),
            ImprovedBlockCipherAlgorithm.Mars => (16, 16),
            ImprovedBlockCipherAlgorithm.Twofish => (16, 16),
            ImprovedBlockCipherAlgorithm.Serpent => (16, 16),
            ImprovedBlockCipherAlgorithm.Cast256 => (16, 16),
            ImprovedBlockCipherAlgorithm.Idea => (16, 8),
            ImprovedBlockCipherAlgorithm.TripleDes2Key => (16, 8),
            ImprovedBlockCipherAlgorithm.Camellia => (16, 16),
            ImprovedBlockCipherAlgorithm.Seed => (16, 16),
            ImprovedBlockCipherAlgorithm.Rc5 => (16, 8),
            ImprovedBlockCipherAlgorithm.Blowfish => (16, 8),
            ImprovedBlockCipherAlgorithm.Tea => (16, 8),
            ImprovedBlockCipherAlgorithm.Shacal2 => (16, 32),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unknown improved block cipher algorithm.")
        };
}
