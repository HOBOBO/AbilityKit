using System.Collections.Immutable;
using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaBTreeNodeAnalyzerTests
{
    [Fact]
    public async Task Analyze_ReportsGenericAndInaccessibleNodes()
    {
        const string source = ContractSource + """
            namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
            {
                internal sealed class GenericNode<T> : BTCore.Runtime.BTNode { }

                internal static class Owner
                {
                    private sealed class HiddenNode : BTCore.Runtime.BTNode { }
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Equal(2, diagnostics.Count(item => item.Id == MobaDiagnosticIds.InvalidBTreeNodeRuleId));
    }

    [Fact]
    public async Task Analyze_ReportsDuplicateShortNameOnce()
    {
        const string source = ContractSource + """
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
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Single(diagnostics, item => item.Id == MobaDiagnosticIds.DuplicateBTreeNodeNameRuleId);
    }

    [Fact]
    public async Task Analyze_IgnoresAbstractAndOutOfScopeNodes()
    {
        const string source = ContractSource + """
            namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
            {
                internal abstract class AbstractNode : BTCore.Runtime.BTNode { }
            }

            namespace External.Behavior
            {
                internal sealed class ExternalNode : BTCore.Runtime.BTNode { }
            }
            """;

        Assert.Empty(await GetDiagnosticsAsync(source));
    }

    [Fact]
    public async Task GeneratorAndAnalyzer_ReportDuplicateOnlyOnce()
    {
        const string source = ContractSource + """
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
            """;
        var compilation = RoslynTestCompilation.Create(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaBTreeNodeManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
        var analyzerDiagnostics = await outputCompilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MobaBTreeNodeAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.DoesNotContain(
            generatorDiagnostics,
            item => item.Id.StartsWith("AKSG700", StringComparison.Ordinal));
        Assert.Single(
            analyzerDiagnostics,
            item => item.Id == MobaDiagnosticIds.DuplicateBTreeNodeNameRuleId);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = RoslynTestCompilation.Create(source);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new MobaBTreeNodeAnalyzer());
        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
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
