using System.Collections.Immutable;
using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaSnapshotEmitterAnalyzerTests
{
    [Fact]
    public async Task Analyze_ReportsInvalidRuntimeEmitterShapes()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                [MobaSnapshotEmitter(1)]
                internal sealed class MissingInterface { }

                [MobaSnapshotEmitter(2)]
                internal abstract class AbstractEmitter : IMobaSnapshotEmitter { }

                [MobaSnapshotEmitter(3)]
                internal sealed class GenericEmitter<T> : IMobaSnapshotEmitter { }

                internal static class Owner
                {
                    [MobaSnapshotEmitter(4)]
                    private sealed class HiddenEmitter : IMobaSnapshotEmitter { }
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Equal(4, diagnostics.Count(item => item.Id == MobaDiagnosticIds.InvalidSnapshotEmitterRuleId));
    }

    [Fact]
    public async Task Analyze_ReportsNonIntegerPriority()
    {
        const string source = """
            using System;

            namespace AbilityKit.Demo.Moba.Services
            {
                [AttributeUsage(AttributeTargets.Class)]
                internal sealed class MobaSnapshotEmitterAttribute : Attribute
                {
                    public MobaSnapshotEmitterAttribute(string priority) { }
                }

                internal interface IMobaSnapshotEmitter { }

                internal static partial class MobaGeneratedSnapshotEmitterManifest { }
            }

            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                [MobaSnapshotEmitter("bad")]
                internal sealed class InvalidPriorityEmitter : IMobaSnapshotEmitter { }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(
            diagnostics,
            item => item.Id == MobaDiagnosticIds.InvalidSnapshotEmitterRuleId);
        Assert.Contains("compile-time int value", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyze_AllowsEmitterWithoutParameterlessConstructor()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                [MobaSnapshotEmitter(10)]
                internal sealed class DiConstructedEmitter : IMobaSnapshotEmitter
                {
                    public DiConstructedEmitter(object dependency) { }
                }
            }
            """;

        Assert.Empty(await GetDiagnosticsAsync(source));
    }

    [Fact]
    public async Task Analyze_IgnoresExternalReflectionEmitterWithoutSourceManifest()
    {
        const string source = ExternalContractSource + """
            namespace External.Game
            {
                using AbilityKit.Demo.Moba.Services;

                internal static class Owner
                {
                    [MobaSnapshotEmitter(999)]
                    private sealed class ReflectionEmitter { }
                }
            }
            """;

        Assert.Empty(await GetDiagnosticsAsync(source));
    }

    [Fact]
    public async Task GeneratorAndAnalyzer_ReportInvalidEmitterOnlyOnce()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                [MobaSnapshotEmitter(25)]
                internal sealed class InvalidEmitter { }
            }
            """;
        var compilation = RoslynTestCompilation.Create(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaSnapshotEmitterManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
        var analyzerDiagnostics = await outputCompilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MobaSnapshotEmitterAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.DoesNotContain(
            generatorDiagnostics,
            item => item.Id.StartsWith("AKSG800", StringComparison.Ordinal));
        Assert.Single(
            analyzerDiagnostics,
            item => item.Id == MobaDiagnosticIds.InvalidSnapshotEmitterRuleId);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = RoslynTestCompilation.Create(source);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new MobaSnapshotEmitterAnalyzer());
        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
    }

    private const string ContractSource = ExternalContractSource + """
        namespace AbilityKit.Demo.Moba.Services
        {
            internal sealed class MobaSnapshotEmitterRegistry
            {
                internal void Register(int priority, System.Type emitterType) { }
                internal bool TryRegisterGenerated(int priority, System.Type emitterType) => true;
            }

            internal static partial class MobaGeneratedSnapshotEmitterManifest
            {
                static partial void AddGenerated(MobaSnapshotEmitterRegistry registry, ref int count);
            }
        }
        """;

    private const string ExternalContractSource = """
        using System;

        namespace AbilityKit.Demo.Moba.Services
        {
            [AttributeUsage(AttributeTargets.Class)]
            internal sealed class MobaSnapshotEmitterAttribute : Attribute
            {
                public MobaSnapshotEmitterAttribute(int priority) { }
            }

            internal interface IMobaSnapshotEmitter { }
        }
        """;
}
