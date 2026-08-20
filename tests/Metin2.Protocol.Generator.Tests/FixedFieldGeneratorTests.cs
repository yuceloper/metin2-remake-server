using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Metin2.Protocol.Generator.Tests;

[TestClass]
public sealed class FixedFieldGeneratorTests
{
    [TestMethod]
    public void LoginRequest_GeneratesExactPayloadShape()
    {
        const string yaml = """
            schema: 1
            protocol: legacy-metin2
            packets:
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

        GeneratorDriverRunResult result = RunGenerator(yaml);
        string model = GetSource(result, "Packets.LoginRequest.g.cs");
        string codec = GetSource(result, "Codecs.LoginRequest.g.cs");

        StringAssert.Contains(model, "string Username");
        StringAssert.Contains(model, "string Password");
        StringAssert.Contains(model, "global::System.ReadOnlyMemory<uint> EncryptKey");
        StringAssert.Contains(model, "public const bool HasSequence = true;");

        StringAssert.Contains(codec, "public const int PayloadSize = 64;");
        StringAssert.Contains(codec, "TryReadFixedAsciiNullTerminated(31");
        StringAssert.Contains(codec, "TryReadFixedAsciiNullTerminated(17");
        StringAssert.Contains(codec, "new uint[4]");
        StringAssert.Contains(codec, "packet.EncryptKey.Length != 4");
        StringAssert.Contains(codec, "TryWriteUInt32LittleEndian");
    }

    [TestMethod]
    public void TokenLogin_GeneratesExpectedPayloadSize()
    {
        const string yaml = """
            schema: 1
            protocol: legacy-metin2
            packets:
              - name: TokenLogin
                opcode: 0x6D
                direction: client_to_server
                phase: login
                size: fixed
                sequence: true
                fields:
                  - name: username
                    type: fixed_string
                    length: 31
                    encoding: ascii
                    termination: "null"
                    trim: "null"
                  - name: key
                    type: u32le
                  - name: xtea_key
                    type: array
                    length: 4
                    element:
                      type: u32le
            """;

        GeneratorDriverRunResult result = RunGenerator(yaml);
        string model = GetSource(result, "Packets.TokenLogin.g.cs");
        string codec = GetSource(result, "Codecs.TokenLogin.g.cs");

        StringAssert.Contains(model, "PacketPhase.Login");
        StringAssert.Contains(model, "public const bool HasSequence = true;");
        StringAssert.Contains(codec, "public const int PayloadSize = 51;");
    }

    [TestMethod]
    public void Validator_RejectsArrayWithoutElementMetadata()
    {
        const string yaml = """
            schema: 1
            protocol: test
            packets:
              - name: BrokenArray
                opcode: 1
                direction: client_to_server
                phase: auth
                size: fixed
                fields:
                  - name: key
                    type: array
                    length: 4
            """;

        GeneratorDriverRunResult result = RunGeneratorAllowingDiagnostics(yaml, out ImmutableArray<Diagnostic> diagnostics);

        Assert.IsTrue(diagnostics.Any(static diagnostic =>
            diagnostic.Id == "M2P002" && diagnostic.GetMessage().Contains("MissingArrayElement", StringComparison.Ordinal)));
        Assert.IsFalse(result.Results[0].GeneratedSources.Any(static source => source.HintName == "Codecs.BrokenArray.g.cs"));
    }

    private static string GetSource(GeneratorDriverRunResult result, string hintName) =>
        result.Results[0].GeneratedSources.Single(source => source.HintName == hintName).SourceText.ToString();

    private static GeneratorDriverRunResult RunGenerator(string yaml)
    {
        GeneratorDriverRunResult result = RunGeneratorAllowingDiagnostics(yaml, out ImmutableArray<Diagnostic> diagnostics);
        Assert.IsFalse(diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        return result;
    }

    private static GeneratorDriverRunResult RunGeneratorAllowingDiagnostics(
        string yaml,
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
            additionalTexts: new AdditionalText[] { new InMemoryAdditionalText("test.packet.yml", yaml) },
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
