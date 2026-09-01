using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace Metin2.Infrastructure.Networking.Security;

public interface IImprovedCipherTransform
{
    void Transform(Span<byte> data);
}

public interface IImprovedCipherProvider
{
    bool Supports(ImprovedBlockCipherAlgorithm algorithm);

    IImprovedCipherTransform Create(in ImprovedCipherMaterial material);
}

public sealed class BouncyCastleImprovedCipherProvider : IImprovedCipherProvider
{
    private const int CryptoPpRc5DefaultRounds = 16;

    public bool Supports(ImprovedBlockCipherAlgorithm algorithm) =>
        algorithm != ImprovedBlockCipherAlgorithm.Mars;

    public IImprovedCipherTransform Create(in ImprovedCipherMaterial material)
    {
        if (!Supports(material.Algorithm))
        {
            throw new NotSupportedException(
                $"Improved packet cipher '{material.Algorithm}' has no verified managed implementation.");
        }

        IBlockCipher engine = CreateEngine(material.Algorithm);
        byte[] key = material.Key.ToArray();
        byte[] iv = material.Iv.ToArray();

        if (iv.Length != engine.GetBlockSize())
        {
            throw new ArgumentException(
                $"Cipher IV length {iv.Length} does not match {material.Algorithm} block size {engine.GetBlockSize()}.",
                nameof(material));
        }

        ICipherParameters parameters = material.Algorithm == ImprovedBlockCipherAlgorithm.Rc5
            ? new RC5Parameters(key, CryptoPpRc5DefaultRounds)
            : new KeyParameter(key);
        engine.Init(true, parameters);

        return new BouncyCastleCtrTransform(engine, iv);
    }

    private static IBlockCipher CreateEngine(ImprovedBlockCipherAlgorithm algorithm) =>
        algorithm switch
        {
            ImprovedBlockCipherAlgorithm.TwofishDefault => new TwofishEngine(),
            ImprovedBlockCipherAlgorithm.Rc6 => new RC6Engine(),
            ImprovedBlockCipherAlgorithm.Twofish => new TwofishEngine(),
            ImprovedBlockCipherAlgorithm.Serpent => new SerpentEngine(),
            ImprovedBlockCipherAlgorithm.Cast256 => new Cast6Engine(),
            ImprovedBlockCipherAlgorithm.Idea => new IdeaEngine(),
            ImprovedBlockCipherAlgorithm.TripleDes2Key => new DesEdeEngine(),
            ImprovedBlockCipherAlgorithm.Camellia => new CamelliaEngine(),
            ImprovedBlockCipherAlgorithm.Seed => new SeedEngine(),
            ImprovedBlockCipherAlgorithm.Rc5 => new RC532Engine(),
            ImprovedBlockCipherAlgorithm.Blowfish => new BlowfishEngine(),
            ImprovedBlockCipherAlgorithm.Tea => new TeaEngine(),
            ImprovedBlockCipherAlgorithm.Shacal2 => new Shacal2Engine(),
            ImprovedBlockCipherAlgorithm.Mars => throw new NotSupportedException(
                "MARS is part of the original Crypto++ selector but is not available in BouncyCastle.Cryptography."),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unknown improved cipher algorithm.")
        };

    private sealed class BouncyCastleCtrTransform : IImprovedCipherTransform
    {
        private readonly IBlockCipher _engine;
        private readonly byte[] _counter;
        private readonly byte[] _keyStream;
        private int _keyStreamOffset;

        public BouncyCastleCtrTransform(IBlockCipher engine, byte[] iv)
        {
            _engine = engine;
            _counter = iv.ToArray();
            _keyStream = new byte[engine.GetBlockSize()];
            _keyStreamOffset = _keyStream.Length;
        }

        public void Transform(Span<byte> data)
        {
            for (int index = 0; index < data.Length; index++)
            {
                if (_keyStreamOffset == _keyStream.Length)
                {
                    _engine.ProcessBlock(_counter, 0, _keyStream, 0);
                    IncrementCounter(_counter);
                    _keyStreamOffset = 0;
                }

                data[index] ^= _keyStream[_keyStreamOffset++];
            }
        }

        private static void IncrementCounter(Span<byte> counter)
        {
            for (int index = counter.Length - 1; index >= 0; index--)
            {
                counter[index]++;
                if (counter[index] != 0)
                {
                    return;
                }
            }
        }
    }
}
