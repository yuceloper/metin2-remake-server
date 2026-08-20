using Metin2.Protocol.Generator.Generation;
using Metin2.Protocol.Generator.Model;
using Microsoft.CodeAnalysis.CSharp;

namespace Metin2.Protocol.Generator.Validation;

internal static class PacketDefinitionValidator
{
    private static readonly HashSet<string> Directions = new(StringComparer.Ordinal)
    {
        "client_to_server", "server_to_client", "bidirectional"
    };

    private static readonly HashSet<string> Phases = new(StringComparer.Ordinal)
    {
        "handshake", "login", "auth", "select", "loading", "game", "any"
    };

    private static readonly HashSet<string> Sizes = new(StringComparer.Ordinal) { "fixed", "variable" };

    private static readonly HashSet<string> ScalarWireTypes = new(StringComparer.Ordinal)
    {
        "i8", "u8",
        "i16le", "i16be", "u16le", "u16be",
        "i32le", "i32be", "u32le", "u32be",
        "i64le", "i64be", "u64le", "u64be",
        "f32le", "f32be", "f64le", "f64be", "bool8"
    };

    private static readonly HashSet<string> WireTypes = new(ScalarWireTypes, StringComparer.Ordinal)
    {
        "fixed_string", "string", "bytes", "array"
    };

    private static readonly HashSet<string> StringEncodings = new(StringComparer.Ordinal)
    {
        "ascii", "utf8", "latin1"
    };

    private static readonly IReadOnlyDictionary<string, HashSet<string>> DomainWireTypes =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["AccountId"] = new(StringComparer.Ordinal) { "u32le", "u32be" },
            ["CharacterId"] = new(StringComparer.Ordinal) { "u32le", "u32be" },
            ["EntityId"] = new(StringComparer.Ordinal) { "u32le", "u32be" },
            ["GuildId"] = new(StringComparer.Ordinal) { "u32le", "u32be" },
            ["MonsterId"] = new(StringComparer.Ordinal) { "u32le", "u32be" },
            ["ItemId"] = new(StringComparer.Ordinal) { "u64le", "u64be" },
            ["MapId"] = new(StringComparer.Ordinal) { "i32le", "i32be" }
        };

    public static IReadOnlyList<ValidationFailure> Validate(PacketDocument document)
    {
        var failures = new List<ValidationFailure>();

        if (document.Schema != 1)
            failures.Add(new("UnsupportedSchema", $"Schema version '{document.Schema}' is not supported."));
        if (string.IsNullOrWhiteSpace(document.Protocol))
            failures.Add(new("MissingProtocol", "Protocol name is required."));

        var packetNames = new HashSet<string>(StringComparer.Ordinal);
        var opcodeKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (PacketDefinition packet in document.Packets)
        {
            if (string.IsNullOrWhiteSpace(packet.Name))
            {
                failures.Add(new("MissingPacketName", "Packet name is required."));
            }
            else
            {
                if (!packetNames.Add(packet.Name))
                    failures.Add(new("DuplicatePacketName", $"Packet name '{packet.Name}' is duplicated."));
                if (!SyntaxFacts.IsValidIdentifier(packet.Name))
                    failures.Add(new("InvalidGeneratedPacketIdentifier", $"Packet name '{packet.Name}' is not a valid C# identifier."));
            }

            if (packet.Opcode is < 0 or > ushort.MaxValue)
                failures.Add(new("InvalidOpcode", $"Packet '{packet.Name}' opcode '{packet.Opcode}' is outside the supported range."));
            if (!Directions.Contains(packet.Direction))
                failures.Add(new("InvalidDirection", $"Packet '{packet.Name}' has unsupported direction '{packet.Direction}'."));
            if (!Phases.Contains(packet.Phase))
                failures.Add(new("InvalidPhase", $"Packet '{packet.Name}' has unsupported phase '{packet.Phase}'."));
            if (!Sizes.Contains(packet.Size))
                failures.Add(new("InvalidSizeModel", $"Packet '{packet.Name}' has unsupported size model '{packet.Size}'."));
            if (packet.Until.HasValue && packet.Until.Value < packet.Since)
                failures.Add(new("InvalidVersionRange", $"Packet '{packet.Name}' has until < since."));

            string opcodeKey = $"{packet.Direction}:{packet.Phase}:{packet.Opcode}";
            if (!opcodeKeys.Add(opcodeKey))
                failures.Add(new("DuplicateOpcode", $"Opcode '{packet.Opcode}' is duplicated for direction '{packet.Direction}' and phase '{packet.Phase}'."));

            ValidateFields(packet, failures);
        }

        return failures;
    }

    private static void ValidateFields(PacketDefinition packet, ICollection<ValidationFailure> failures)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var generatedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (FieldDefinition field in packet.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.Name))
            {
                failures.Add(new("MissingFieldName", $"Packet '{packet.Name}' contains a field without a name."));
                continue;
            }

            if (!names.Add(field.Name))
                failures.Add(new("DuplicateFieldName", $"Packet '{packet.Name}' contains duplicate field '{field.Name}'."));

            string generatedName = PacketModelEmitter.ToPascalCase(field.Name);
            if (!SyntaxFacts.IsValidIdentifier(generatedName))
                failures.Add(new("InvalidGeneratedFieldIdentifier", $"Packet '{packet.Name}' field '{field.Name}' generates invalid C# identifier '{generatedName}'."));
            else if (!generatedNames.Add(generatedName))
                failures.Add(new("GeneratedFieldNameCollision", $"Packet '{packet.Name}' has multiple fields that generate C# identifier '{generatedName}'."));

            if (string.IsNullOrWhiteSpace(field.Type))
                failures.Add(new("MissingFieldType", $"Packet '{packet.Name}' field '{field.Name}' has no wire type."));
            else if (!WireTypes.Contains(field.Type))
                failures.Add(new("UnsupportedWireType", $"Packet '{packet.Name}' field '{field.Name}' uses unsupported wire type '{field.Type}'."));

            ValidateDomainMapping(packet, field, failures);

            bool isVariable = field.Type is "string" || (field.Type is "bytes" or "array" && field.LengthFrom is not null);
            if (isVariable && !field.MaxLength.HasValue)
                failures.Add(new("UnboundedVariableField", $"Packet '{packet.Name}' field '{field.Name}' must declare maxLength."));

            if (field.LengthFrom is not null && !names.Contains(field.LengthFrom))
                failures.Add(new("InvalidLengthReference", $"Packet '{packet.Name}' field '{field.Name}' lengthFrom must reference an earlier field."));

            if (field.Type == "fixed_string")
                ValidateFixedString(packet, field, failures);
            else if (field.Type == "array")
                ValidateArray(packet, field, failures);
        }
    }

    private static void ValidateFixedString(PacketDefinition packet, FieldDefinition field, ICollection<ValidationFailure> failures)
    {
        if (!field.Length.HasValue || field.Length.Value <= 0)
            failures.Add(new("InvalidFixedStringLength", $"Packet '{packet.Name}' field '{field.Name}' must declare a positive fixed length."));
        if (string.IsNullOrWhiteSpace(field.Encoding) || !StringEncodings.Contains(field.Encoding!))
            failures.Add(new("InvalidStringEncoding", $"Packet '{packet.Name}' field '{field.Name}' must declare a supported encoding."));
        if (field.Termination is not ("null" or "none"))
            failures.Add(new("InvalidStringTermination", $"Packet '{packet.Name}' field '{field.Name}' must explicitly declare termination as 'null' or 'none'."));
        if (field.Trim is not null && field.Trim is not ("null" or "none"))
            failures.Add(new("InvalidStringTrim", $"Packet '{packet.Name}' field '{field.Name}' has unsupported trim policy '{field.Trim}'."));
    }

    private static void ValidateArray(PacketDefinition packet, FieldDefinition field, ICollection<ValidationFailure> failures)
    {
        if (field.LengthFrom is null && (!field.Length.HasValue || field.Length.Value <= 0))
            failures.Add(new("InvalidArrayLength", $"Packet '{packet.Name}' field '{field.Name}' must declare a positive fixed length or lengthFrom."));
        if (field.Element is null)
        {
            failures.Add(new("MissingArrayElement", $"Packet '{packet.Name}' field '{field.Name}' must declare element metadata."));
            return;
        }

        if (!ScalarWireTypes.Contains(field.Element.Type))
            failures.Add(new("UnsupportedArrayElementType", $"Packet '{packet.Name}' field '{field.Name}' uses unsupported array element wire type '{field.Element.Type}'."));

        if (!string.IsNullOrWhiteSpace(field.Element.DomainType))
            ValidateDomainWireType(packet.Name, field.Name, field.Element.DomainType!, field.Element.Type, failures);
    }

    private static void ValidateDomainMapping(PacketDefinition packet, FieldDefinition field, ICollection<ValidationFailure> failures)
    {
        if (string.IsNullOrWhiteSpace(field.DomainType))
            return;
        ValidateDomainWireType(packet.Name, field.Name, field.DomainType!, field.Type, failures);
    }

    private static void ValidateDomainWireType(string packetName, string fieldName, string domainType, string wireType, ICollection<ValidationFailure> failures)
    {
        if (!DomainWireTypes.TryGetValue(domainType, out HashSet<string>? compatibleTypes))
            failures.Add(new("UnsupportedDomainType", $"Packet '{packetName}' field '{fieldName}' uses unsupported domain type '{domainType}'."));
        else if (!compatibleTypes.Contains(wireType))
            failures.Add(new("DomainWireTypeMismatch", $"Packet '{packetName}' field '{fieldName}' maps domain type '{domainType}' to incompatible wire type '{wireType}'."));
    }
}

internal sealed class ValidationFailure
{
    public ValidationFailure(string code, string message)
    {
        Code = code;
        Message = message;
    }

    public string Code { get; }
    public string Message { get; }
}
