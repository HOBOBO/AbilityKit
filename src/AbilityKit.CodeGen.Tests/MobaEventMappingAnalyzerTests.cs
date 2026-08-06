using System.Collections.Immutable;
using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaEventMappingAnalyzerTests
{
    [Fact]
    public async Task Analyze_ReportsInvalidMappingArguments()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Systems;

                [MobaTriggerEvent(" ", typeof(int))]
                internal static class InvalidMapping { }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Single(diagnostics, item => item.Id == MobaDiagnosticIds.InvalidEventMappingRuleId);
    }

    [Fact]
    public async Task Analyze_ReportsDuplicateWithinSameKindOnce()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Systems;

                [MobaTriggerEvent("duplicate", typeof(int))]
                internal static class First { }

                [MobaTriggerEvent("duplicate", typeof(string))]
                internal static class Second { }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Single(diagnostics, item => item.Id == MobaDiagnosticIds.DuplicateEventMappingRuleId);
    }

    [Fact]
    public async Task Analyze_RejectsArgsTypeHiddenFromGeneratedManifest()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Systems;

                internal static class Container
                {
                    private sealed class HiddenArgs { }

                    [MobaTriggerEvent("hidden", typeof(HiddenArgs))]
                    internal sealed class Mapping { }
                }
            }
            """;

        Assert.Single(
            await GetDiagnosticsAsync(source),
            item => item.Id == MobaDiagnosticIds.InvalidEventMappingRuleId);
    }

    [Fact]
    public async Task Analyze_AllowsSameTextForExactAndPrefixMappings()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Systems;

                [MobaTriggerEvent("shared", typeof(int))]
                [MobaTriggerEvent("shared", typeof(string), true)]
                internal static class ValidMappings { }
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
                using AbilityKit.Demo.Moba.Systems;

                [MobaTriggerEvent("duplicate", typeof(int))]
                [MobaTriggerEvent("duplicate", typeof(string))]
                internal static class Mappings { }
            }
            """;
        var compilation = RoslynTestCompilation.Create(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaEventMappingManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
        var analyzerDiagnostics = await outputCompilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MobaEventMappingAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.DoesNotContain(
            generatorDiagnostics,
            item => item.Id.StartsWith("AKSG300", StringComparison.Ordinal));
        Assert.Single(
            analyzerDiagnostics,
            item => item.Id == MobaDiagnosticIds.DuplicateEventMappingRuleId);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = RoslynTestCompilation.Create(source);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new MobaEventMappingAnalyzer());
        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
    }

    private const string ContractSource = """
        using System;

        namespace AbilityKit.Demo.Moba.Systems
        {
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
            public sealed class MobaTriggerEventAttribute : Attribute
            {
                public MobaTriggerEventAttribute(string eventId, Type argsType, bool isPrefix = false) { }
            }

            public sealed class MobaEventSubscriptionRegistry { }

            internal static partial class MobaGeneratedEventMappingManifest
            {
                static partial void AddGenerated(MobaEventSubscriptionRegistry registry, ref int count);
                private static void AddExact<T>(MobaEventSubscriptionRegistry registry, string eventId, ref int count) { }
                private static void AddPrefix<T>(MobaEventSubscriptionRegistry registry, string prefix, ref int count) { }
            }
        }
        """;
}
