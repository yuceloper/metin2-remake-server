using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Security;
using Metin2.Infrastructure.Networking.Sessions;

namespace Metin2.Infrastructure.Networking.Send;

/// <summary>
/// Queues complete legacy wire frames using the packet-security stage that is active
/// at the time each frame is written. Encryption is intentionally applied here rather
/// than in the socket send pump so later key rotations cannot affect already-queued data.
/// </summary>
public sealed class LegacyPacketOutput
{
    private readonly PipeWriter _writer;
    private readonly GameSession? _session;

    public LegacyPacketOutput(PipeWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
    }

    public LegacyPacketOutput(PipeWriter writer, GameSession session)
        : this(writer)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    public int Write(ReadOnlySpan<byte> frame)
    {
        if (frame.IsEmpty)
        {
            throw new ArgumentException("Legacy packet frame cannot be empty.", nameof(frame));
        }

        ImprovedPacketSecuritySession? improved = _session?.ImprovedSecuritySession;
        if (improved is { IsActive: true })
        {
            Span<byte> destination = _writer.GetSpan(frame.Length)[..frame.Length];
            frame.CopyTo(destination);
            improved.EncryptOutbound(destination);
            _writer.Advance(frame.Length);
            return frame.Length;
        }

        LegacyTeaSecurityState? tea = _session?.TeaSecurityState;
        if (tea is { IsActive: true })
        {
            int encryptedSize = LegacyTeaCipher.GetEncryptedSize(frame.Length);
            Span<byte> destination = _writer.GetSpan(encryptedSize)[..encryptedSize];
            int written = LegacyTeaCipher.EncryptPadded(frame, destination, tea.EncryptionKey.Span);
            if (written != encryptedSize)
            {
                throw new InvalidOperationException($"Legacy TEA output size mismatch: {written} != {encryptedSize}.");
            }

            _writer.Advance(written);
            return written;
        }

        Span<byte> plaintextDestination = _writer.GetSpan(frame.Length)[..frame.Length];
        frame.CopyTo(plaintextDestination);
        _writer.Advance(frame.Length);
        return frame.Length;
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        FlushResult flush = await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        if (flush.IsCanceled)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }
}
