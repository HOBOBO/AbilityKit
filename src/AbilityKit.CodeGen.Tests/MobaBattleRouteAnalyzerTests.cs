using System.Collections.Immutable;
using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaBattleRouteAnalyzerTests
{
    [Fact]
    public async Task Analyze_ReportsInvalidHandlerShape()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                [MobaInputCommandHandler(7)]
                internal sealed class InvalidHandler { }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Single(diagnostics, item => item.Id == MobaDiagnosticIds.InvalidInputCommandHandlerRuleId);
    }

    [Fact]
    public async Task Analyze_ReportsDuplicateInputOpCodeOnce()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                [MobaInputCommandHandler(7)]
                internal sealed class FirstHandler : IMobaInputCommandHandler { }

                [MobaInputCommandHandler(7)]
                internal sealed class SecondHandler : IMobaInputCommandHandler { }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Single(diagnostics, item => item.Id == MobaDiagnosticIds.DuplicateBattleRouteRuleId);
    }

    [Fact]
    public async Task Analyze_ReportsInvalidRouteIdentities()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                [MobaBattleRoute(0, MobaBattleRouteKind.RuntimeInput)]
                internal sealed class ZeroOpCodeRoute { }

                [MobaBattleRoute(8, MobaBattleRouteKind.Unknown)]
                internal sealed class UnknownKindRoute { }

                [MobaInputCommandHandler(0)]
                internal sealed class ZeroOpCodeHandler : IMobaInputCommandHandler { }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Equal(3, diagnostics.Count(item => item.Id == MobaDiagnosticIds.InvalidBattleRouteIdentityRuleId));
    }

    [Fact]
    public async Task Analyze_WarnsWhenHandlerRequiresDiConstruction()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                [MobaInputCommandHandler(7)]
                internal sealed class DiOnlyHandler : IMobaInputCommandHandler
                {
                    public DiOnlyHandler(int dependency) { }
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        var diagnostic = Assert.Single(
            diagnostics,
            item => item.Id == MobaDiagnosticIds.MissingInputHandlerFallbackConstructorRuleId);

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.DoesNotContain(diagnostics, item => item.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Analyze_RejectsRouteTypesHiddenFromGeneratedManifest()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                internal static class Container
                {
                    private sealed class HiddenPayload { }

                    [MobaBattleRoute(9, MobaBattleRouteKind.Protocol, PayloadType = typeof(HiddenPayload))]
                    internal sealed class Route { }

                    [MobaBattleRoute(10, MobaBattleRouteKind.Protocol)]
                    private sealed class HiddenRoute { }
                }
            }
            """;

        Assert.Equal(
            2,
            (await GetDiagnosticsAsync(source)).Count(
                item => item.Id == MobaDiagnosticIds.InvalidBattleRouteTypeRuleId));
    }

    [Fact]
    public async Task Analyze_ReportsUnsupportedDerivedRouteAttribute()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                internal sealed class CustomRouteAttribute : MobaBattleRouteAttribute
                {
                    public CustomRouteAttribute(int opCode)
                        : base(opCode, MobaBattleRouteKind.RuntimeInput) { }
                }

                [CustomRoute(9)]
                internal sealed class CustomRouteOwner { }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Single(diagnostics, item => item.Id == MobaDiagnosticIds.UnsupportedBattleRouteAttributeRuleId);
    }

    [Fact]
    public async Task Analyze_AcceptsValidInputHandler()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                [MobaInputCommandHandler(7)]
                internal sealed class ValidHandler : IMobaInputCommandHandler { }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task GeneratorAndAnalyzer_ReportDuplicateOnlyOnce()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                [MobaInputCommandHandler(7)]
                internal sealed class FirstHandler : IMobaInputCommandHandler { }

                [MobaInputCommandHandler(7)]
                internal sealed class SecondHandler : IMobaInputCommandHandler { }
            }
            """;
        var compilation = RoslynTestCompilation.Create(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaBattleRouteManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
        var analyzerDiagnostics = await outputCompilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MobaBattleRouteAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.DoesNotContain(
            generatorDiagnostics,
            item => item.Id.StartsWith("AKSG900", StringComparison.Ordinal));
        Assert.Single(
            analyzerDiagnostics,
            item => item.Id == MobaDiagnosticIds.DuplicateBattleRouteRuleId);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = RoslynTestCompilation.Create(source);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new MobaBattleRouteAnalyzer());
        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
    }

    private const string ContractSource = """
        using System;

        namespace AbilityKit.Demo.Moba.Services
        {
            internal enum MobaBattleRouteKind { Unknown = 0, RuntimeInput = 1, Protocol = 2 }

            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
            internal class MobaBattleRouteAttribute : Attribute
            {
                public MobaBattleRouteAttribute(int opCode, MobaBattleRouteKind kind) { }
                public Type PayloadType { get; set; }
                public Type HandlerType { get; set; }
                public string Name { get; set; }
            }

            [AttributeUsage(AttributeTargets.Class)]
            internal sealed class MobaInputCommandHandlerAttribute : MobaBattleRouteAttribute
            {
                public MobaInputCommandHandlerAttribute(int opCode)
                    : base(opCode, MobaBattleRouteKind.RuntimeInput) { }
            }

            internal interface IMobaInputCommandHandler { }
        }
        """;
}
