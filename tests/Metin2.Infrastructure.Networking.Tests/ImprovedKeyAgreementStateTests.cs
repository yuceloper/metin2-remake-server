using Metin2.Infrastructure.Networking.Security;
using Metin2.Protocol.Generated.Packets;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class ImprovedKeyAgreementStateTests
{
    [TestMethod]
    public void Improved_handshake_requires_plain_completion_flush_before_cipher_activation()
    {
        var state = new ImprovedKeyAgreementState();
        byte[] serverData = [1, 2, 3, 4];

        KeyAgreement offer = state.Start(new ImprovedKeyAgreementOffer(128, serverData));

        Assert.AreEqual(ImprovedKeyAgreementStage.WaitingForClientReply, state.Stage);
        Assert.AreEqual((ushort)128, offer.AgreedLength);
        Assert.AreEqual((ushort)4, offer.DataLength);
        CollectionAssert.AreEqual(serverData, offer.Data.Span[..4].ToArray());

        var peerBuffer = new byte[256];
        peerBuffer[0] = 9;
        peerBuffer[1] = 8;
        var reply = new KeyAgreement(128, 2, peerBuffer);
        ImprovedKeyAgreementPeerReply peer = state.AcceptClientReply(in reply);

        Assert.AreEqual(ImprovedKeyAgreementStage.CompletionMustBeFlushed, state.Stage);
        CollectionAssert.AreEqual(new byte[] { 9, 8 }, peer.Data.ToArray());
        _ = state.CreateCompletionPacket();

        Assert.ThrowsExactly<InvalidOperationException>(state.MarkCipherActivated);

        state.MarkCompletionFlushed();
        Assert.AreEqual(ImprovedKeyAgreementStage.ReadyToActivateCipher, state.Stage);

        state.MarkCipherActivated();
        Assert.AreEqual(ImprovedKeyAgreementStage.CipherActive, state.Stage);
    }

    [TestMethod]
    public void Improved_handshake_rejects_wire_data_length_larger_than_fixed_buffer()
    {
        var state = new ImprovedKeyAgreementState();
        _ = state.Start(new ImprovedKeyAgreementOffer(128, new byte[1]));
        var packet = new KeyAgreement(128, 257, new byte[256]);

        Assert.ThrowsExactly<InvalidOperationException>(() => state.AcceptClientReply(in packet));
    }
}
