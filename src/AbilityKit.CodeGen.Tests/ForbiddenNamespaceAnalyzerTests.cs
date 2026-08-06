using System.Collections.Immutable;
using System.Text;
using AbilityKit.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class ForbiddenNamespaceAnalyzerTests
{
    [Fact]
    public async Task Analyze_ReportsForbiddenNamespaceFromAdditionalConfig()
    {
        var diagnostics = await AnalyzeAsync(
            "using Forbidden.Api; public sealed class Usage { }",
            Config("GeneratorTests", forbiddenNamespace: "Forbidden"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticIds.ForbiddenNamespaceAnalyzerRuleId, diagnostic.Id);
        Assert.Equal("Forbidden.Api", diagnostic.GetMessage().Split('\'')[1]);
    }

    [Fact]
    public async Task Analyze_ReportsForbiddenReferencedAssembly()
    {
        var diagnostics = await AnalyzeAsync(
            "public sealed class Usage { }",
            Config("GeneratorTests", forbiddenAssembly: "System.Runtime"));

        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.Id == DiagnosticIds.ForbiddenAssemblyAnalyzerRuleId &&
                          diagnostic.GetMessage().Contains("System.Runtime", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Analyze_UsesEnabledAndAliasDefaultsWhenFieldsAreOmitted()
    {
        var diagnostics = await AnalyzeAsync(
            "using Forbidden.Api; public sealed class Usage { }",
            """
            {
              "constraints": {
                "GeneratorTests": {
                  "forbiddenNamespaces": ["Forbidden"]
                }
              }
            }
            """);

        Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == DiagnosticIds.ForbiddenNamespaceAnalyzerRuleId);
    }

    [Fact]
    public async Task Analyze_ReportsOnlyConstraintNamesMissingFromProvidedAsmdefs()
    {
        var compilation = RoslynTestCompilation.Create("public sealed class Usage { }")
            .WithOptions(new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                specificDiagnosticOptions: new Dictionary<string, ReportDiagnostic>
                {
                    [DiagnosticIds.UnmatchedConstraintPackageRuleId] = ReportDiagnostic.Warn,
                }));
        var options = new AnalyzerOptions(ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText("PackageConstraints.json", """
                {
                  "constraints": {
                    "Known.Assembly": { "packageName": "Known.Assembly" },
                    "Missing.Assembly": { "packageName": "Missing.Assembly" }
                  }
                }
                """),
            new InMemoryAdditionalText("Known.Assembly.asmdef", "{ \"name\": \"Known.Assembly\" }")));

        var diagnostics = await compilation
            .WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ForbiddenNamespaceAnalyzer()),
                options)
            .GetAnalyzerDiagnosticsAsync();

        var diagnostic = Assert.Single(
            diagnostics,
            item => item.Id == DiagnosticIds.UnmatchedConstraintPackageRuleId);
        Assert.Contains("Missing.Assembly", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyze_DoesNothingWithoutExplicitConfig()
    {
        var compilation = RoslynTestCompilation.Create("using Forbidden.Api; public sealed class Usage { }");

        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ForbiddenNamespaceAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.Empty(diagnostics);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, string config)
    {
        var compilation = RoslynTestCompilation.Create(source);
        var options = new AnalyzerOptions(ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText("PackageConstraints.json", config)));
        return await compilation
            .WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ForbiddenNamespaceAnalyzer()),
                options)
            .GetAnalyzerDiagnosticsAsync();
    }

    private static string Config(
        string assemblyName,
        string? forbiddenNamespace = null,
        string? forbiddenAssembly = null)
    {
        var namespaces = forbiddenNamespace == null ? string.Empty : $"\"{forbiddenNamespace}\"";
        var assemblies = forbiddenAssembly == null ? string.Empty : $"\"{forbiddenAssembly}\"";
        return $$"""
            {
              "constraints": {
                "{{assemblyName}}": {
                  "packageName": "{{assemblyName}}",
                  "forbiddenNamespaces": [{{namespaces}}],
                  "forbiddenAssemblies": [{{assemblies}}],
                  "isEnabled": true,
                  "checkUsingAliases": true
                }
              }
            }
            """;
    }

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        public InMemoryAdditionalText(string path, string text)
        {
            Path = path;
            _text = SourceText.From(text, Encoding.UTF8);
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }
}
