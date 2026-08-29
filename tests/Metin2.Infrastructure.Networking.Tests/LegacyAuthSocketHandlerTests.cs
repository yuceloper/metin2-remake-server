using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Metin2.Infrastructure.Networking.Auth;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Listeners;
using Metin2.Modules.Auth.Application;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Protocol.Legacy;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class LegacyAuthSocketHandlerTests
{
    [TestMethod]
    public async Task Handshake_then_sequenced_login_returns_login_success()
    {
        var service = new AuthLoginService(
            new FixedVerifier(CredentialVerificationResult.Success(new AccountId(7), "player")),
            new FixedIssuer(0x11223344));

        byte[] response = await RunLoginAsync(service);

        Assert.AreEqual(6, response.Length);
        Assert.AreEqual((byte)0x96, response[0]);
        Assert.AreEqual(0x11223344u, BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(1, 4)));
        Assert.AreEqual((byte)1, response[5]);
    }

    [TestMethod]
    public async Task Handshake_then_invalid_credentials_returns_wrongpwd()
    {
        var service = new AuthLoginService(
            new FixedVerifier(CredentialVerificationResult.InvalidCredentials()),
            new FixedIssuer(0x11223344));

        byte[] response = await RunLoginAsync(service, expectedResponseLength: 11);

        Assert.AreEqual((byte)0x07, response[0]);
        Assert.AreEqual((byte)0, response[1]);
        Assert.AreEqual("WRONGPWD", Encoding.ASCII.GetString(response.AsSpan(2, 8)));
        Assert.AreEqual((byte)0, response[10]);
    }

    private static async Task<byte[]> RunLoginAsync(
        IAuthLoginService service,
        int expectedResponseLength = 6)
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new LegacyAuthSocketHandler(
            new ConstantTimeProvider(1_000),
            new FixedHandshakeTokenSource(0xAABBCCDD),
            new LegacySequenceProfile("test", new byte[] { 0xAA }),
            service);

        await using var listener = new TcpGameListener(new IPEndPoint(IPAddress.Loopback, 0));
        Task listenerTask = listener.RunAsync(handler, cancellation.Token);
        var endpoint = (IPEndPoint)listener.LocalEndPoint!;

        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await client.ConnectAsync(endpoint);

        byte[] initial = await ReceiveExactAsync(client, 15);
        Assert.AreEqual((byte)0xFD, initial[0]);
        Assert.AreEqual((byte)LegacyPhaseCode.Handshake, initial[1]);
        Assert.AreEqual((byte)0xFF, initial[2]);

        await SendAllAsync(client, initial.AsMemory(2, 13));

        byte[] authPhase = await ReceiveExactAsync(client, 2);
        Assert.AreEqual((byte)0xFD, authPhase[0]);
        Assert.AreEqual((byte)LegacyPhaseCode.Auth, authPhase[1]);

        var login = new LoginRequest("player", "secret", new uint[] { 1, 2, 3, 4 });
        var loginFrame = new byte[66];
        PacketFrameWriteStatus writeStatus = PacketFrameWriter.TryWrite(
            in login,
            0xAA,
            loginFrame,
            out int written);
        Assert.AreEqual(PacketFrameWriteStatus.Done, writeStatus);
        Assert.AreEqual(loginFrame.Length, written);

        await SendAllAsync(client, loginFrame);
        byte[] response = await ReceiveExactAsync(client, expectedResponseLength);

        client.Shutdown(SocketShutdown.Send);
        cancellation.Cancel();
        await listenerTask;

        return response;
    }

    private static async Task<byte[]> ReceiveExactAsync(Socket socket, int length)
    {
        var buffer = new byte[length];
        int offset = 0;
        while (offset < buffer.Length)
        {
            int received = await socket.ReceiveAsync(buffer.AsMemory(offset));
            Assert.IsTrue(received > 0);
            offset += received;
        }

        return buffer;
    }

    private static async ValueTask SendAllAsync(Socket socket, ReadOnlyMemory<byte> data)
    {
        while (!data.IsEmpty)
        {
            int sent = await socket.SendAsync(data);
            Assert.IsTrue(sent > 0);
            data = data.Slice(sent);
        }
    }

    private sealed class FixedVerifier(CredentialVerificationResult result) : IAccountCredentialVerifier
    {
        public ValueTask<CredentialVerificationResult> VerifyAsync(
            string username,
            string password,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(result);
    }

    private sealed class FixedIssuer(uint token) : IAuthTokenIssuer
    {
        public ValueTask<uint> IssueAsync(
            AccountId accountId,
            string username,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(token);
    }

    private sealed class FixedHandshakeTokenSource(uint token) : IHandshakeTokenSource
    {
        public uint NextToken() => token;
    }

    private sealed class ConstantTimeProvider(long value) : IServerTimeProvider
    {
        public long GetMilliseconds() => value;
    }
}
