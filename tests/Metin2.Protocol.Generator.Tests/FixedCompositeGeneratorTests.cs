using Metin2.Protocol.Generator.Generation;
using Metin2.Protocol.Generator.Model;
using Metin2.Protocol.Generator.Parsing;
using Metin2.Protocol.Generator.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Metin2.Protocol.Generator.Tests;

[TestClass]
public sealed class FixedCompositeGeneratorTests
{
    private const string CharactersYaml = """
        schema: 1
        protocol: test
        types:
          - name: CharacterSummary
            size: fixed
            fields:
              - name: id
                type: u32le
                domainType: CharacterId
              - name: name
                type: fixed_string
                length: 25
                encoding: ascii
                termination: "null"
                trim: "null"
              - name: class
                type: u8
              - name: level
                type: u8
              - name: playtime
                type: u32le
              - name: st
                type: u8
              - name: ht
                type: u8
              - name: dx
                type: u8
              - name: iq
                type: u8
              - name: body_part
                type: u16le
              - name: name_change
                type: u8
              - name: hair_part
                type: u16le
              - name: unknown
                type: u32le
              - name: position_x
                type: i32le
              - name: position_y
                type: i32le
              - name: ip
                type: i32le
              - name: port
                type: u16le
              - name: skill_group
                type: u8
        packets:
          - name: Characters
            opcode: 32
            direction: server_to_client
            phase: select
            size: fixed
            fields:
              - name: character_list
                type: array
                length: 4
                element:
                  type: CharacterSummary
              - name: guild_ids
                type: array
                length: 4
                element:
                  type: u32le
                  domainType: GuildId
              - name: guild_names
                type: array
                length: 4
                element:
                  type: fixed_string
                  length: 13
                  encoding: ascii
                  termination: "null"
                  trim: "null"
              - name: handle
                type: u32le
              - name: random_key
                type: u32le
        """;

    [TestMethod]
    public void Parser_and_validator_accept_reference_character_composite()
    {
        PacketDocument document = new PacketDefinitionParser().Parse(CharactersYaml);
        IReadOnlyList<ValidationFailure> failures = PacketDefinitionValidator.Validate(document);

        Assert.AreEqual(0, failures.Count);
        Assert.AreEqual(1, document.Types.Count);
        Assert.AreEqual("CharacterSummary", document.Types[0].Name);
        Assert.AreEqual(4, document.Packets[0].Fields[0].Length);
        Assert.AreEqual("fixed_string", document.Packets[0].Fields[2].Element!.Type);
        Assert.AreEqual(13, document.Packets[0].Fields[2].Element!.Length);
    }

    [TestMethod]
    public void Emitters_compute_reference_packed_sizes()
    {
        PacketDocument document = new PacketDefinitionParser().Parse(CharactersYaml);
        WireTypeDefinition type = document.Types.Single();
        PacketDefinition packet = document.Packets.Single();
        IReadOnlyDictionary<string, WireTypeDefinition> types = document.Types.ToDictionary(static item => item.Name, StringComparer.Ordinal);

        Assert.AreEqual(63, FixedWireTypeEmitter.GetPayloadSize(type));
        string typeSource = FixedWireTypeEmitter.Emit(type);
        string packetSource = FixedPacketCodecEmitter.Emit(packet, types);

        StringAssert.Contains(typeSource, "public const int PayloadSize = 63;");
        StringAssert.Contains(packetSource, "public const int PayloadSize = 328;");
        StringAssert.Contains(packetSource, "CharacterSummaryCodec.TryRead");
        StringAssert.Contains(packetSource, "TryReadFixedAsciiNullTerminated(13");
    }

    [TestMethod]
    public void Validator_rejects_unknown_composite_array_element()
    {
        const string yaml = """
            schema: 1
            protocol: test
            packets:
              - name: Broken
                opcode: 1
                direction: server_to_client
                phase: select
                size: fixed
                fields:
                  - name: entries
                    type: array
                    length: 4
                    element:
                      type: MissingSummary
            """;

        PacketDocument document = new PacketDefinitionParser().Parse(yaml);
        IReadOnlyList<ValidationFailure> failures = PacketDefinitionValidator.Validate(document);

        Assert.IsTrue(failures.Any(static failure => failure.Code == "UnsupportedArrayElementType"));
    }

    [TestMethod]
    public void Validator_rejects_nested_composite_fields_in_schema_v1()
    {
        const string yaml = """
            schema: 1
            protocol: test
            types:
              - name: Inner
                size: fixed
                fields:
                  - name: value
                    type: u32le
              - name: Outer
                size: fixed
                fields:
                  - name: inner
                    type: Inner
            packets: []
            """;

        PacketDocument document = new PacketDefinitionParser().Parse(yaml);
        IReadOnlyList<ValidationFailure> failures = PacketDefinitionValidator.Validate(document);

        Assert.IsTrue(failures.Any(static failure => failure.Code == "NestedCompositeNotSupported"));
    }
}
