using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaPlanActionManifestGeneratorTests
{
    [Fact]
    public void Generate_OrdersValidModulesByOrderThenFullyQualifiedName()
    {
        const string source = ContractSource + """
            namespace Game.Actions
            {
                using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;

                public struct Args { }

                [PlanActionModule(20)]
                public sealed class Last : MobaPlanActionModuleBase<Args, Last> { }

                [PlanActionModule(10)]
                public sealed class Zebra : MobaPlanActionModuleBase<Args, Zebra> { }

                [PlanActionModule(10)]
                public sealed class Alpha : MobaPlanActionModuleBase<Args, Alpha> { }

                [PlanActionModule(5)]
                public abstract class Invalid : MobaPlanActionModuleBase<Args, Invalid> { }
            }
            """;

        var compilation = RoslynTestCompilation.Create(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaPlanActionManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        var alpha = generated.IndexOf("Add<global::Game.Actions.Alpha>(descriptors, 10);", StringComparison.Ordinal);
        var zebra = generated.IndexOf("Add<global::Game.Actions.Zebra>(descriptors, 10);", StringComparison.Ordinal);
        var last = generated.IndexOf("Add<global::Game.Actions.Last>(descriptors, 20);", StringComparison.Ordinal);

        Assert.True(alpha >= 0);
        Assert.True(zebra > alpha);
        Assert.True(last > zebra);
        Assert.DoesNotContain("Game.Actions.Invalid", generated, StringComparison.Ordinal);
    }

    private const string ContractSource = """
        using System;
        using System.Collections.Generic;

        namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
        {
            [AttributeUsage(AttributeTargets.Class)]
            public sealed class PlanActionModuleAttribute : Attribute
            {
                public PlanActionModuleAttribute(int order = 0) { }
            }

            public interface IPlanActionModule { }
            public interface IMobaPlanActionMetadata { string ActionName { get; } }

            public abstract class MobaPlanActionModuleBase<TArgs, TModule> : IPlanActionModule, IMobaPlanActionMetadata
            {
                public string ActionName => typeof(TModule).Name;
            }

            public readonly struct MobaPlanActionDescriptor { }

            internal static partial class MobaGeneratedPlanActionManifest
            {
                static partial void AddGenerated(List<MobaPlanActionDescriptor> descriptors);
                private static void Add<TModule>(List<MobaPlanActionDescriptor> descriptors, int order)
                    where TModule : IPlanActionModule, IMobaPlanActionMetadata, new() { }
            }
        }
        """;
}

internal static class RoslynTestCompilation
{
    public static CSharpCompilation Create(string source)
    {
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));

        return CSharpCompilation.Create(
            assemblyName: "GeneratorTests",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)) },
            references: trustedAssemblies,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
