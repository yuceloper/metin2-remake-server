using Metin2.Protocol.Generator.Generation;
using Metin2.Protocol.Generator.Model;
using Microsoft.CodeAnalysis.CSharp;

namespace Metin2.Protocol.Generator.Validation;

internal static class PacketDefinitionValidator
{
    private static readonly HashSet<string> Directions = new(StringComparer.Ordinal)
    {
        "client_to_server",
        "server_to_client",
        "bidirectional"
    };

    private static readonly HashSet<string> Phases = new(StringComparer.Ordinal)
    {
        "handshake",
        "auth",
        "select",
        "loading",
        "game",
        "any"
    };

    private static readonly HashSet<string> Sizes = new(StringComparer.Ordinal)
    {
        "fixed",
        "variable"
    };

    private static readonly HashSet<string> WireTypes = new(StringComparer.Ordinal)
    {
        "i8", "u8",
        "i16le", "i16be", "u16le", "u16be",
        "i32le", "i32be", "u32le", "u32be",
        "i64le", "i64be", "u64le", "u64be",
        "f32le", "f32be", "f64le", "f64be",
        "bool8", "fixed_string", "string", "bytes", "array"
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
        {
            failures.Add(new ValidationFailure("UnsupportedSchema", $"Schema version '{document.Schema}' is not supported."));
        }

        if (string.IsNullOrWhiteSpace(document.Protocol))
        {
            failures.Add(new ValidationFailure("MissingProtocol", "Protocol name is required."));
        }

        var packetNames = new HashSet<string>(StringComparer.Ordinal);
        var opcodeKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (PacketDefinition packet in document.Packets)
        {
            if (string.IsNullOrWhiteSpace(packet.Name))
            {
                failures.Add(new ValidationFailure("MissingPacketName", "Packet name is required."));
            }
            else
            {
                if (!packetNames.Add(packet.Name))
                {
                    failures.Add(new ValidationFailure("DuplicatePacketName", $"Packet name '{packet.Name}' is duplicated."));
                }

                if (!SyntaxFacts.IsValidIdentifier(packet.Name))
                {
                    failures.Add(new ValidationFailure("InvalidGeneratedPacketIdentifier", $"Packet name '{packet.Name}' is not a valid C# identifier."));
                }
            }

            if (packet.Opcode is < 0 or > ushort.MaxValue)
            {
                failures.Add(new ValidationFailure("InvalidOpcode", $"Packet '{packet.Name}' opcode '{packet.Opcode}' is outside the supported range."));
            }

            if (!Directions.Contains(packet.Direction))
            {
                failures.Add(new ValidationFailure("InvalidDirection", $"Packet '{packet.Name}' has unsupported direction '{packet.Direction}'."));
            }

            if (!Phases.Contains(packet.Phase))
            {
                failures.Add(new ValidationFailure("InvalidPhase", $"Packet '{packet.Name}' has unsupported phase '{packet.Phase}'."));
            }

            if (!Sizes.Contains(packet.Size))
            {
                failures.Add(new ValidationFailure("InvalidSizeModel", $"Packet '{packet.Name}' has unsupported size model '{packet.Size}'."));
            }

            if (packet.Until.HasValue && packet.Until.Value < packet.Since)
            {
                failures.Add(new ValidationFailure("InvalidVersionRange", $"Packet '{packet.Name}' has until < since."));
            }

            string opcodeKey = $"{packet.Direction}:{packet.Phase}:{packet.Opcode}";
            if (!opcodeKeys.Add(opcodeKey))
            {
                failures.Add(new ValidationFailure("DuplicateOpcode", $"Opcode '{packet.Opcode}' is duplicated for direction '{packet.Direction}' and phase '{packet.Phase}'."));
            }

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
                failures.Add(new ValidationFailure("MissingFieldName", $"Packet '{packet.Name}' contains a field without a name."));
                continue;
            }

            if (!names.Add(field.Name))
            {
                failures.Add(new ValidationFailure("DuplicateFieldName", $"Packet '{packet.Name}' contains duplicate field '{field.Name}'."));
            }

            string generatedName = PacketModelEmitter.ToPascalCase(field.Name);
            if (!SyntaxFacts.IsValidIdentifier(generatedName))
            {
                failures.Add(new ValidationFailure("InvalidGeneratedFieldIdentifier", $"Packet '{packet.Name}' field '{field.Name}' generates invalid C# identifier '{generatedName}'."));
            }
            else if (!generatedNames.Add(generatedName))
            {
                failures.Add(new ValidationFailure("GeneratedFieldNameCollision", $"Packet '{packet.Name}' has multiple fields that generate C# identifier '{generatedName}'."));
            }

            if (string.IsNullOrWhiteSpace(field.Type))
            {
                failures.Add(new ValidationFailure("MissingFieldType", $"Packet '{packet.Name}' field '{field.Name}' has no wire type."));
            }
            else if (!WireTypes.Contains(field.Type))
            {
                failures.Add(new ValidationFailure("UnsupportedWireType", $"Packet '{packet.Name}' field '{field.Name}' uses unsupported wire type '{field.Type}'."));
            }

            if (!string.IsNullOrWhiteSpace(field.DomainType))
            {
                if (!DomainWireTypes.TryGetValue(field.DomainType!, out HashSet<string>? compatibleTypes))
                {
                    failures.Add(new ValidationFailure("UnsupportedDomainType", $"Packet '{packet.Name}' field '{field.Name}' uses unsupported domain type '{field.DomainType}'."));
                }
                else if (!compatibleTypes.Contains(field.Type))
                {
                    failures.Add(new ValidationFailure("DomainWireTypeMismatch", $"Packet '{packet.Name}' field '{field.Name}' maps domain type '{field.DomainType}' to incompatible wire type '{field.Type}'."));
                }
            }

            bool isVariable = field.Type is "string" || (field.Type is "bytes" or "array" && field.LengthFrom is not null);
            if (isVariable && !field.MaxLength.HasValue)
            {
                failures.Add(new ValidationFailure("UnboundedVariableField", $"Packet '{packet.Name}' field '{field.Name}' must declare maxLength."));
            }

            if (field.LengthFrom is not null && !names.Contains(field.LengthFrom))
            {
                failures.Add(new ValidationFailure("InvalidLengthReference", $"Packet '{packet.Name}' field '{field.Name}' lengthFrom must reference an earlier field."));
            }

            if (field.Type == "fixed_string" && (!field.Length.HasValue || field.Length.Value <= 0))
            {
                failures.Add(new ValidationFailure("InvalidFixedStringLength", $"Packet '{packet.Name}' field '{field.Name}' must declare a positive fixed length."));
            }
        }
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
