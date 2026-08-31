using Metin2.Protocol.Generated.Packets;

namespace Metin2.Infrastructure.Networking.Security;

public enum ImprovedKeyAgreementStage : byte
{
    WaitingToStart = 0,
    WaitingForClientReply = 1,
    CompletionMustBeFlushed = 2,
    ReadyToActivateCipher = 3,
    CipherActive = 4
}

public readonly record struct ImprovedKeyAgreementOffer(
    ushort AgreedLength,
    ReadOnlyMemory<byte> Data);

public readonly record struct ImprovedKeyAgreementPeerReply(
    ushort AgreedLength,
    ReadOnlyMemory<byte> Data);

public sealed class ImprovedKeyAgreementState
{
    public const int MaximumDataLength = 256;

    public ImprovedKeyAgreementStage Stage { get; private set; }

    public KeyAgreement Start(in ImprovedKeyAgreementOffer offer)
    {
        if (Stage != ImprovedKeyAgreementStage.WaitingToStart)
        {
            throw new InvalidOperationException("Improved key agreement has already started.");
        }

        if (offer.Data.Length > MaximumDataLength)
        {
            throw new ArgumentException("Improved key agreement data exceeds the fixed 256-byte wire buffer.", nameof(offer));
        }

        var data = new byte[MaximumDataLength];
        offer.Data.Span.CopyTo(data);
        Stage = ImprovedKeyAgreementStage.WaitingForClientReply;
        return new KeyAgreement(offer.AgreedLength, checked((ushort)offer.Data.Length), data);
    }

    public ImprovedKeyAgreementPeerReply AcceptClientReply(in KeyAgreement packet)
    {
        if (Stage != ImprovedKeyAgreementStage.WaitingForClientReply)
        {
            throw new InvalidOperationException("Improved key agreement client reply was not expected.");
        }

        if (packet.DataLength > MaximumDataLength || packet.DataLength > packet.Data.Length)
        {
            throw new InvalidOperationException("Improved key agreement client data length is invalid.");
        }

        byte[] peerData = packet.Data.Span[..packet.DataLength].ToArray();
        Stage = ImprovedKeyAgreementStage.CompletionMustBeFlushed;
        return new ImprovedKeyAgreementPeerReply(packet.AgreedLength, peerData);
    }

    public KeyAgreementCompleted CreateCompletionPacket()
    {
        if (Stage != ImprovedKeyAgreementStage.CompletionMustBeFlushed)
        {
            throw new InvalidOperationException("Key agreement completion packet is not ready to be sent.");
        }

        return new KeyAgreementCompleted(new byte[3]);
    }

    public void MarkCompletionFlushed()
    {
        if (Stage != ImprovedKeyAgreementStage.CompletionMustBeFlushed)
        {
            throw new InvalidOperationException("Completion can only be marked flushed after the client reply.");
        }

        Stage = ImprovedKeyAgreementStage.ReadyToActivateCipher;
    }

    public void MarkCipherActivated()
    {
        if (Stage != ImprovedKeyAgreementStage.ReadyToActivateCipher)
        {
            throw new InvalidOperationException("Improved cipher cannot activate before the plaintext completion packet is flushed.");
        }

        Stage = ImprovedKeyAgreementStage.CipherActive;
    }
}
