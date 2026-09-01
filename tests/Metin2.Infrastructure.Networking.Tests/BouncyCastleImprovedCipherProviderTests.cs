using Metin2.Infrastructure.Networking.Security;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class BouncyCastleImprovedCipherProviderTests
{
    [TestMethod]
    public void Supported_improved_ciphers_round_trip_and_preserve_ctr_state_across_chunks()
    {
        var provider = new BouncyCastleImprovedCipherProvider();

        foreach (ImprovedBlockCipherAlgorithm algorithm in Enum.GetValues<ImprovedBlockCipherAlgorithm>())
        {
            if (!provider.Supports(algorithm))
            {
                continue;
            }

            (int keyLength, int ivLength) = ImprovedCipherSuiteSelector.GetSizes(algorithm);
            byte[] key = Enumerable.Range(1, keyLength).Select(value => (byte)value).ToArray();
            byte[] iv = Enumerable.Range(0, ivLength).Select(value => (byte)(0xA0 + value)).ToArray();
            var material = new ImprovedCipherMaterial(algorithm, key, iv);
            byte[] plaintext = Enumerable.Range(0, 97).Select(value => (byte)(value * 17)).ToArray();
            byte[] ciphertext = plaintext.ToArray();

            IImprovedCipherTransform encoder = provider.Create(in material);
            encoder.Transform(ciphertext.AsSpan(0, 13));
            encoder.Transform(ciphertext.AsSpan(13, 29));
            encoder.Transform(ciphertext.AsSpan(42));

            CollectionAssert.AreNotEqual(plaintext, ciphertext, $"{algorithm} did not transform the test plaintext.");

            IImprovedCipherTransform decoder = provider.Create(in material);
            decoder.Transform(ciphertext.AsSpan(0, 1));
            decoder.Transform(ciphertext.AsSpan(1, 64));
            decoder.Transform(ciphertext.AsSpan(65));

            CollectionAssert.AreEqual(plaintext, ciphertext, $"{algorithm} CTR round trip failed.");
        }
    }

    [TestMethod]
    public void Mars_is_explicitly_unsupported_in_managed_provider()
    {
        var provider = new BouncyCastleImprovedCipherProvider();
        Assert.IsFalse(provider.Supports(ImprovedBlockCipherAlgorithm.Mars));

        (int keyLength, int ivLength) = ImprovedCipherSuiteSelector.GetSizes(ImprovedBlockCipherAlgorithm.Mars);
        var material = new ImprovedCipherMaterial(
            ImprovedBlockCipherAlgorithm.Mars,
            new byte[keyLength],
            new byte[ivLength]);

        Assert.ThrowsExactly<NotSupportedException>(() => provider.Create(in material));
    }
}
