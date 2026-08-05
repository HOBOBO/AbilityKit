using System.Collections.Immutable;
using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaTargetQueryFactoryAnalyzerTests
{
    [Fact]
    public async Task Analyze_ReportsInvalidFactoryShapes()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services.Search;

                [MobaTargetFilter(1)]
                internal sealed class MissingInterface { }

                [MobaTargetFilter(2)]
                internal abstract class AbstractFactory : IMobaTargetFilterFactory { }

                [MobaTargetFilter(3)]
                internal sealed class GenericFactory<T> : IMobaTargetFilterFactory { }

                internal static class Owner
                {
                    [MobaTargetFilter(4)]
                    private sealed class HiddenFactory : IMobaTargetFilterFactory { }
                }

                [MobaTargetFilter(5)]
                internal sealed class MissingConstructorFactory : IMobaTargetFilterFactory
                {
                    public MissingConstructorFactory(int dependency) { }
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Equal(5, diagnostics.Count(item => item.Id == MobaDiagnosticIds.InvalidTargetQueryFactoryRuleId));
    }

    [Fact]
    public async Task Analyze_ReportsNonIntegerFactoryCode()
    {
        const string source = """
            using System;

            namespace AbilityKit.Demo.Moba.Services.Search
            {
                [AttributeUsage(AttributeTargets.Class)]
                internal sealed class MobaTargetFilterAttribute : Attribute
                {
                    public MobaTargetFilterAttribute(string code) { }
                }

                internal interface IMobaTargetFilterFactory { }
            }

            namespace Game
            {
                using AbilityKit.Demo.Moba.Services.Search;

                [MobaTargetFilter("bad")]
                internal sealed class InvalidCodeFactory : IMobaTargetFilterFactory { }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(
            diagnostics,
            item => item.Id == MobaDiagnosticIds.InvalidTargetQueryFactoryRuleId);
        Assert.Contains("constant int", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyze_ReportsDuplicateCodeWithinKindOnce()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services.Search;

                [MobaTargetFilter(7)]
                internal sealed class First : IMobaTargetFilterFactory { }

                [MobaTargetFilter(7)]
                internal sealed class Second : IMobaTargetFilterFactory { }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Single(
            diagnostics,
            item => item.Id == MobaDiagnosticIds.DuplicateTargetQueryFactoryCodeRuleId);
    }

    [Fact]
    public async Task Analyze_AllowsSameCodeAcrossFactoryKinds()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services.Search;

                [MobaTargetSourceProvider(7)]
                internal sealed class Source : IMobaTargetSourceFactory { }

                [MobaTargetFilter(7)]
                internal sealed class Filter : IMobaTargetFilterFactory { }
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
                using AbilityKit.Demo.Moba.Services.Search;

                [MobaTargetFilter(7)]
                internal sealed class First : IMobaTargetFilterFactory { }

                [MobaTargetFilter(7)]
                internal sealed class Second : IMobaTargetFilterFactory { }
            }
            """;
        var compilation = RoslynTestCompilation.Create(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaTargetQueryFactoryManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
        var analyzerDiagnostics = await outputCompilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MobaTargetQueryFactoryAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.DoesNotContain(
            generatorDiagnostics,
            item => item.Id.StartsWith("AKSG400", StringComparison.Ordinal));
        Assert.Single(
            analyzerDiagnostics,
            item => item.Id == MobaDiagnosticIds.DuplicateTargetQueryFactoryCodeRuleId);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = RoslynTestCompilation.Create(source);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new MobaTargetQueryFactoryAnalyzer());
        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
    }

    private const string ContractSource = """
        using System;

        namespace AbilityKit.Demo.Moba.Services.Search
        {
            [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
            internal sealed class MobaTargetSourceProviderAttribute : Attribute { public MobaTargetSourceProviderAttribute(int code) { } }
            [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
            internal sealed class MobaTargetFilterAttribute : Attribute { public MobaTargetFilterAttribute(int code) { } }
            [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
            internal sealed class MobaTargetOrderAttribute : Attribute { public MobaTargetOrderAttribute(int code) { } }
            [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
            internal sealed class MobaTargetSelectAttribute : Attribute { public MobaTargetSelectAttribute(int code) { } }

            internal interface IMobaTargetSourceFactory { }
            internal interface IMobaTargetFilterFactory { }
            internal interface IMobaTargetOrderFactory { }
            internal interface IMobaTargetSelectFactory { }

            internal sealed class MobaTargetQueryFactoryRegistry
            {
                internal void RegisterSource(int code, IMobaTargetSourceFactory factory) { }
                internal void RegisterFilter(int code, IMobaTargetFilterFactory factory) { }
                internal void RegisterOrder(int code, IMobaTargetOrderFactory factory) { }
                internal void RegisterSelect(int code, IMobaTargetSelectFactory factory) { }
            }

            internal static partial class MobaGeneratedTargetQueryFactoryManifest
            {
                static partial void AddGenerated(MobaTargetQueryFactoryRegistry registry, ref int count);
            }
        }
        """;
}
