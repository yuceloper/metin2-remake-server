using System.Buffers.Binary;

namespace Metin2.Infrastructure.Networking.Security;

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
            throw new ArgumentException("Destination is too small for padded TEA output.", nameof(destination));
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
            throw new ArgumentException("Legacy TEA ciphertext must contain complete 8-byte blocks.", nameof(ciphertext));
        }

        if (destination.Length < ciphertext.Length)
        {
            throw new ArgumentException("Destination is too small for TEA plaintext.", nameof(destination));
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

        uint v0 = BinaryPrimitives.ReadUInt32LittleEndian(block);
        uint v1 = BinaryPrimitives.ReadUInt32LittleEndian(block[sizeof(uint)..]);
        uint sum = 0;

        unchecked
        {
            for (int i = 0; i < Rounds; i++)
            {
                sum += Delta;
                v0 += ((v1 << 4) + key[0]) ^ (v1 + sum) ^ ((v1 >> 5) + key[1]);
                v1 += ((v0 << 4) + key[2]) ^ (v0 + sum) ^ ((v0 >> 5) + key[3]);
            }
        }

        BinaryPrimitives.WriteUInt32LittleEndian(block, v0);
        BinaryPrimitives.WriteUInt32LittleEndian(block[sizeof(uint)..], v1);
    }

    public static void DecryptBlock(Span<byte> block, ReadOnlySpan<uint> key)
    {
        ValidateBlock(block);
        ValidateKey(key);

        uint v0 = BinaryPrimitives.ReadUInt32LittleEndian(block);
        uint v1 = BinaryPrimitives.ReadUInt32LittleEndian(block[sizeof(uint)..]);
        uint sum = unchecked(Delta * Rounds);

        unchecked
        {
            for (int i = 0; i < Rounds; i++)
            {
                v1 -= ((v0 << 4) + key[2]) ^ (v0 + sum) ^ ((v0 >> 5) + key[3]);
                v0 -= ((v1 << 4) + key[0]) ^ (v1 + sum) ^ ((v1 >> 5) + key[1]);
                sum -= Delta;
            }
        }

        BinaryPrimitives.WriteUInt32LittleEndian(block, v0);
        BinaryPrimitives.WriteUInt32LittleEndian(block[sizeof(uint)..], v1);
    }

    private static void ValidateBlock(ReadOnlySpan<byte> block)
    {
        if (block.Length != BlockSize)
        {
            throw new ArgumentException($"TEA block must contain exactly {BlockSize} bytes.", nameof(block));
        }
    }

    private static void ValidateKey(ReadOnlySpan<uint> key)
    {
        if (key.Length != KeyWordCount)
        {
            throw new ArgumentException($"TEA key must contain exactly {KeyWordCount} uint32 words.", nameof(key));
        }
    }
}
