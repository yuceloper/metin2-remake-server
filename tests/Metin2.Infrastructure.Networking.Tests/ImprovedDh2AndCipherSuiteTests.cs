using System.Numerics;
using Metin2.Infrastructure.Networking.Security;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class ImprovedDh2AndCipherSuiteTests
{
    [TestMethod]
    public void Two_deterministic_DH2_peers_derive_the_same_256_byte_shared_secret()
    {
        var alice = new ImprovedDh2KeyAgreement(new QueuePrivateKeySource(2, 3));
        var bob = new ImprovedDh2KeyAgreement(new QueuePrivateKeySource(5, 7));

        ImprovedDh2Offer aliceOffer = alice.Prepare();
        ImprovedDh2Offer bobOffer = bob.Prepare();
        byte[] aliceShared = alice.Agree(bobOffer.PublicData.Span);
        byte[] bobShared = bob.Agree(aliceOffer.PublicData.Span);

        Assert.AreEqual((ushort)256, aliceOffer.AgreedLength);
        Assert.AreEqual(256, aliceOffer.PublicData.Length);
        Assert.AreEqual(256, aliceShared.Length);
        CollectionAssert.AreEqual(aliceShared, bobShared);
    }

    [TestMethod]
    public void Cipher_selector_matches_CryptoPP_indexing_and_server_polarity()
    {
        byte[] shared = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        shared[0] = 14;
        shared[1] = 15;
        shared[14] = 12; // selector 12 => TEA for algorithm_0/server outbound
        shared[15] = 6;  // selector 6 => IDEA for algorithm_1/server inbound

        ImprovedServerCipherSuite suite = ImprovedCipherSuiteSelector.SelectForServer(shared);

        Assert.AreEqual(ImprovedBlockCipherAlgorithm.Tea, suite.Outbound.Algorithm);
        Assert.AreEqual(ImprovedBlockCipherAlgorithm.Idea, suite.Inbound.Algorithm);
        Assert.AreEqual(16, suite.Outbound.Key.Length);
        Assert.AreEqual(8, suite.Outbound.Iv.Length);
        Assert.AreEqual(16, suite.Inbound.Key.Length);
        Assert.AreEqual(8, suite.Inbound.Iv.Length);
        CollectionAssert.AreEqual(shared[..16], suite.Outbound.Key.ToArray());
        CollectionAssert.AreEqual(shared[16..32], suite.Inbound.Key.ToArray());
        CollectionAssert.AreEqual(shared[248..256], suite.Outbound.Iv.ToArray());
        CollectionAssert.AreEqual(shared[240..248], suite.Inbound.Iv.ToArray());
    }

    private sealed class QueuePrivateKeySource(params int[] values) : IImprovedDh2PrivateKeySource
    {
        private readonly Queue<BigInteger> _values = new(values.Select(value => new BigInteger(value)));

        public BigInteger NextPrivateKey(BigInteger subgroupOrder)
        {
            Assert.IsGreaterThan(0, _values.Count);
            return _values.Dequeue();
        }
    }
}
