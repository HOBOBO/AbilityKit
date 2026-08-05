using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaProjectileEmitterManifestGeneratorTests
{
    [Fact]
    public void Generate_RegistersEmitterMetadata()
    {
        var compilation = RoslynTestCompilation.Create(ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba;
                using AbilityKit.Demo.Moba.Services.Projectile.Launch;

                [MobaProjectileEmitter(ProjectileEmitterType.Linear, Priority = 7, IsDefault = true)]
                internal sealed class LinearEmitter : IMobaProjectileLaunchSequence { }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaProjectileEmitterManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        Assert.Contains(
            "registry.Register((global::AbilityKit.Demo.Moba.ProjectileEmitterType)1, () => new global::Game.LinearEmitter(), 7, true);",
            generated,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_FiltersAmbiguousEmittersWithoutReportingAnalyzerDiagnostic()
    {
        var compilation = RoslynTestCompilation.Create(ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba;
                using AbilityKit.Demo.Moba.Services.Projectile.Launch;

                [MobaProjectileEmitter(ProjectileEmitterType.Linear, Priority = 3)]
                internal sealed class First : IMobaProjectileLaunchSequence { }
                [MobaProjectileEmitter(ProjectileEmitterType.Linear, Priority = 3)]
                internal sealed class Second : IMobaProjectileLaunchSequence { }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaProjectileEmitterManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.DoesNotContain(diagnostics, item => item.Id.StartsWith("AKSG500", StringComparison.Ordinal));
        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        Assert.DoesNotContain("global::Game.First", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("global::Game.Second", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_RegistersLowerPriorityBeforeHigherPriority()
    {
        var compilation = RoslynTestCompilation.Create(ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba;
                using AbilityKit.Demo.Moba.Services.Projectile.Launch;

                [MobaProjectileEmitter(ProjectileEmitterType.Linear, Priority = 7)]
                internal sealed class Higher : IMobaProjectileLaunchSequence { }
                [MobaProjectileEmitter(ProjectileEmitterType.Linear, Priority = -2)]
                internal sealed class Lower : IMobaProjectileLaunchSequence { }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaProjectileEmitterManifestGenerator());
        driver = driver.RunGenerators(compilation);

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        var lower = generated.IndexOf("global::Game.Lower(), -2", StringComparison.Ordinal);
        var higher = generated.IndexOf("global::Game.Higher(), 7", StringComparison.Ordinal);
        Assert.True(lower >= 0);
        Assert.True(higher > lower);
    }

    private const string ContractSource = """
        using System;

        namespace AbilityKit.Demo.Moba
        {
            internal enum ProjectileEmitterType { None = 0, Linear = 1 }
        }

        namespace AbilityKit.Demo.Moba.Services.Projectile.Launch
        {
            [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
            internal sealed class MobaProjectileEmitterAttribute : Attribute
            {
                public MobaProjectileEmitterAttribute(AbilityKit.Demo.Moba.ProjectileEmitterType emitterType) { }
                public int Priority { get; set; }
                public bool IsDefault { get; set; }
            }

            internal interface IMobaProjectileLaunchSequence { }

            internal sealed class MobaProjectileEmitterRegistry
            {
                internal void Register(
                    AbilityKit.Demo.Moba.ProjectileEmitterType emitterType,
                    Func<IMobaProjectileLaunchSequence> factory,
                    int priority,
                    bool isDefault) { }
            }

            internal static partial class MobaGeneratedProjectileEmitterManifest
            {
                static partial void AddGenerated(MobaProjectileEmitterRegistry registry, ref int count);
            }
        }
        """;
}
