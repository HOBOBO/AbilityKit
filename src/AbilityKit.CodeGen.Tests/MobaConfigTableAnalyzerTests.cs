using System.Collections.Immutable;
using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaConfigTableAnalyzerTests
{
    [Fact]
    public async Task Analyze_ReportsMissingDtoKeyAndMoConstructor()
    {
        var source = MobaConfigTableManifestGeneratorTests.SourceWithDeclarations("""
            [assembly: AbilityKit.Demo.Moba.Config.Core.MobaConfigTable(
                "missing-key", typeof(Invalid.MissingKeyDTO), typeof(Invalid.MissingKeyMO), "LegacyJson", 10)]
            [assembly: AbilityKit.Demo.Moba.Config.Core.MobaConfigTable(
                "missing-ctor", typeof(Invalid.ValidDTO), typeof(Invalid.MissingConstructorMO), "LegacyJson", 20)]
            """) + """
            namespace Invalid
            {
                public sealed class MissingKeyDTO { }
                public sealed class MissingKeyMO { public MissingKeyMO(MissingKeyDTO dto) { } }
                public sealed class ValidDTO { public int Id; }
                public sealed class MissingConstructorMO { }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Equal(2, diagnostics.Count(item => item.Id == MobaDiagnosticIds.InvalidConfigTableRuleId));
    }

    [Fact]
    public async Task Analyze_ReportsDuplicateFilePath()
    {
        var source = MobaConfigTableManifestGeneratorTests.SourceWithDeclarations("""
            [assembly: AbilityKit.Demo.Moba.Config.Core.MobaConfigTable(
                "same", typeof(Game.CharacterDTO), typeof(Game.CharacterMO), "LegacyJson", 10)]
            [assembly: AbilityKit.Demo.Moba.Config.Core.MobaConfigTable(
                "same", typeof(Game.LaterDTO), typeof(Game.LaterMO), "LegacyJson", 20)]
            """);

        var diagnostic = Assert.Single(
            await GetDiagnosticsAsync(source),
            item => item.Id == MobaDiagnosticIds.DuplicateConfigTableRuleId);
        Assert.Contains("file path", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyze_RejectsValueTypeDtoAndMoBeforeGeneration()
    {
        var source = MobaConfigTableManifestGeneratorTests.SourceWithDeclarations("""
            [assembly: AbilityKit.Demo.Moba.Config.Core.MobaConfigTable(
                "structs", typeof(Invalid.Dto), typeof(Invalid.Mo), "LegacyJson", 10)]
            """) + """
            namespace Invalid
            {
                public struct Dto { public int Id; }
                public struct Mo { public Mo(Dto dto) { } }
            }
            """;

        Assert.Single(
            await GetDiagnosticsAsync(source),
            item => item.Id == MobaDiagnosticIds.InvalidConfigTableRuleId);
    }

    [Fact]
    public async Task Analyze_AcceptsInheritedPublicDtoKey()
    {
        var source = MobaConfigTableManifestGeneratorTests.SourceWithDeclarations("""
            [assembly: AbilityKit.Demo.Moba.Config.Core.MobaConfigTable(
                "inherited", typeof(Valid.Dto), typeof(Valid.Mo), "LegacyJson", 10)]
            """) + """
            namespace Valid
            {
                public abstract class DtoBase { public int Id { get; set; } }
                public sealed class Dto : DtoBase { }
                public sealed class Mo { public Mo(Dto dto) { } }
            }
            """;

        Assert.Empty(await GetDiagnosticsAsync(source));
    }

    [Fact]
    public async Task GeneratorAndAnalyzer_ReportInvalidDeclarationOnlyOnce()
    {
        var source = MobaConfigTableManifestGeneratorTests.SourceWithDeclarations("""
            [assembly: AbilityKit.Demo.Moba.Config.Core.MobaConfigTable(
                "invalid", typeof(Invalid.Dto), typeof(Invalid.Mo), "LegacyJson", 10)]
            """) + """
            namespace Invalid
            {
                public sealed class Dto { }
                public sealed class Mo { }
            }
            """;
        var compilation = RoslynTestCompilation.Create(source);
        GeneratorDriver driver = Microsoft.CodeAnalysis.CSharp.CSharpGeneratorDriver.Create(
            new MobaConfigTableManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);
        var analyzerDiagnostics = await outputCompilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MobaConfigTableAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.DoesNotContain(
            generatorDiagnostics,
            item => item.Id.StartsWith("AKSG100", StringComparison.Ordinal));
        Assert.Single(
            analyzerDiagnostics,
            item => item.Id == MobaDiagnosticIds.InvalidConfigTableRuleId);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = RoslynTestCompilation.Create(source);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new MobaConfigTableAnalyzer());
        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
    }
}
