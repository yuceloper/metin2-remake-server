using System.Numerics;
using System.Security.Cryptography;

namespace Metin2.Infrastructure.Networking.Security;

public interface IImprovedDh2PrivateKeySource
{
    BigInteger NextPrivateKey(BigInteger subgroupOrder);
}

public sealed class CryptographicImprovedDh2PrivateKeySource : IImprovedDh2PrivateKeySource
{
    public BigInteger NextPrivateKey(BigInteger subgroupOrder)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(subgroupOrder);
        int byteCount = subgroupOrder.GetByteCount(isUnsigned: true);
        Span<byte> candidate = byteCount <= 256 ? stackalloc byte[byteCount] : new byte[byteCount];

        while (true)
        {
            RandomNumberGenerator.Fill(candidate);
            var value = new BigInteger(candidate, isUnsigned: true, isBigEndian: true);
            if (value > BigInteger.Zero && value < subgroupOrder)
            {
                return value;
            }
        }
    }
}

public readonly record struct ImprovedDh2Offer(
    ushort AgreedLength,
    ReadOnlyMemory<byte> PublicData);

public sealed class ImprovedDh2KeyAgreement
{
    public const int PublicKeyLength = 128;
    public const int OfferLength = PublicKeyLength * 2;
    public const int AgreedValueLength = PublicKeyLength * 2;

    private static readonly BigInteger Modulus = ParseUnsignedHex(
        "B10B8F96A080E01DDE92DE5EAE5D54EC52C99FBCFB06A3C69A6A9DCA52D23B6" +
        "16073E28675A23D189838EF1E2EE652C013ECB4AEA906112324975C3CD49B83BF" +
        "ACCBDD7D90C4BD7098488E9C219A73724EFFD6FAE5644738FAA31A4FF55BCCC0" +
        "A151AF5F0DC8B4BD45BF37DF365C1A65E68CFDA76D4DA708DF1FB2BC2E4A4371");

    private static readonly BigInteger Generator = ParseUnsignedHex(
        "A4D1CBD5C3FD34126765A442EFB99905F8104DD258AC507FD6406CFF14266D31" +
        "266FEA1E5C41564B777E690F5504F213160217B4B01B886A5E91547F9E2749F4" +
        "D7FBD7D3B9A92EE1909D0D2263F80A76A6A24C087A091F531DBF0A0169B6A28A" +
        "D662A4D18E73AFA32D779D5918D08BC8858F4DCEF97C2A24855E6EEB22B3B2E5");

    private static readonly BigInteger SubgroupOrder = ParseUnsignedHex(
        "F518AA8781A8DF278ABA4E7D64B7CB9D49462353");

    private readonly IImprovedDh2PrivateKeySource _privateKeySource;
    private BigInteger _staticPrivate;
    private BigInteger _ephemeralPrivate;
    private bool _prepared;

    public ImprovedDh2KeyAgreement(IImprovedDh2PrivateKeySource? privateKeySource = null)
    {
        _privateKeySource = privateKeySource ?? new CryptographicImprovedDh2PrivateKeySource();
    }

    public ImprovedDh2Offer Prepare()
    {
        if (_prepared)
        {
            throw new InvalidOperationException("DH2 key agreement is already prepared.");
        }

        _staticPrivate = _privateKeySource.NextPrivateKey(SubgroupOrder);
        _ephemeralPrivate = _privateKeySource.NextPrivateKey(SubgroupOrder);
        ValidatePrivate(_staticPrivate);
        ValidatePrivate(_ephemeralPrivate);

        BigInteger staticPublic = BigInteger.ModPow(Generator, _staticPrivate, Modulus);
        BigInteger ephemeralPublic = BigInteger.ModPow(Generator, _ephemeralPrivate, Modulus);

        var publicData = new byte[OfferLength];
        EncodeFixed(staticPublic, publicData.AsSpan(0, PublicKeyLength));
        EncodeFixed(ephemeralPublic, publicData.AsSpan(PublicKeyLength, PublicKeyLength));
        _prepared = true;

        return new ImprovedDh2Offer(AgreedValueLength, publicData);
    }

    public byte[] Agree(ReadOnlySpan<byte> peerPublicData)
    {
        if (!_prepared)
        {
            throw new InvalidOperationException("DH2 agreement must be prepared before accepting peer data.");
        }

        if (peerPublicData.Length != OfferLength)
        {
            throw new ArgumentException($"DH2 peer data must contain exactly {OfferLength} bytes.", nameof(peerPublicData));
        }

        BigInteger peerStatic = DecodeUnsigned(peerPublicData[..PublicKeyLength]);
        BigInteger peerEphemeral = DecodeUnsigned(peerPublicData[PublicKeyLength..]);
        ValidatePeerPublic(peerStatic);
        ValidatePeerPublic(peerEphemeral);

        BigInteger staticShared = BigInteger.ModPow(peerStatic, _staticPrivate, Modulus);
        BigInteger ephemeralShared = BigInteger.ModPow(peerEphemeral, _ephemeralPrivate, Modulus);

        var shared = new byte[AgreedValueLength];
        EncodeFixed(staticShared, shared.AsSpan(0, PublicKeyLength));
        EncodeFixed(ephemeralShared, shared.AsSpan(PublicKeyLength, PublicKeyLength));
        return shared;
    }

    private static void ValidatePrivate(BigInteger value)
    {
        if (value <= BigInteger.Zero || value >= SubgroupOrder)
        {
            throw new InvalidOperationException("DH2 private key source returned a value outside the RFC5114 subgroup range.");
        }
    }

    private static void ValidatePeerPublic(BigInteger value)
    {
        if (value <= BigInteger.One || value >= Modulus - BigInteger.One)
        {
            throw new InvalidOperationException("DH2 peer public key is outside the valid modulus range.");
        }

        if (BigInteger.ModPow(value, SubgroupOrder, Modulus) != BigInteger.One)
        {
            throw new InvalidOperationException("DH2 peer public key is not in the expected RFC5114 subgroup.");
        }
    }

    private static void EncodeFixed(BigInteger value, Span<byte> destination)
    {
        destination.Clear();
        byte[] bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (bytes.Length > destination.Length)
        {
            throw new InvalidOperationException("DH2 value exceeds the fixed wire width.");
        }

        bytes.CopyTo(destination[(destination.Length - bytes.Length)..]);
    }

    private static BigInteger DecodeUnsigned(ReadOnlySpan<byte> bytes) =>
        new(bytes, isUnsigned: true, isBigEndian: true);

    private static BigInteger ParseUnsignedHex(string value) =>
        new(Convert.FromHexString(value), isUnsigned: true, isBigEndian: true);
}
