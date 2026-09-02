namespace Metin2.Infrastructure.Networking.Security;

public sealed class ImprovedPacketSecuritySession
{
    private readonly ImprovedDh2KeyAgreement _keyAgreement;
    private readonly IImprovedCipherProvider _cipherProvider;
    private IImprovedCipherTransform? _outbound;
    private IImprovedCipherTransform? _inbound;

    public ImprovedPacketSecuritySession(
        ImprovedDh2KeyAgreement keyAgreement,
        IImprovedCipherProvider cipherProvider)
    {
        ArgumentNullException.ThrowIfNull(keyAgreement);
        ArgumentNullException.ThrowIfNull(cipherProvider);
        _keyAgreement = keyAgreement;
        _cipherProvider = cipherProvider;
        KeyAgreementState = new ImprovedKeyAgreementState();
    }

    public ImprovedKeyAgreementState KeyAgreementState { get; }
    public bool IsActive => KeyAgreementState.Stage == ImprovedKeyAgreementStage.CipherActive;

    public Metin2.Protocol.Generated.Packets.KeyAgreement Start()
    {
        ImprovedDh2Offer offer = _keyAgreement.Prepare();
        return KeyAgreementState.Start(new ImprovedKeyAgreementOffer(offer.AgreedLength, offer.PublicData));
    }

    public void AcceptClientReply(in Metin2.Protocol.Generated.Packets.KeyAgreement packet)
    {
        ImprovedKeyAgreementPeerReply reply = KeyAgreementState.AcceptClientReply(in packet);
        if (reply.AgreedLength != ImprovedDh2KeyAgreement.AgreedValueLength)
        {
            throw new InvalidOperationException(
                $"Improved client agreed length {reply.AgreedLength} does not match expected {ImprovedDh2KeyAgreement.AgreedValueLength}.");
        }

        byte[] sharedSecret = _keyAgreement.Agree(reply.Data.Span);
        ImprovedServerCipherSuite suite = ImprovedCipherSuiteSelector.SelectForServer(sharedSecret);
        if (!_cipherProvider.Supports(suite.Outbound.Algorithm) || !_cipherProvider.Supports(suite.Inbound.Algorithm))
        {
            throw new NotSupportedException(
                $"Improved cipher selection is not supported by the configured provider: outbound={suite.Outbound.Algorithm}, inbound={suite.Inbound.Algorithm}.");
        }

        ImprovedCipherMaterial outboundMaterial = suite.Outbound;
        ImprovedCipherMaterial inboundMaterial = suite.Inbound;
        _outbound = _cipherProvider.Create(in outboundMaterial);
        _inbound = _cipherProvider.Create(in inboundMaterial);
    }

    public Metin2.Protocol.Generated.Packets.KeyAgreementCompleted CreateCompletionPacket() =>
        KeyAgreementState.CreateCompletionPacket();

    public void MarkCompletionFlushedAndActivate()
    {
        if (_outbound is null || _inbound is null)
        {
            throw new InvalidOperationException("Improved cipher transforms must be prepared before activation.");
        }

        KeyAgreementState.MarkCompletionFlushed();
        KeyAgreementState.MarkCipherActivated();
    }

    public void EncryptOutbound(Span<byte> bytes)
    {
        if (!IsActive || _outbound is null)
        {
            throw new InvalidOperationException("Improved outbound cipher is not active.");
        }

        _outbound.Transform(bytes);
    }

    public void DecryptInbound(Span<byte> bytes)
    {
        if (!IsActive || _inbound is null)
        {
            throw new InvalidOperationException("Improved inbound cipher is not active.");
        }

        _inbound.Transform(bytes);
    }
}
