using Metin2.Protocol.Generator.Model;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Metin2.Protocol.Generator.Parsing;

internal sealed class PacketDefinitionParser
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public PacketDocument Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw new PacketDefinitionParseException("Packet definition file is empty.");
        }

        try
        {
            PacketDocumentDto? dto = _deserializer.Deserialize<PacketDocumentDto>(yaml);
            if (dto is null)
            {
                throw new PacketDefinitionParseException("Packet definition file could not be deserialized.");
            }

            return ToModel(dto);
        }
        catch (PacketDefinitionParseException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PacketDefinitionParseException(exception.Message, exception);
        }
    }

    private static PacketDocument ToModel(PacketDocumentDto dto)
    {
        var packets = (dto.Packets ?? new List<PacketDto>())
            .Select(packet => new PacketDefinition(
                packet.Name ?? string.Empty,
                packet.Opcode,
                packet.Direction ?? string.Empty,
                packet.Phase ?? string.Empty,
                packet.Size ?? string.Empty,
                packet.Since <= 0 ? 1 : packet.Since,
                packet.Until,
                (packet.Fields ?? new List<FieldDto>())
                    .Select(field => new FieldDefinition(
                        field.Name ?? string.Empty,
                        field.Type ?? string.Empty,
                        field.DomainType,
                        field.Length,
                        field.LengthFrom,
                        field.LengthType,
                        field.MaxLength,
                        field.Encoding,
                        field.Termination,
                        field.Trim))
                    .ToArray()))
            .ToArray();

        return new PacketDocument(dto.Schema, dto.Protocol ?? string.Empty, packets);
    }

    private sealed class PacketDocumentDto
    {
        public int Schema { get; set; }
        public string? Protocol { get; set; }
        public List<PacketDto>? Packets { get; set; }
    }

    private sealed class PacketDto
    {
        public string? Name { get; set; }
        public int Opcode { get; set; }
        public string? Direction { get; set; }
        public string? Phase { get; set; }
        public string? Size { get; set; }
        public int Since { get; set; } = 1;
        public int? Until { get; set; }
        public List<FieldDto>? Fields { get; set; }
    }

    private sealed class FieldDto
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? DomainType { get; set; }
        public int? Length { get; set; }
        public string? LengthFrom { get; set; }
        public string? LengthType { get; set; }
        public int? MaxLength { get; set; }
        public string? Encoding { get; set; }
        public string? Termination { get; set; }
        public string? Trim { get; set; }
    }
}

internal sealed class PacketDefinitionParseException : Exception
{
    public PacketDefinitionParseException(string message)
        : base(message)
    {
    }

    public PacketDefinitionParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
