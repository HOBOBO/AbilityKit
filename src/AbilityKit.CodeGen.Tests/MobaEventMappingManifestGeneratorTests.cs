using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaEventMappingManifestGeneratorTests
{
    [Fact]
    public void Generate_CreatesDeterministicExactAndPrefixMappings()
    {
        var compilation = RoslynTestCompilation.Create(EventContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Systems;

                [MobaTriggerEvent("skill.cast", typeof(int))]
                [MobaTriggerEvent("skill.", typeof(string), true)]
                internal static class Mappings { }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaEventMappingManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        var exact = generated.IndexOf("AddExact<int>", StringComparison.Ordinal);
        var prefix = generated.IndexOf("AddPrefix<string>", StringComparison.Ordinal);
        Assert.True(exact >= 0);
        Assert.True(prefix > exact);
    }

    [Fact]
    public void Generate_FiltersDuplicateMappingWithoutReportingAnalyzerDiagnostic()
    {
        var compilation = RoslynTestCompilation.Create(EventContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Systems;

                [MobaTriggerEvent("duplicate", typeof(int))]
                [MobaTriggerEvent("duplicate", typeof(string))]
                internal static class Mappings { }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaEventMappingManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.DoesNotContain(diagnostics, item => item.Id.StartsWith("AKSG300", StringComparison.Ordinal));
        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        Assert.DoesNotContain("duplicate", generated, StringComparison.Ordinal);
    }

    private const string EventContractSource = """
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
