using Metin2.Protocol.Generator.Model;

namespace Metin2.Protocol.Generator.Validation;

internal static class ProtocolDefinitionValidator
{
    public static IReadOnlyList<ValidationFailure> Validate(IReadOnlyList<PacketDocument> documents)
    {
        var failures = new List<ValidationFailure>();
        PacketDefinition[] packets = documents.SelectMany(static document => document.Packets).ToArray();

        foreach (IGrouping<string, PacketDefinition> group in packets.GroupBy(static packet => packet.Name, StringComparer.Ordinal))
        {
            if (group.Count() > 1)
            {
                failures.Add(new ValidationFailure(
                    "DuplicatePacketNameAcrossFiles",
                    $"Packet name '{group.Key}' is declared in multiple packet definition files."));
            }
        }

        for (int i = 0; i < packets.Length; i++)
        {
            for (int j = i + 1; j < packets.Length; j++)
            {
                PacketDefinition left = packets[i];
                PacketDefinition right = packets[j];

                if (left.Opcode != right.Opcode)
                {
                    continue;
                }

                if (!DirectionsOverlap(left.Direction, right.Direction) || !PhasesOverlap(left.Phase, right.Phase))
                {
                    continue;
                }

                failures.Add(new ValidationFailure(
                    "AmbiguousPacketRegistration",
                    $"Packets '{left.Name}' and '{right.Name}' both match opcode '{left.Opcode}' for overlapping direction/phase metadata."));
            }
        }

        return failures;
    }

    private static bool DirectionsOverlap(string left, string right) =>
        left == right || left == "bidirectional" || right == "bidirectional";

    private static bool PhasesOverlap(string left, string right) =>
        left == right || left == "any" || right == "any";
}
