using System.Collections.Immutable;
using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaBootstrapStageAnalyzerTests
{
    [Fact]
    public async Task Analyze_ReportsInvalidStageShapes()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Systems.Bootstrap.Flow;

                [MobaBootstrapStage]
                internal sealed class NotAStage { }

                [MobaBootstrapStage]
                internal abstract class AbstractStage : MobaBootstrapStageBase { }

                [MobaBootstrapStage]
                internal sealed class GenericStage<T> : MobaBootstrapStageBase { }

                [MobaBootstrapStage]
                internal sealed class NoDefaultConstructorStage : MobaBootstrapStageBase
                {
                    public NoDefaultConstructorStage(int dependency) { }
                }

                internal static class Owner
                {
                    [MobaBootstrapStage]
                    private sealed class HiddenStage : MobaBootstrapStageBase { }
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Equal(5, diagnostics.Count(item => item.Id == MobaDiagnosticIds.InvalidBootstrapStageRuleId));
    }

    [Fact]
    public async Task Analyze_ReportsStaticallyResolvableDuplicateStageNameOnce()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Systems.Bootstrap.Flow;

                [MobaBootstrapStage]
                internal sealed class First : MobaBootstrapStageBase
                {
                    public override string Name => "Duplicate";
                }

                [MobaBootstrapStage]
                internal sealed class Second : MobaBootstrapStageBase
                {
                    public override string Name => StageNames.Duplicate;
                }

                internal static class StageNames
                {
                    public const string Duplicate = "Duplicate";
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Single(diagnostics, item => item.Id == MobaDiagnosticIds.DuplicateBootstrapStageNameRuleId);
    }

    [Fact]
    public async Task Analyze_AcceptsValidStage()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Systems.Bootstrap.Flow;

                [MobaBootstrapStage]
                internal sealed class ValidStage : MobaBootstrapStageBase
                {
                    public override string Name => "Valid";
                }
            }
            """;

        Assert.Empty(await GetDiagnosticsAsync(source));
    }

    [Fact]
    public async Task GeneratorAndAnalyzer_ReportDuplicateOnlyOnce()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Systems.Bootstrap.Flow;

                [MobaBootstrapStage]
                internal sealed class First : MobaBootstrapStageBase
                {
                    public override string Name => "Duplicate";
                }

                [MobaBootstrapStage]
                internal sealed class Second : MobaBootstrapStageBase
                {
                    public override string Name => "Duplicate";
                }
            }
            """;
        var compilation = RoslynTestCompilation.Create(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaBootstrapStageManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
        var analyzerDiagnostics = await outputCompilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MobaBootstrapStageAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.DoesNotContain(
            generatorDiagnostics,
            item => item.Id.StartsWith("AKSG600", StringComparison.Ordinal));
        Assert.Single(
            analyzerDiagnostics,
            item => item.Id == MobaDiagnosticIds.DuplicateBootstrapStageNameRuleId);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = RoslynTestCompilation.Create(source);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new MobaBootstrapStageAnalyzer());
        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
    }

    private const string ContractSource = """
        using System;

        namespace AbilityKit.Demo.Moba.Systems.Bootstrap.Flow
        {
            [AttributeUsage(AttributeTargets.Class)]
            internal sealed class MobaBootstrapStageAttribute : Attribute { }

            internal abstract class MobaBootstrapStageBase
            {
                public virtual string Name => GetType().Name;
            }

            internal static partial class MobaGeneratedBootstrapStageManifest
            {
                static partial void AddGenerated(ref int count);
                private static void Register(
                    Func<MobaBootstrapStageBase> factory,
                    string stageTypeName,
                    ref int count) { }
            }
        }
        """;
}
