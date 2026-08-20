namespace Metin2.Protocol.Generator.Model;

internal sealed class PacketDocument
{
    public PacketDocument(int schema, string protocol, IReadOnlyList<PacketDefinition> packets)
    {
        Schema = schema;
        Protocol = protocol;
        Packets = packets;
    }

    public int Schema { get; }
    public string Protocol { get; }
    public IReadOnlyList<PacketDefinition> Packets { get; }
}

internal sealed class PacketDefinition
{
    public PacketDefinition(
        string name,
        int opcode,
        string direction,
        string phase,
        string size,
        bool sequence,
        int since,
        int? until,
        IReadOnlyList<FieldDefinition> fields)
    {
        Name = name;
        Opcode = opcode;
        Direction = direction;
        Phase = phase;
        Size = size;
        Sequence = sequence;
        Since = since;
        Until = until;
        Fields = fields;
    }

    public string Name { get; }
    public int Opcode { get; }
    public string Direction { get; }
    public string Phase { get; }
    public string Size { get; }
    public bool Sequence { get; }
    public int Since { get; }
    public int? Until { get; }
    public IReadOnlyList<FieldDefinition> Fields { get; }
}

internal sealed class FieldDefinition
{
    public FieldDefinition(
        string name,
        string type,
        string? domainType,
        int? length,
        string? lengthFrom,
        string? lengthType,
        int? maxLength,
        string? encoding,
        string? termination,
        string? trim)
    {
        Name = name;
        Type = type;
        DomainType = domainType;
        Length = length;
        LengthFrom = lengthFrom;
        LengthType = lengthType;
        MaxLength = maxLength;
        Encoding = encoding;
        Termination = termination;
        Trim = trim;
    }

    public string Name { get; }
    public string Type { get; }
    public string? DomainType { get; }
    public int? Length { get; }
    public string? LengthFrom { get; }
    public string? LengthType { get; }
    public int? MaxLength { get; }
    public string? Encoding { get; }
    public string? Termination { get; }
    public string? Trim { get; }
}
