using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaBootstrapStageManifestGeneratorTests
{
    [Fact]
    public void Generate_RegistersConcreteAttributedStagesInStableOrder()
    {
        var compilation = RoslynTestCompilation.Create(ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Systems.Bootstrap.Flow;

                [MobaBootstrapStage]
                internal sealed class WorldStage : MobaBootstrapStageBase
                {
                    public override string Name => "World";
                }

                [MobaBootstrapStage]
                internal sealed class CoreStage : MobaBootstrapStageBase
                {
                    public override string Name => StageNames.Core;
                }

                internal static class StageNames
                {
                    public const string Core = "Core";
                }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaBootstrapStageManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        var coreIndex = generated.IndexOf("new global::Game.CoreStage()", StringComparison.Ordinal);
        var worldIndex = generated.IndexOf("new global::Game.WorldStage()", StringComparison.Ordinal);
        Assert.True(coreIndex >= 0 && worldIndex > coreIndex);
    }

    [Fact]
    public void Generate_OmitsStaticallyResolvableDuplicateStageNames()
    {
        var compilation = RoslynTestCompilation.Create(ContractSource + """
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
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaBootstrapStageManifestGenerator());
        driver = driver.RunGenerators(compilation);

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        Assert.DoesNotContain("global::Game.First", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("global::Game.Second", generated, StringComparison.Ordinal);
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

            internal static class MobaBootstrapStageRegistry
            {
                internal static void Register(MobaBootstrapStageBase stage) { }
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
