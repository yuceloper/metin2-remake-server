using Metin2.Protocol.Generator.Model;

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
            else if (!packetNames.Add(packet.Name))
            {
                failures.Add(new ValidationFailure("DuplicatePacketName", $"Packet name '{packet.Name}' is duplicated."));
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

            if (string.IsNullOrWhiteSpace(field.Type))
            {
                failures.Add(new ValidationFailure("MissingFieldType", $"Packet '{packet.Name}' field '{field.Name}' has no wire type."));
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
