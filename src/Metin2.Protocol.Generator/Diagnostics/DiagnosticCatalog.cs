using Microsoft.CodeAnalysis;

namespace Metin2.Protocol.Generator.Diagnostics;

internal static class DiagnosticCatalog
{
    public static readonly DiagnosticDescriptor ParseError = new(
        id: "M2P001",
        title: "Packet definition could not be parsed",
        messageFormat: "{0}",
        category: "Metin2.Protocol",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ValidationError = new(
        id: "M2P002",
        title: "Packet definition is invalid",
        messageFormat: "{0}: {1}",
        category: "Metin2.Protocol",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
