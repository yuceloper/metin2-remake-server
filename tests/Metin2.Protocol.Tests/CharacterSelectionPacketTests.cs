using Metin2.Protocol.Generated;
using Metin2.Protocol.Generated.Packets;
using Metin2.Protocol.Generated.Types;
using Metin2.Protocol.IO;
using Metin2.Shared.Identity;

namespace Metin2.Protocol.Tests;

[TestClass]
public sealed class CharacterSelectionPacketTests
{
    [TestMethod]
    public void Characters_round_trips_composite_and_fixed_string_arrays()
    {
        CharacterSummary first = CreateCharacter(101, "Warrior", 0, 42, 1200, 1000, 2000);
        CharacterSummary second = CreateCharacter(202, "Sura", 2, 35, 600, 3000, 4000);
        CharacterSummary empty = CreateCharacter(0, string.Empty, 0, 0, 0, 0, 0);

        var packet = new Characters(
            new[] { first, second, empty, empty },
            new[] { new GuildId(11), new GuildId(22), new GuildId(0), new GuildId(0) },
            new[] { "Knights", "Mystics", string.Empty, string.Empty },
            0x11223344,
            0x55667788);

        Span<byte> payload = stackalloc byte[CharactersCodec.PayloadSize];
        var writer = new PacketWriter(payload);

        Assert.IsTrue(CharactersCodec.TryWrite(ref writer, in packet));
        Assert.AreEqual(0, writer.Remaining);

        var reader = new PacketReader(payload);
        Assert.IsTrue(CharactersCodec.TryRead(ref reader, out Characters decoded));
        Assert.AreEqual(0, reader.Remaining);
        Assert.AreEqual(4, decoded.CharacterList.Length);
        Assert.AreEqual(new CharacterId(101), decoded.CharacterList.Span[0].Id);
        Assert.AreEqual("Warrior", decoded.CharacterList.Span[0].Name);
        Assert.AreEqual((byte)42, decoded.CharacterList.Span[0].Level);
        Assert.AreEqual(new CharacterId(202), decoded.CharacterList.Span[1].Id);
        Assert.AreEqual("Sura", decoded.CharacterList.Span[1].Name);
        Assert.AreEqual(new GuildId(11), decoded.GuildIds.Span[0]);
        Assert.AreEqual("Knights", decoded.GuildNames.Span[0]);
        Assert.AreEqual(0x11223344u, decoded.Handle);
        Assert.AreEqual(0x55667788u, decoded.RandomKey);
    }

    [TestMethod]
    public void Characters_frame_is_exactly_329_bytes_with_0x20_header()
    {
        CharacterSummary empty = CreateCharacter(0, string.Empty, 0, 0, 0, 0, 0);
        var packet = new Characters(
            new[] { empty, empty, empty, empty },
            new[] { new GuildId(0), new GuildId(0), new GuildId(0), new GuildId(0) },
            new[] { string.Empty, string.Empty, string.Empty, string.Empty },
            0,
            0);
        Span<byte> frame = stackalloc byte[329];

        PacketFrameWriteStatus status = PacketFrameWriter.TryWrite(in packet, frame, out int written);

        Assert.AreEqual(PacketFrameWriteStatus.Done, status);
        Assert.AreEqual(329, written);
        Assert.AreEqual((byte)0x20, frame[0]);
    }

    [TestMethod]
    public void Select_character_and_enter_game_keep_sequence_framing()
    {
        var select = new SelectCharacter(3);
        var enterGame = new EnterGame();
        Span<byte> selectFrame = stackalloc byte[3];
        Span<byte> enterFrame = stackalloc byte[2];

        Assert.AreEqual(PacketFrameWriteStatus.Done, PacketFrameWriter.TryWrite(in select, 0xA1, selectFrame, out int selectWritten));
        Assert.AreEqual(3, selectWritten);
        Assert.AreEqual((byte)0x06, selectFrame[0]);
        Assert.AreEqual((byte)3, selectFrame[1]);
        Assert.AreEqual((byte)0xA1, selectFrame[2]);

        Assert.AreEqual(PacketFrameWriteStatus.Done, PacketFrameWriter.TryWrite(in enterGame, 0xA2, enterFrame, out int enterWritten));
        Assert.AreEqual(2, enterWritten);
        Assert.AreEqual((byte)0x0A, enterFrame[0]);
        Assert.AreEqual((byte)0xA2, enterFrame[1]);
    }

    private static CharacterSummary CreateCharacter(
        uint id,
        string name,
        byte @class,
        byte level,
        uint playtime,
        int x,
        int y) =>
        new(
            new CharacterId(id),
            name,
            @class,
            level,
            playtime,
            10,
            11,
            12,
            13,
            0,
            0,
            0,
            0,
            x,
            y,
            0,
            13000,
            0);
}
