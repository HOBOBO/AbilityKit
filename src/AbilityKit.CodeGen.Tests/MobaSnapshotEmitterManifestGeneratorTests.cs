using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaSnapshotEmitterManifestGeneratorTests
{
    [Fact]
    public void Generate_RegistersEmitterPriorityAndType()
    {
        var compilation = RoslynTestCompilation.Create(ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                [MobaSnapshotEmitter(25)]
                internal sealed class ActorSnapshotEmitter : IMobaSnapshotEmitter { }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaSnapshotEmitterManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        Assert.Contains(
            "if (registry.TryRegisterGenerated(25, typeof(global::Game.ActorSnapshotEmitter))) count++;",
            generated,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_FiltersInvalidEmitterWithoutReportingAnalyzerDiagnostic()
    {
        var compilation = RoslynTestCompilation.Create(ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                [MobaSnapshotEmitter(25)]
                internal sealed class InvalidEmitter { }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaSnapshotEmitterManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.DoesNotContain(diagnostics, item => item.Id.StartsWith("AKSG800", StringComparison.Ordinal));
        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        Assert.DoesNotContain("global::Game.InvalidEmitter", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_OrdersEmittersByPriorityThenQualifiedTypeName()
    {
        var compilation = RoslynTestCompilation.Create(ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                [MobaSnapshotEmitter(20)]
                internal sealed class Later : IMobaSnapshotEmitter { }
                [MobaSnapshotEmitter(10)]
                internal sealed class Zebra : IMobaSnapshotEmitter { }
                [MobaSnapshotEmitter(10)]
                internal sealed class Alpha : IMobaSnapshotEmitter { }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaSnapshotEmitterManifestGenerator());
        driver = driver.RunGenerators(compilation);

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        var alpha = generated.IndexOf("global::Game.Alpha", StringComparison.Ordinal);
        var zebra = generated.IndexOf("global::Game.Zebra", StringComparison.Ordinal);
        var later = generated.IndexOf("global::Game.Later", StringComparison.Ordinal);
        Assert.True(alpha >= 0);
        Assert.True(zebra > alpha);
        Assert.True(later > zebra);
    }

    private const string ContractSource = """
        using System;

        namespace AbilityKit.Demo.Moba.Services
        {
            [AttributeUsage(AttributeTargets.Class)]
            internal sealed class MobaSnapshotEmitterAttribute : Attribute
            {
                public MobaSnapshotEmitterAttribute(int priority) { }
            }

            internal interface IMobaSnapshotEmitter { }

            internal sealed class MobaSnapshotEmitterRegistry
            {
                internal void Register(int priority, Type emitterType) { }
                internal bool TryRegisterGenerated(int priority, Type emitterType) => true;
            }

            internal static partial class MobaGeneratedSnapshotEmitterManifest
            {
                static partial void AddGenerated(MobaSnapshotEmitterRegistry registry, ref int count);
            }
        }
        """;
}
