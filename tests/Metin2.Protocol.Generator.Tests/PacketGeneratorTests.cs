using System.Collections.Immutable;
using Metin2.Protocol.Generator.Model;
using Metin2.Protocol.Generator.Parsing;
using Metin2.Protocol.Generator.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Metin2.Protocol.Generator.Tests;

[TestClass]
public sealed class PacketGeneratorTests
{
    private const string ValidYaml = """
        schema: 1
        protocol: test
        packets:
          - name: Ping
            opcode: 1
            direction: client_to_server
            phase: game
            size: fixed
            since: 1
            fields:
              - name: value
                type: u32le
        """;

    [TestMethod]
    public void Parser_ParsesValidDefinition()
    {
        var parser = new PacketDefinitionParser();

        PacketDocument document = parser.Parse(ValidYaml);

        Assert.AreEqual(1, document.Schema);
        Assert.AreEqual("test", document.Protocol);
        Assert.AreEqual(1, document.Packets.Count);
        Assert.AreEqual("Ping", document.Packets[0].Name);
        Assert.AreEqual(1, document.Packets[0].Opcode);
    }

    [TestMethod]
    public void Validator_RejectsDuplicateOpcodeInSameNamespace()
    {
        const string yaml = """
            schema: 1
            protocol: test
            packets:
              - name: First
                opcode: 1
                direction: client_to_server
                phase: game
                size: fixed
                fields: []
              - name: Second
                opcode: 1
                direction: client_to_server
                phase: game
                size: fixed
                fields: []
            """;

        var parser = new PacketDefinitionParser();
        PacketDocument document = parser.Parse(yaml);

        IReadOnlyList<ValidationFailure> failures = PacketDefinitionValidator.Validate(document);

        Assert.IsTrue(failures.Any(static failure => failure.Code == "DuplicateOpcode"));
    }

    [TestMethod]
    public void Validator_RejectsUnboundedVariableField()
    {
        const string yaml = """
            schema: 1
            protocol: test
            packets:
              - name: Chat
                opcode: 2
                direction: client_to_server
                phase: game
                size: variable
                fields:
                  - name: message
                    type: string
                    encoding: utf8
                    lengthType: u16le
            """;

        var parser = new PacketDefinitionParser();
        PacketDocument document = parser.Parse(yaml);

        IReadOnlyList<ValidationFailure> failures = PacketDefinitionValidator.Validate(document);

        Assert.IsTrue(failures.Any(static failure => failure.Code == "UnboundedVariableField"));
    }

    [TestMethod]
    public void Generator_EmitsManifestForValidAdditionalFile()
    {
        var syntaxTree = CSharpSyntaxTree.ParseText("namespace Consumer; public static class Marker { }");
        MetadataReference coreLibrary = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Consumer",
            syntaxTrees: new[] { syntaxTree },
            references: new[] { coreLibrary },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new PacketGenerator().AsSourceGenerator() },
            additionalTexts: new AdditionalText[] { new InMemoryAdditionalText("test.packet.yml", ValidYaml) },
            parseOptions: (CSharpParseOptions)syntaxTree.Options);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> diagnostics);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        Assert.IsFalse(diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.AreEqual(2, outputCompilation.SyntaxTrees.Count());
        Assert.AreEqual(1, runResult.Results.Length);
        Assert.IsTrue(runResult.Results[0].GeneratedSources.Any(static source => source.HintName == "ProtocolManifest.g.cs"));
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

        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            return _text;
        }
    }
}
