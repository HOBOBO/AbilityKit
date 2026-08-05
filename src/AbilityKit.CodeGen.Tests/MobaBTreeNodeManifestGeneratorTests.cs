using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaBTreeNodeManifestGeneratorTests
{
    [Fact]
    public void Generate_RegistersConcreteNodesByShortName()
    {
        var compilation = RoslynTestCompilation.Create(ContractSource + """
            namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
            {
                internal sealed class SelectTargetAction : BTCore.Runtime.BTNode { }
                internal abstract class AbstractAction : BTCore.Runtime.BTNode { }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaBTreeNodeManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        Assert.Contains(
            "nodeTypes.Add(\"SelectTargetAction\", typeof(global::AbilityKit.Demo.Moba.Services.Behavior.BTree.SelectTargetAction));",
            generated,
            StringComparison.Ordinal);
        Assert.DoesNotContain("AbstractAction", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_OmitsDuplicateShortNames()
    {
        var compilation = RoslynTestCompilation.Create(ContractSource + """
            namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
            {
                internal static class FirstOwner
                {
                    internal sealed class SharedAction : BTCore.Runtime.BTNode { }
                }

                internal static class SecondOwner
                {
                    internal sealed class SharedAction : BTCore.Runtime.BTNode { }
                }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaBTreeNodeManifestGenerator());
        driver = driver.RunGenerators(compilation);

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        Assert.DoesNotContain("SharedAction", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_DoesNotEmitIntoAssemblyWithoutManifestContract()
    {
        var compilation = RoslynTestCompilation.Create("""
            namespace BTCore.Runtime
            {
                internal abstract class BTNode { }
            }

            namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
            {
                internal sealed class ReferencingAssemblyNode : BTCore.Runtime.BTNode { }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaBTreeNodeManifestGenerator());
        driver = driver.RunGenerators(compilation);

        Assert.Empty(driver.GetRunResult().GeneratedTrees);
    }

    private const string ContractSource = """
        using System;
        using System.Collections.Generic;

        namespace BTCore.Runtime
        {
            internal abstract class BTNode { }
        }

        namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
        {
            internal static partial class MobaGeneratedBTreeNodeManifest
            {
                static partial void AddGenerated(Dictionary<string, Type> nodeTypes);
            }
        }
        """;
}
