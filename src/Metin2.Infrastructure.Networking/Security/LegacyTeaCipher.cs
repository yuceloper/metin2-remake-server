using System.Buffers.Binary;

namespace Metin2.Infrastructure.Networking.Security;

/// <summary>
/// Implements the classic Metin2 libthecore `TEA_Encrypt` / `TEA_Decrypt` wire primitive.
/// Despite the exported TEA name, the original block routine uses XTEA-style key scheduling.
/// </summary>
public static class LegacyTeaCipher
{
    public const int BlockSize = 8;
    public const int KeyWordCount = 4;
    private const uint Delta = 0x9E3779B9u;
    private const int Rounds = 32;

    public static int GetEncryptedSize(int plaintextLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(plaintextLength);
        if (plaintextLength == 0)
        {
            return 0;
        }

        return checked((plaintextLength + (BlockSize - 1)) & ~(BlockSize - 1));
    }

    public static int EncryptPadded(
        ReadOnlySpan<byte> plaintext,
        Span<byte> destination,
        ReadOnlySpan<uint> key)
    {
        ValidateKey(key);
        int encryptedSize = GetEncryptedSize(plaintext.Length);
        if (destination.Length < encryptedSize)
        {
            throw new ArgumentException("Destination is too small for padded Metin2 TEA output.", nameof(destination));
        }

        Span<byte> output = destination[..encryptedSize];
        output.Clear();
        plaintext.CopyTo(output);

        for (int offset = 0; offset < output.Length; offset += BlockSize)
        {
            EncryptBlock(output.Slice(offset, BlockSize), key);
        }

        return encryptedSize;
    }

    public static int DecryptBlocks(
        ReadOnlySpan<byte> ciphertext,
        Span<byte> destination,
        ReadOnlySpan<uint> key)
    {
        ValidateKey(key);
        if ((ciphertext.Length & (BlockSize - 1)) != 0)
        {
            throw new ArgumentException("Classic Metin2 ciphertext must contain complete 8-byte blocks.", nameof(ciphertext));
        }

        if (destination.Length < ciphertext.Length)
        {
            throw new ArgumentException("Destination is too small for decrypted Metin2 blocks.", nameof(destination));
        }

        ciphertext.CopyTo(destination);
        Span<byte> output = destination[..ciphertext.Length];
        for (int offset = 0; offset < output.Length; offset += BlockSize)
        {
            DecryptBlock(output.Slice(offset, BlockSize), key);
        }

        return ciphertext.Length;
    }

    public static void EncryptBlock(Span<byte> block, ReadOnlySpan<uint> key)
    {
        ValidateBlock(block);
        ValidateKey(key);

        uint y = BinaryPrimitives.ReadUInt32LittleEndian(block);
        uint z = BinaryPrimitives.ReadUInt32LittleEndian(block[sizeof(uint)..]);
        uint sum = 0;

        unchecked
        {
            for (int i = 0; i < Rounds; i++)
            {
                y += (((z << 4) ^ (z >> 5)) + z) ^ (sum + key[(int)(sum & 3u)]);
                sum += Delta;
                z += (((y << 4) ^ (y >> 5)) + y) ^ (sum + key[(int)((sum >> 11) & 3u)]);
            }
        }

        BinaryPrimitives.WriteUInt32LittleEndian(block, y);
        BinaryPrimitives.WriteUInt32LittleEndian(block[sizeof(uint)..], z);
    }

    public static void DecryptBlock(Span<byte> block, ReadOnlySpan<uint> key)
    {
        ValidateBlock(block);
        ValidateKey(key);

        uint y = BinaryPrimitives.ReadUInt32LittleEndian(block);
        uint z = BinaryPrimitives.ReadUInt32LittleEndian(block[sizeof(uint)..]);
        uint sum = unchecked(Delta * Rounds);

        unchecked
        {
            for (int i = 0; i < Rounds; i++)
            {
                z -= (((y << 4) ^ (y >> 5)) + y) ^ (sum + key[(int)((sum >> 11) & 3u)]);
                sum -= Delta;
                y -= (((z << 4) ^ (z >> 5)) + z) ^ (sum + key[(int)(sum & 3u)]);
            }
        }

        BinaryPrimitives.WriteUInt32LittleEndian(block, y);
        BinaryPrimitives.WriteUInt32LittleEndian(block[sizeof(uint)..], z);
    }

    private static void ValidateBlock(ReadOnlySpan<byte> block)
    {
        if (block.Length != BlockSize)
        {
            throw new ArgumentException($"Metin2 TEA block must contain exactly {BlockSize} bytes.", nameof(block));
        }
    }

    private static void ValidateKey(ReadOnlySpan<uint> key)
    {
        if (key.Length != KeyWordCount)
        {
            throw new ArgumentException($"Metin2 TEA key must contain exactly {KeyWordCount} uint32 words.", nameof(key));
        }
    }
}
