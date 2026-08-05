using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaTargetQueryFactoryManifestGeneratorTests
{
    [Fact]
    public void Generate_RegistersAllFactoryKinds()
    {
        var compilation = RoslynTestCompilation.Create(ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services.Search;

                [MobaTargetSourceProvider(1)]
                internal sealed class Source : IMobaTargetSourceFactory { }
                [MobaTargetFilter(2)]
                internal sealed class Filter : IMobaTargetFilterFactory { }
                [MobaTargetOrder(3)]
                internal sealed class Order : IMobaTargetOrderFactory { }
                [MobaTargetSelect(4)]
                internal sealed class Select : IMobaTargetSelectFactory { }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaTargetQueryFactoryManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        Assert.Contains("registry.RegisterSource(1, new global::Game.Source());", generated, StringComparison.Ordinal);
        Assert.Contains("registry.RegisterFilter(2, new global::Game.Filter());", generated, StringComparison.Ordinal);
        Assert.Contains("registry.RegisterOrder(3, new global::Game.Order());", generated, StringComparison.Ordinal);
        Assert.Contains("registry.RegisterSelect(4, new global::Game.Select());", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_FiltersDuplicateCodeWithinKindWithoutReportingAnalyzerDiagnostic()
    {
        var compilation = RoslynTestCompilation.Create(ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services.Search;

                [MobaTargetFilter(7)]
                internal sealed class First : IMobaTargetFilterFactory { }
                [MobaTargetFilter(7)]
                internal sealed class Second : IMobaTargetFilterFactory { }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaTargetQueryFactoryManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.DoesNotContain(diagnostics, item => item.Id.StartsWith("AKSG400", StringComparison.Ordinal));
        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        Assert.DoesNotContain("global::Game.First", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("global::Game.Second", generated, StringComparison.Ordinal);
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
