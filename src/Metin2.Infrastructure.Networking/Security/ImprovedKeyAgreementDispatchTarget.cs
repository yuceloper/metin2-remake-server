using Metin2.Infrastructure.Networking.Send;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Protocol.Legacy;

namespace Metin2.Infrastructure.Networking.Security;

public sealed class ImprovedKeyAgreementDispatchTarget(
    GameSession session,
    LegacyPacketOutput output,
    ImprovedPacketSecuritySession security,
    PacketPhase nextPhase)
{
    private const int KeyAgreementFrameSize = 261;
    private const int CompletionFrameSize = 4;
    private const int PhaseFrameSize = 2;

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        KeyAgreement packet = security.Start();
        Span<byte> frame = stackalloc byte[KeyAgreementFrameSize];
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, frame, out int written);
        EnsureWritten(nameof(KeyAgreement), status, written, KeyAgreementFrameSize);
        output.Write(frame[..written]);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(KeyAgreement packet, CancellationToken cancellationToken)
    {
        if (session.Phase != PacketPhase.Handshake)
        {
            throw new InvalidOperationException("Improved key agreement reply is only valid before the post-handshake phase transition.");
        }

        security.AcceptClientReply(in packet);
        KeyAgreementCompleted completion = security.CreateCompletionPacket();
        Span<byte> completionFrame = stackalloc byte[CompletionFrameSize];
        PacketFrameWriteStatus completionStatus = PacketFrameWriter.TryWrite(in completion, completionFrame, out int completionWritten);
        EnsureWritten(nameof(KeyAgreementCompleted), completionStatus, completionWritten, CompletionFrameSize);

        // Completion must remain plaintext. Activate only after this flush finishes.
        output.Write(completionFrame[..completionWritten]);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        security.MarkCompletionFlushedAndActivate();

        session.TransitionTo(nextPhase);
        var phase = new Phase((byte)MapWirePhase(nextPhase));
        Span<byte> phaseFrame = stackalloc byte[PhaseFrameSize];
        PacketFrameWriteStatus phaseStatus = PacketFrameWriter.TryWrite(in phase, phaseFrame, out int phaseWritten);
        EnsureWritten(nameof(Phase), phaseStatus, phaseWritten, PhaseFrameSize);

        // The phase announcement is the first frame protected by the improved cipher.
        output.Write(phaseFrame[..phaseWritten]);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static LegacyPhaseCode MapWirePhase(PacketPhase phase) =>
        phase switch
        {
            PacketPhase.Login => LegacyPhaseCode.Login,
            PacketPhase.Auth => LegacyPhaseCode.Auth,
            PacketPhase.Select => LegacyPhaseCode.Select,
            PacketPhase.Loading => LegacyPhaseCode.Loading,
            PacketPhase.Game => LegacyPhaseCode.Game,
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unsupported improved post-handshake phase.")
        };

    private static void EnsureWritten(string packetName, PacketFrameWriteStatus status, int written, int expected)
    {
        if (status != PacketFrameWriteStatus.Done || written != expected)
        {
            throw new InvalidOperationException($"{packetName} frame could not be written: {status} ({written}/{expected} bytes).");
        }
    }
}
