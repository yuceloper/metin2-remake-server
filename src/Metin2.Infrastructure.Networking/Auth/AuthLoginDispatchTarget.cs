using System.IO.Pipelines;
using Metin2.Modules.Auth.Application;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;

namespace Metin2.Infrastructure.Networking.Auth;

public sealed class AuthLoginDispatchTarget : IPacketDispatchTarget
{
    private const int LoginSuccessFrameSize = 6;
    private const int LoginFailedFrameSize = 11;
    private const string WrongPasswordStatus = "WRONGPWD";

    private readonly PipeWriter _output;
    private readonly IAuthLoginService _loginService;

    public AuthLoginDispatchTarget(PipeWriter output, IAuthLoginService loginService)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(loginService);
        _output = output;
        _loginService = loginService;
    }

    public async ValueTask HandleAsync(LoginRequest packet, CancellationToken cancellationToken)
    {
        AuthLoginResult result = await _loginService
            .LoginAsync(new AuthLoginRequest(packet.Username, packet.Password), cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            var response = new LoginSuccess(result.Token, 1);
            await WriteLoginSuccessAsync(response, cancellationToken).ConfigureAwait(false);
            return;
        }

        var failed = new LoginFailed(0, WrongPasswordStatus);
        await WriteLoginFailedAsync(failed, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask HandleAsync(Handshake packet, CancellationToken cancellationToken) => Unsupported(packet);
    public ValueTask HandleAsync(LoginFailed packet, CancellationToken cancellationToken) => Unsupported(packet);
    public ValueTask HandleAsync(LoginSuccess packet, CancellationToken cancellationToken) => Unsupported(packet);
    public ValueTask HandleAsync(Phase packet, CancellationToken cancellationToken) => Unsupported(packet);
    public ValueTask HandleAsync(TokenLogin packet, CancellationToken cancellationToken) => Unsupported(packet);

    private async ValueTask WriteLoginSuccessAsync(LoginSuccess packet, CancellationToken cancellationToken)
    {
        Memory<byte> memory = _output.GetMemory(LoginSuccessFrameSize);
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, memory.Span, out int written);
        EnsureWritten(status, written, LoginSuccessFrameSize);
        _output.Advance(written);
        await FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask WriteLoginFailedAsync(LoginFailed packet, CancellationToken cancellationToken)
    {
        Memory<byte> memory = _output.GetMemory(LoginFailedFrameSize);
        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, memory.Span, out int written);
        EnsureWritten(status, written, LoginFailedFrameSize);
        _output.Advance(written);
        await FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        FlushResult flush = await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
        if (flush.IsCanceled)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private static void EnsureWritten(PacketFrameWriteStatus status, int written, int expectedSize)
    {
        if (status != PacketFrameWriteStatus.Done || written != expectedSize)
        {
            throw new InvalidOperationException($"Auth response frame could not be written: {status} ({written} bytes).");
        }
    }

    private static ValueTask Unsupported<TPacket>(TPacket packet) =>
        ValueTask.FromException(new InvalidOperationException(
            $"Packet '{typeof(TPacket).Name}' is not handled by the auth login target."));
}
