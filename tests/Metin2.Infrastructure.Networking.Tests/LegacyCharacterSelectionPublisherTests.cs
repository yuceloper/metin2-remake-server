using System.Buffers;
using System.IO.Pipelines;
using Metin2.Infrastructure.Networking.Game;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Characters.Application;
using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Protocol.IO;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Tests;

[TestClass]
public sealed class LegacyCharacterSelectionPublisherTests
{
    [TestMethod]
    public async Task Publish_writes_empire_select_phase_and_characters_as_one_legacy_batch()
    {
        var pipe = new Pipe();
        var session = new GameSession(PacketPhase.Login);
        session.Authenticate(new AccountId(7), "Player", new uint[] { 1, 2, 3, 4 });

        CharacterListEntry slotZero = CreateEntry(0, 101, "Warrior", 42, 1000, 2000);
        CharacterListEntry slotTwo = CreateEntry(2, 202, "Sura", 35, 3000, 4000);
        var listService = new CharacterListService(new StubCharacterListRepository([slotTwo, slotZero]));
        var selectionService = new CharacterSelectionService(new StubEmpireRepository(2), listService);
        var context = new FixedContextProvider(new LegacyCharacterSelectionWireContext(
            0x01020304,
            13000,
            0x11223344,
            0x55667788,
            0xA5));
        var publisher = new LegacyCharacterSelectionPublisher(pipe.Writer, selectionService, context);

        try
        {
            await publisher.PublishAsync(session);

            ReadResult read = await pipe.Reader.ReadAsync();
            ReadOnlySequence<byte> buffer = read.Buffer;
            Assert.AreEqual(333L, buffer.Length);

            byte[] frame = buffer.ToArray();
            Assert.AreEqual((byte)0x5A, frame[0]);
            Assert.AreEqual((byte)2, frame[1]);
            Assert.AreEqual((byte)0xFD, frame[2]);
            Assert.AreEqual((byte)0x03, frame[3]);
            Assert.AreEqual((byte)0x20, frame[4]);

            var reader = new PacketReader(frame.AsSpan(5, CharactersCodec.PayloadSize));
            Assert.IsTrue(CharactersCodec.TryRead(ref reader, out Characters characters));
            Assert.AreEqual(0, reader.Remaining);

            Assert.AreEqual(new CharacterId(101), characters.CharacterList.Span[0].Id);
            Assert.AreEqual("Warrior", characters.CharacterList.Span[0].Name);
            Assert.AreEqual((byte)42, characters.CharacterList.Span[0].Level);
            Assert.AreEqual(0x01020304, characters.CharacterList.Span[0].Ip);
            Assert.AreEqual((ushort)13000, characters.CharacterList.Span[0].Port);

            Assert.AreEqual(new CharacterId(0), characters.CharacterList.Span[1].Id);
            Assert.AreEqual(string.Empty, characters.CharacterList.Span[1].Name);

            Assert.AreEqual(new CharacterId(202), characters.CharacterList.Span[2].Id);
            Assert.AreEqual("Sura", characters.CharacterList.Span[2].Name);
            Assert.AreEqual(0x01020304, characters.CharacterList.Span[2].Ip);
            Assert.AreEqual((ushort)13000, characters.CharacterList.Span[2].Port);

            Assert.AreEqual(new CharacterId(0), characters.CharacterList.Span[3].Id);
            Assert.AreEqual(new GuildId(0), characters.GuildIds.Span[1]);
            Assert.AreEqual(string.Empty, characters.GuildNames.Span[1]);
            Assert.AreEqual(0x11223344u, characters.Handle);
            Assert.AreEqual(0x55667788u, characters.RandomKey);
            Assert.AreEqual(PacketPhase.Select, session.Phase);

            pipe.Reader.AdvanceTo(buffer.End);
        }
        finally
        {
            await pipe.Writer.CompleteAsync();
            await pipe.Reader.CompleteAsync();
        }
    }

    private static CharacterListEntry CreateEntry(
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

    private sealed class StubCharacterListRepository(IReadOnlyList<CharacterListEntry> entries) : ICharacterListRepository
    {
        public ValueTask<IReadOnlyList<CharacterListEntry>> GetByAccountAsync(
            AccountId accountId,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(entries);
    }

    private sealed class StubEmpireRepository(byte empire) : IAccountEmpireRepository
    {
        public ValueTask<byte> GetEmpireAsync(
            AccountId accountId,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(empire);
    }

    private sealed class FixedContextProvider(LegacyCharacterSelectionWireContext context)
        : ILegacyCharacterSelectionWireContextProvider
    {
        public LegacyCharacterSelectionWireContext Get(GameSession session) => context;
    }
}
