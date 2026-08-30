using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Metin2.Infrastructure.Networking.Game;
using Metin2.Infrastructure.Networking.Handshake;
using Metin2.Infrastructure.Networking.Listeners;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Characters.Application;
using Metin2.Modules.Game.Application;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Protocol.Legacy;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class LegacyGameSocketHandlerTests
{
    [TestMethod]
    public async Task Handshake_login_select_and_bootstrap_flow_over_one_tcp_connection()
    {
        using var cancellation = new CancellationTokenSource();
        var loginService = new RecordingGameLoginService();
        CharacterListEntry[] entries =
        [
            CreateCharacter(0, 101, "Warrior", 42, 1000, 2000),
            CreateCharacter(2, 202, "Sura", 35, 3000, 4000)
        ];
        var characterRepository = new FixedCharacterRepository(entries);
        var listService = new CharacterListService(characterRepository);
        var selectionService = new CharacterSelectionService(new FixedEmpireRepository(2), listService);
        var selectService = new CharacterSelectService(characterRepository);
        var bootstrapService = new CharacterBootstrapService(new FixedBootstrapRepository(
            new CharacterBootstrapSnapshot(
                new CharacterId(101),
                new AccountId(7),
                "Warrior",
                0,
                42,
                987654,
                123456,
                10,
                11,
                12,
                13,
                0,
                0,
                1000,
                2000,
                new MapId(1),
                0,
                7,
                2)));
        var points = new uint[255];
        points[5] = 777;
        points[6] = 999;
        var handler = new LegacyGameSocketHandler(
            new ConstantTimeProvider(1_000),
            new FixedHandshakeTokenSource(0xAABBCCDD),
            new LegacySequenceProfile("test", new byte[] { 0xAA }),
            loginService,
            selectionService,
            selectService,
            bootstrapService,
            new FixedSelectionContextProvider(new LegacyCharacterSelectionWireContext(
                0x01020304,
                13000,
                0x11223344,
                0x55667788,
                0xA5)),
            new FixedBootstrapRuntimeContextProvider(new LegacyCharacterBootstrapRuntimeContext(
                0x01020304,
                points,
                new ushort[] { 10, 20, 0, 30 },
                150,
                140,
                0,
                new uint[2],
                new GuildId(0),
                0,
                0,
                0)));

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

        byte[] loginPhase = await ReceiveExactAsync(client, 2);
        Assert.AreEqual((byte)0xFD, loginPhase[0]);
        Assert.AreEqual((byte)LegacyPhaseCode.Login, loginPhase[1]);

        var login = new TokenLogin("player", 0x11223344, new uint[] { 10, 20, 30, 40 });
        var loginFrame = new byte[53];
        PacketFrameWriteStatus loginStatus = PacketFrameWriter.TryWrite(in login, 0xAA, loginFrame, out int loginWritten);
        Assert.AreEqual(PacketFrameWriteStatus.Done, loginStatus);
        Assert.AreEqual(loginFrame.Length, loginWritten);

        await SendAllAsync(client, loginFrame);
        GameLoginRequest request = await loginService.Request.Task.WaitAsync(TimeSpan.FromSeconds(2));
        byte[] selection = await ReceiveExactAsync(client, 334);

        Assert.AreEqual(0x11223344u, request.Token);
        Assert.AreEqual("player", request.Username);
        Assert.AreEqual((byte)0x5A, selection[0]);
        Assert.AreEqual((byte)2, selection[1]);
        Assert.AreEqual((byte)0xA5, selection[2]);
        Assert.AreEqual((byte)0xFD, selection[3]);
        Assert.AreEqual((byte)LegacyPhaseCode.Select, selection[4]);
        Assert.AreEqual((byte)0x20, selection[5]);

        var select = new SelectCharacter(0);
        var selectFrame = new byte[3];
        PacketFrameWriteStatus selectStatus = PacketFrameWriter.TryWrite(in select, 0xAA, selectFrame, out int selectWritten);
        Assert.AreEqual(PacketFrameWriteStatus.Done, selectStatus);
        Assert.AreEqual(selectFrame.Length, selectWritten);

        await SendAllAsync(client, selectFrame);
        byte[] loadingPhase = await ReceiveExactAsync(client, 2);
        Assert.AreEqual((byte)0xFD, loadingPhase[0]);
        Assert.AreEqual((byte)LegacyPhaseCode.Loading, loadingPhase[1]);
        Assert.AreEqual(new CharacterId(101), characterRepository.LastSelectedCharacterId);

        byte[] bootstrap = await ReceiveExactAsync(client, 1102);
        Assert.AreEqual((byte)0x71, bootstrap[0]);
        Assert.AreEqual((byte)0x10, bootstrap[46]);
        Assert.AreEqual((byte)0x13, bootstrap[1067]);

        const int pointPayloadOffset = 47;
        Assert.AreEqual(42u, ReadPoint(bootstrap, pointPayloadOffset, 1));
        Assert.AreEqual(987654u, ReadPoint(bootstrap, pointPayloadOffset, 3));
        Assert.AreEqual(777u, ReadPoint(bootstrap, pointPayloadOffset, 5));
        Assert.AreEqual(999u, ReadPoint(bootstrap, pointPayloadOffset, 6));
        Assert.AreEqual(123456u, ReadPoint(bootstrap, pointPayloadOffset, 11));
        Assert.AreEqual(10u, ReadPoint(bootstrap, pointPayloadOffset, 12));
        Assert.AreEqual(11u, ReadPoint(bootstrap, pointPayloadOffset, 13));
        Assert.AreEqual(12u, ReadPoint(bootstrap, pointPayloadOffset, 14));
        Assert.AreEqual(13u, ReadPoint(bootstrap, pointPayloadOffset, 15));
        Assert.AreEqual(7u, ReadPoint(bootstrap, pointPayloadOffset, 26));

        client.Shutdown(SocketShutdown.Send);
        cancellation.Cancel();
        await listenerTask;
    }

    private static uint ReadPoint(byte[] frame, int payloadOffset, int index) =>
        BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(payloadOffset + (index * sizeof(uint)), sizeof(uint)));

    private static CharacterListEntry CreateCharacter(
        byte slot,
        uint id,
        string name,
        byte level,
        int x,
        int y) =>
        new(
            slot,
            new CharacterId(id),
            name,
            slot,
            level,
            120,
            10,
            11,
            12,
            13,
            0,
            0,
            0,
            x,
            y,
            0,
            new GuildId(0),
            string.Empty);

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

    private sealed class RecordingGameLoginService : IGameLoginService
    {
        public TaskCompletionSource<GameLoginRequest> Request { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<GameLoginResult> LoginAsync(
            GameLoginRequest request,
            CancellationToken cancellationToken = default)
        {
            Request.TrySetResult(request);
            return ValueTask.FromResult(GameLoginResult.Success(new AccountId(7), "player"));
        }
    }

    private sealed class FixedCharacterRepository(IReadOnlyList<CharacterListEntry> entries)
        : ICharacterListRepository, ICharacterSelectionRepository
    {
        public CharacterId? LastSelectedCharacterId { get; private set; }

        public ValueTask<IReadOnlyList<CharacterListEntry>> GetByAccountAsync(
            AccountId accountId,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(entries);

        public ValueTask<CharacterId?> FindOwnedCharacterIdAsync(
            AccountId accountId,
            byte slot,
            CancellationToken cancellationToken = default)
        {
            CharacterId? match = entries
                .Where(entry => entry.Slot == slot)
                .Select(entry => (CharacterId?)entry.CharacterId)
                .SingleOrDefault();
            LastSelectedCharacterId = match;
            return ValueTask.FromResult(match);
        }
    }

    private sealed class FixedBootstrapRepository(CharacterBootstrapSnapshot snapshot) : ICharacterBootstrapRepository
    {
        public ValueTask<CharacterBootstrapSnapshot?> GetOwnedAsync(
            AccountId accountId,
            CharacterId characterId,
            CancellationToken cancellationToken = default)
        {
            CharacterBootstrapSnapshot? result =
                snapshot.AccountId == accountId && snapshot.CharacterId == characterId ? snapshot : null;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FixedEmpireRepository(byte empire) : IAccountEmpireRepository
    {
        public ValueTask<byte> GetEmpireAsync(
            AccountId accountId,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(empire);
    }

    private sealed class FixedSelectionContextProvider(LegacyCharacterSelectionWireContext context)
        : ILegacyCharacterSelectionWireContextProvider
    {
        public LegacyCharacterSelectionWireContext Get(GameSession session) => context;
    }

    private sealed class FixedBootstrapRuntimeContextProvider(LegacyCharacterBootstrapRuntimeContext context)
        : ILegacyCharacterBootstrapRuntimeContextProvider
    {
        public LegacyCharacterBootstrapRuntimeContext Get(
            GameSession session,
            in CharacterBootstrapSnapshot snapshot) => context;
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
