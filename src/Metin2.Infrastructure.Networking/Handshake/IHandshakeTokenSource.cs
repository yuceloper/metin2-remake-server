using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Metin2.Infrastructure.Networking.Handshake;

public interface IHandshakeTokenSource
{
    uint NextToken();
}

public sealed class RandomHandshakeTokenSource : IHandshakeTokenSource
{
    public uint NextToken()
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        RandomNumberGenerator.Fill(bytes);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }
}
