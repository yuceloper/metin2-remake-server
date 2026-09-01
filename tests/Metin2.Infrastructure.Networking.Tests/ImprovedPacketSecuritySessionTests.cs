using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;
using Metin2.Infrastructure.Networking.Compatibility;
using Metin2.Infrastructure.Networking.Security;
using Metin2.Infrastructure.Networking.Send;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Protocol.Generated.Packets;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class ImprovedPacketSecuritySessionTests
{
    [TestMethod]
    public async Task Improved_output_is_plain_before_completion_and_length_preserving_after_activation()
    {
        var serverDh = new ImprovedDh2KeyAgreement(new FixedPrivateKeySource(3, 5));
        var clientDh = new ImprovedDh2KeyAgreement(new FixedPrivateKeySource(7, 11));
        var provider = new XorCipherProvider();
        var security = new ImprovedPacketSecuritySession(serverDh, provider);
        KeyAgreement serverOffer = security.Start();
        ImprovedDh2Offer clientOffer = clientDh.Prepare();
        _ = clientDh.Agree(serverOffer.Data.Span[..serverOffer.DataLength]);

        var clientReplyData = new byte[ImprovedKeyAgreementState.MaximumDataLength];
        clientOffer.PublicData.Span.CopyTo(clientReplyData);
        var clientReply = new KeyAgreement(clientOffer.AgreedLength, checked((ushort)clientOffer.PublicData.Length), clientReplyData);
        security.AcceptClientReply(in clientReply);
        _ = security.CreateCompletionPacket();

        var profile = new LegacyClientCompatibilityProfile(
            "improved-test",
            LegacyPacketEncryptionMode.ImprovedPacketEncryption,
            null);
        var session = new GameSession(compatibilityProfile: profile);
        session.ConfigureImprovedSecurity(security);
        var pipe = new Pipe();
        var output = new LegacyPacketOutput(pipe.Writer, session);

        byte[] plaintext = [0xFD, 0x02, 0x79];
        Assert.AreEqual(plaintext.Length, output.Write(plaintext));
        await output.FlushAsync();
        ReadResult firstRead = await pipe.Reader.ReadAsync();
        byte[] beforeActivation = firstRead.Buffer.ToArray();
        pipe.Reader.AdvanceTo(firstRead.Buffer.End);
        CollectionAssert.AreEqual(plaintext, beforeActivation);

        security.MarkCompletionFlushedAndActivate();
        Assert.AreEqual(plaintext.Length, output.Write(plaintext));
        await output.FlushAsync();
        ReadResult secondRead = await pipe.Reader.ReadAsync();
        byte[] afterActivation = secondRead.Buffer.ToArray();
        pipe.Reader.AdvanceTo(secondRead.Buffer.End);

        Assert.AreEqual(plaintext.Length, afterActivation.Length);
        CollectionAssert.AreNotEqual(plaintext, afterActivation);
    }

    private sealed class FixedPrivateKeySource(params int[] values) : IImprovedDh2PrivateKeySource
    {
        private readonly Queue<BigInteger> _values = new(values.Select(value => new BigInteger(value)));

        public BigInteger NextPrivateKey(BigInteger subgroupOrder) => _values.Dequeue();
    }

    private sealed class XorCipherProvider : IImprovedCipherProvider
    {
        public bool Supports(ImprovedBlockCipherAlgorithm algorithm) => true;

        public IImprovedCipherTransform Create(in ImprovedCipherMaterial material) => new XorTransform(0xA5);
    }

    private sealed class XorTransform(byte mask) : IImprovedCipherTransform
    {
        public void Transform(Span<byte> data)
        {
            for (int index = 0; index < data.Length; index++)
            {
                data[index] ^= mask;
            }
        }
    }
}
