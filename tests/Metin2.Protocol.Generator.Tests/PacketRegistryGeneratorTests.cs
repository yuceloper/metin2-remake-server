using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Metin2.Protocol.Generator.Tests;

[TestClass]
public sealed class PacketRegistryGeneratorTests
{
    [TestMethod]
    public void Registry_EmitsTypedLookupMetadataForFixedPackets()
    {
        const string yaml = """
            schema: 1
            protocol: legacy-metin2
            packets:
              - name: Handshake
                opcode: 0xFF
                direction: bidirectional
                phase: handshake
                size: fixed
                sequence: false
                fields:
                  - name: handshake
                    type: u32le
                  - name: time
                    type: u32le
                  - name: delta
                    type: u32le
              - name: LoginRequest
                opcode: 0x6F
                direction: client_to_server
                phase: auth
                size: fixed
                sequence: true
                fields:
                  - name: username
                    type: fixed_string
                    length: 31
                    encoding: ascii
                    termination: "null"
                    trim: "null"
                  - name: password
                    type: fixed_string
                    length: 17
                    encoding: ascii
                    termination: "null"
                    trim: "null"
                  - name: encrypt_key
                    type: array
                    length: 4
                    element:
                      type: u32le
            """;

        GeneratorDriverRunResult result = RunGenerator(new InMemoryAdditionalText("canonical.packet.yml", yaml), out ImmutableArray<Diagnostic> diagnostics);

        Assert.IsFalse(diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        string registry = GetSource(result, "PacketRegistry.g.cs");

        StringAssert.Contains(registry, "PacketId.Handshake");
        StringAssert.Contains(registry, "PacketId.LoginRequest");
        StringAssert.Contains(registry, "PacketPhase.Handshake");
        StringAssert.Contains(registry, "PacketPhase.Auth");
        StringAssert.Contains(registry, "false, true, 12");
        StringAssert.Contains(registry, "true, true, 64");
    }

    [TestMethod]
    public void Registry_RejectsOverlappingRegistrationAcrossFiles()
    {
        const string first = """
            schema: 1
            protocol: legacy-metin2
            packets:
              - name: First
                opcode: 0x42
                direction: bidirectional
                phase: handshake
                size: fixed
                fields: []
            """;

        const string second = """
            schema: 1
            protocol: legacy-metin2
            packets:
              - name: Second
                opcode: 0x42
                direction: client_to_server
                phase: handshake
                size: fixed
                fields: []
            """;

        GeneratorDriverRunResult result = RunGenerator(
            new InMemoryAdditionalText("first.packet.yml", first),
            new InMemoryAdditionalText("second.packet.yml", second),
            out ImmutableArray<Diagnostic> diagnostics);

        Assert.IsTrue(diagnostics.Any(static diagnostic =>
            diagnostic.Id == "M2P002" && diagnostic.GetMessage().Contains("AmbiguousPacketRegistration", StringComparison.Ordinal)));
        Assert.IsFalse(result.Results[0].GeneratedSources.Any(static source => source.HintName == "PacketRegistry.g.cs"));
    }

    private static string GetSource(GeneratorDriverRunResult result, string hintName) =>
        result.Results[0].GeneratedSources.Single(source => source.HintName == hintName).SourceText.ToString();

    private static GeneratorDriverRunResult RunGenerator(
        InMemoryAdditionalText additionalText,
        out ImmutableArray<Diagnostic> diagnostics) =>
        RunGenerator(new[] { additionalText }, out diagnostics);

    private static GeneratorDriverRunResult RunGenerator(
        InMemoryAdditionalText first,
        InMemoryAdditionalText second,
        out ImmutableArray<Diagnostic> diagnostics) =>
        RunGenerator(new[] { first, second }, out diagnostics);

    private static GeneratorDriverRunResult RunGenerator(
        IReadOnlyList<InMemoryAdditionalText> additionalTexts,
        out ImmutableArray<Diagnostic> diagnostics)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText("namespace Consumer; public static class Marker { }");
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Consumer",
            new[] { syntaxTree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new PacketGenerator().AsSourceGenerator() },
            additionalTexts: additionalTexts.Cast<AdditionalText>(),
            parseOptions: (CSharpParseOptions)syntaxTree.Options);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out diagnostics);
        return driver.GetRunResult();
    }

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            _text = SourceText.From(content);
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }
}
