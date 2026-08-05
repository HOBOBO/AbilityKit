using System.Collections.Immutable;
using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaProjectileEmitterAnalyzerTests
{
    [Fact]
    public async Task Analyze_ReportsInvalidEmitterShapes()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba;
                using AbilityKit.Demo.Moba.Services.Projectile.Launch;

                [MobaProjectileEmitter(ProjectileEmitterType.Linear)]
                internal sealed class MissingInterface { }

                [MobaProjectileEmitter(ProjectileEmitterType.Linear)]
                internal abstract class AbstractEmitter : IMobaProjectileLaunchSequence { }

                [MobaProjectileEmitter(ProjectileEmitterType.Linear)]
                internal sealed class GenericEmitter<T> : IMobaProjectileLaunchSequence { }

                internal static class Owner
                {
                    [MobaProjectileEmitter(ProjectileEmitterType.Linear)]
                    private sealed class HiddenEmitter : IMobaProjectileLaunchSequence { }
                }

                [MobaProjectileEmitter(ProjectileEmitterType.Linear)]
                internal sealed class MissingConstructorEmitter : IMobaProjectileLaunchSequence
                {
                    public MissingConstructorEmitter(int dependency) { }
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Equal(5, diagnostics.Count(item => item.Id == MobaDiagnosticIds.InvalidProjectileEmitterRuleId));
    }

    [Fact]
    public async Task Analyze_ReportsNonEnumEmitterValue()
    {
        const string source = """
            using System;

            namespace AbilityKit.Demo.Moba
            {
                internal enum ProjectileEmitterType { None = 0, Linear = 1 }
            }

            namespace AbilityKit.Demo.Moba.Services.Projectile.Launch
            {
                [AttributeUsage(AttributeTargets.Class)]
                internal sealed class MobaProjectileEmitterAttribute : Attribute
                {
                    public MobaProjectileEmitterAttribute(string emitterType) { }
                }

                internal interface IMobaProjectileLaunchSequence { }
            }

            namespace Game
            {
                using AbilityKit.Demo.Moba.Services.Projectile.Launch;

                [MobaProjectileEmitter("bad")]
                internal sealed class InvalidEmitter : IMobaProjectileLaunchSequence { }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(
            diagnostics,
            item => item.Id == MobaDiagnosticIds.InvalidProjectileEmitterRuleId);
        Assert.Contains("compile-time enum value", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyze_ReportsSameEmitterAndPriorityOnce()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba;
                using AbilityKit.Demo.Moba.Services.Projectile.Launch;

                [MobaProjectileEmitter(ProjectileEmitterType.Linear, Priority = 3)]
                internal sealed class First : IMobaProjectileLaunchSequence { }

                [MobaProjectileEmitter(ProjectileEmitterType.Linear, Priority = 3)]
                internal sealed class Second : IMobaProjectileLaunchSequence { }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Single(
            diagnostics,
            item => item.Id == MobaDiagnosticIds.AmbiguousProjectileEmitterRuleId);
    }

    [Fact]
    public async Task Analyze_AllowsSameEmitterAtDifferentPriorities()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba;
                using AbilityKit.Demo.Moba.Services.Projectile.Launch;

                [MobaProjectileEmitter(ProjectileEmitterType.Linear, Priority = 2)]
                internal sealed class Lower : IMobaProjectileLaunchSequence { }

                [MobaProjectileEmitter(ProjectileEmitterType.Linear, Priority = 5)]
                internal sealed class Higher : IMobaProjectileLaunchSequence { }
            }
            """;

        Assert.Empty(await GetDiagnosticsAsync(source));
    }

    [Fact]
    public async Task GeneratorAndAnalyzer_ReportAmbiguityOnlyOnce()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba;
                using AbilityKit.Demo.Moba.Services.Projectile.Launch;

                [MobaProjectileEmitter(ProjectileEmitterType.Linear, Priority = 3)]
                internal sealed class First : IMobaProjectileLaunchSequence { }

                [MobaProjectileEmitter(ProjectileEmitterType.Linear, Priority = 3)]
                internal sealed class Second : IMobaProjectileLaunchSequence { }
            }
            """;
        var compilation = RoslynTestCompilation.Create(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaProjectileEmitterManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
        var analyzerDiagnostics = await outputCompilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MobaProjectileEmitterAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.DoesNotContain(
            generatorDiagnostics,
            item => item.Id.StartsWith("AKSG500", StringComparison.Ordinal));
        Assert.Single(
            analyzerDiagnostics,
            item => item.Id == MobaDiagnosticIds.AmbiguousProjectileEmitterRuleId);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = RoslynTestCompilation.Create(source);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new MobaProjectileEmitterAnalyzer());
        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
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
