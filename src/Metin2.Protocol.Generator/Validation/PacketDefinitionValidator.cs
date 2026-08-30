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

    private static readonly HashSet<string> BuiltInWireTypes = new(ScalarWireTypes, StringComparer.Ordinal)
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

        var typeNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (WireTypeDefinition type in document.Types)
        {
            if (string.IsNullOrWhiteSpace(type.Name))
            {
                failures.Add(new("MissingWireTypeName", "Reusable wire type name is required."));
                continue;
            }

            if (!typeNames.Add(type.Name))
                failures.Add(new("DuplicateWireTypeName", $"Reusable wire type '{type.Name}' is duplicated."));
            if (!SyntaxFacts.IsValidIdentifier(type.Name))
                failures.Add(new("InvalidGeneratedWireTypeIdentifier", $"Reusable wire type '{type.Name}' is not a valid C# identifier."));
            if (BuiltInWireTypes.Contains(type.Name))
                failures.Add(new("ReservedWireTypeName", $"Reusable wire type '{type.Name}' collides with a built-in wire type."));
            if (type.Size != "fixed")
                failures.Add(new("UnsupportedCompositeSizeModel", $"Reusable wire type '{type.Name}' must use size: fixed."));
        }

        foreach (WireTypeDefinition type in document.Types)
        {
            ValidateFields(type.Name, type.Fields, typeNames, allowArrays: false, allowCompositeFields: false, failures);
        }

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
                if (typeNames.Contains(packet.Name))
                    failures.Add(new("PacketWireTypeNameCollision", $"Packet name '{packet.Name}' collides with reusable wire type '{packet.Name}'."));
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

            ValidateFields(packet.Name, packet.Fields, typeNames, allowArrays: true, allowCompositeFields: true, failures);
        }

        return failures;
    }

    private static void ValidateFields(
        string ownerName,
        IReadOnlyList<FieldDefinition> fields,
        HashSet<string> compositeTypes,
        bool allowArrays,
        bool allowCompositeFields,
        ICollection<ValidationFailure> failures)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var generatedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (FieldDefinition field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Name))
            {
                failures.Add(new("MissingFieldName", $"'{ownerName}' contains a field without a name."));
                continue;
            }

            if (!names.Add(field.Name))
                failures.Add(new("DuplicateFieldName", $"'{ownerName}' contains duplicate field '{field.Name}'."));

            string generatedName = PacketModelEmitter.ToPascalCase(field.Name);
            if (!SyntaxFacts.IsValidIdentifier(generatedName))
                failures.Add(new("InvalidGeneratedFieldIdentifier", $"'{ownerName}' field '{field.Name}' generates invalid C# identifier '{generatedName}'."));
            else if (!generatedNames.Add(generatedName))
                failures.Add(new("GeneratedFieldNameCollision", $"'{ownerName}' has multiple fields that generate C# identifier '{generatedName}'."));

            bool isComposite = compositeTypes.Contains(field.Type);
            if (string.IsNullOrWhiteSpace(field.Type))
                failures.Add(new("MissingFieldType", $"'{ownerName}' field '{field.Name}' has no wire type."));
            else if (!BuiltInWireTypes.Contains(field.Type) && !isComposite)
                failures.Add(new("UnsupportedWireType", $"'{ownerName}' field '{field.Name}' uses unsupported wire type '{field.Type}'."));
            else if (isComposite && !allowCompositeFields)
                failures.Add(new("NestedCompositeNotSupported", $"Reusable wire type '{ownerName}' cannot contain composite field '{field.Name}' of type '{field.Type}'."));

            ValidateDomainMapping(ownerName, field, failures);

            bool isVariable = field.Type is "string" || (field.Type is "bytes" or "array" && field.LengthFrom is not null);
            if (isVariable && !field.MaxLength.HasValue)
                failures.Add(new("UnboundedVariableField", $"'{ownerName}' field '{field.Name}' must declare maxLength."));

            if (field.LengthFrom is not null && !names.Contains(field.LengthFrom))
                failures.Add(new("InvalidLengthReference", $"'{ownerName}' field '{field.Name}' lengthFrom must reference an earlier field."));

            if (field.Type == "fixed_string")
                ValidateFixedString(ownerName, field.Name, field.Length, field.Encoding, field.Termination, field.Trim, failures);
            else if (field.Type == "array")
            {
                if (!allowArrays)
                    failures.Add(new("CompositeArrayFieldNotSupported", $"Reusable wire type '{ownerName}' cannot contain array field '{field.Name}' in schema v1."));
                ValidateArray(ownerName, field, compositeTypes, failures);
            }
        }
    }

    private static void ValidateFixedString(
        string ownerName,
        string fieldName,
        int? length,
        string? encoding,
        string? termination,
        string? trim,
        ICollection<ValidationFailure> failures)
    {
        if (!length.HasValue || length.Value <= 0)
            failures.Add(new("InvalidFixedStringLength", $"'{ownerName}' field '{fieldName}' must declare a positive fixed length."));
        if (string.IsNullOrWhiteSpace(encoding) || !StringEncodings.Contains(encoding!))
            failures.Add(new("InvalidStringEncoding", $"'{ownerName}' field '{fieldName}' must declare a supported encoding."));
        if (termination is not ("null" or "none"))
            failures.Add(new("InvalidStringTermination", $"'{ownerName}' field '{fieldName}' must explicitly declare termination as 'null' or 'none'."));
        if (trim is not null && trim is not ("null" or "none"))
            failures.Add(new("InvalidStringTrim", $"'{ownerName}' field '{fieldName}' has unsupported trim policy '{trim}'."));
    }

    private static void ValidateArray(
        string ownerName,
        FieldDefinition field,
        HashSet<string> compositeTypes,
        ICollection<ValidationFailure> failures)
    {
        if (field.LengthFrom is null && (!field.Length.HasValue || field.Length.Value <= 0))
            failures.Add(new("InvalidArrayLength", $"'{ownerName}' field '{field.Name}' must declare a positive fixed length or lengthFrom."));
        if (field.Element is null)
        {
            failures.Add(new("MissingArrayElement", $"'{ownerName}' field '{field.Name}' must declare element metadata."));
            return;
        }

        ElementDefinition element = field.Element;
        bool isComposite = compositeTypes.Contains(element.Type);
        bool isFixedString = element.Type == "fixed_string";
        if (!ScalarWireTypes.Contains(element.Type) && !isFixedString && !isComposite)
            failures.Add(new("UnsupportedArrayElementType", $"'{ownerName}' field '{field.Name}' uses unsupported array element wire type '{element.Type}'."));

        if (isFixedString)
            ValidateFixedString(ownerName, field.Name + "[]", element.Length, element.Encoding, element.Termination, element.Trim, failures);

        if (!string.IsNullOrWhiteSpace(element.DomainType))
            ValidateDomainWireType(ownerName, field.Name, element.DomainType!, element.Type, failures);
    }

    private static void ValidateDomainMapping(string ownerName, FieldDefinition field, ICollection<ValidationFailure> failures)
    {
        if (string.IsNullOrWhiteSpace(field.DomainType))
            return;
        ValidateDomainWireType(ownerName, field.Name, field.DomainType!, field.Type, failures);
    }

    private static void ValidateDomainWireType(string ownerName, string fieldName, string domainType, string wireType, ICollection<ValidationFailure> failures)
    {
        if (!DomainWireTypes.TryGetValue(domainType, out HashSet<string>? compatibleTypes))
            failures.Add(new("UnsupportedDomainType", $"'{ownerName}' field '{fieldName}' uses unsupported domain type '{domainType}'."));
        else if (!compatibleTypes.Contains(wireType))
            failures.Add(new("DomainWireTypeMismatch", $"'{ownerName}' field '{fieldName}' maps domain type '{domainType}' to incompatible wire type '{wireType}'."));
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