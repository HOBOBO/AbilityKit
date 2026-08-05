using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaBattleRouteManifestGeneratorTests
{
    [Fact]
    public void Generate_RegistersRouteDescriptorAndInputHandler()
    {
        var compilation = RoslynTestCompilation.Create(ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                internal sealed class MovePayload { }

                [MobaInputCommandHandler(7, PayloadType = typeof(MovePayload), Name = "Move")]
                internal sealed class MoveHandler : IMobaInputCommandHandler { }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaBattleRouteManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        Assert.Contains(
            "new MobaBattleRouteDescriptor(7, (MobaBattleRouteKind)1, typeof(global::Game.MoveHandler), typeof(global::Game.MovePayload), typeof(global::Game.MoveHandler), \"Move\")",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "registry.Register(7, typeof(global::Game.MoveHandler));",
            generated,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_OmitsDuplicateInputOpCode()
    {
        var compilation = RoslynTestCompilation.Create(ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                [MobaInputCommandHandler(7)]
                internal sealed class FirstHandler : IMobaInputCommandHandler { }
                [MobaInputCommandHandler(7)]
                internal sealed class SecondHandler : IMobaInputCommandHandler { }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaBattleRouteManifestGenerator());
        driver = driver.RunGenerators(compilation);

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        Assert.DoesNotContain("registry.Register(7", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_OmitsHandlerThatDoesNotImplementContract()
    {
        var compilation = RoslynTestCompilation.Create(ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                [MobaInputCommandHandler(7)]
                internal sealed class InvalidHandler { }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaBattleRouteManifestGenerator());
        driver = driver.RunGenerators(compilation);

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        Assert.DoesNotContain("InvalidHandler", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_RegistersDiOnlyHandlerWithoutActivatorFallback()
    {
        var compilation = RoslynTestCompilation.Create(ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                [MobaInputCommandHandler(8)]
                internal sealed class DiOnlyHandler : IMobaInputCommandHandler
                {
                    public DiOnlyHandler(int dependency) { }
                }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaBattleRouteManifestGenerator());
        driver = driver.RunGenerators(compilation);

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        Assert.Contains(
            "registry.Register(8, typeof(global::Game.DiOnlyHandler));",
            generated,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_OmitsInvalidRouteIdentities()
    {
        var compilation = RoslynTestCompilation.Create(ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba.Services;

                [MobaBattleRoute(0, MobaBattleRouteKind.RuntimeInput)]
                internal sealed class ZeroRoute { }

                [MobaInputCommandHandler(0)]
                internal sealed class ZeroHandler : IMobaInputCommandHandler { }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaBattleRouteManifestGenerator());
        driver = driver.RunGenerators(compilation);

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        Assert.DoesNotContain("ZeroRoute", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("ZeroHandler", generated, StringComparison.Ordinal);
    }

    private const string ContractSource = """
        using System;

        namespace AbilityKit.Demo.Moba.Services
        {
            internal enum MobaBattleRouteKind { Unknown = 0, RuntimeInput = 1 }

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

            internal readonly struct MobaBattleRouteDescriptor
            {
                public MobaBattleRouteDescriptor(
                    int opCode,
                    MobaBattleRouteKind kind,
                    Type ownerType,
                    Type payloadType,
                    Type handlerType,
                    string name) { }
            }

            internal sealed class MobaBattleRouteRegistry
            {
                internal bool Register(MobaBattleRouteDescriptor descriptor) => true;
            }

            internal sealed class MobaInputCommandHandlerRegistry
            {
                internal void Register(int opCode, Type handlerType) { }
            }

            internal static partial class MobaGeneratedBattleRouteManifest
            {
                static partial void AddGenerated(MobaBattleRouteRegistry registry, ref int count);
            }

            internal static partial class MobaGeneratedInputCommandHandlerManifest
            {
                static partial void AddGenerated(MobaInputCommandHandlerRegistry registry, ref int count);
            }
        }
        """;
}
