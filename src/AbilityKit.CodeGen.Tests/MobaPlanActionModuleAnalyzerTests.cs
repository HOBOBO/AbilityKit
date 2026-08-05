using System.Collections.Immutable;
using AbilityKit.Analyzer;
using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaPlanActionModuleAnalyzerTests
{
    [Fact]
    public async Task Analyze_ReportsInvalidModuleShapesAndMissingAttribute()
    {
        const string source = ContractSource + """
            namespace Game.Actions
            {
                using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;

                public struct Args { }
                public sealed class Other : MobaPlanActionModuleBase<Args, Other> { }

                [PlanActionModule]
                public sealed class NotAModule { }

                [PlanActionModule]
                public abstract class AbstractModule : MobaPlanActionModuleBase<Args, AbstractModule> { }

                [PlanActionModule]
                public sealed class WrongSelf : MobaPlanActionModuleBase<Args, Other> { }

                [PlanActionModule]
                public sealed class NoDefaultConstructor : MobaPlanActionModuleBase<Args, NoDefaultConstructor>
                {
                    public NoDefaultConstructor(int value) { }
                }

                public sealed class MissingAttribute : MobaPlanActionModuleBase<Args, MissingAttribute> { }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        var ids = diagnostics.Select(item => item.Id).ToArray();

        Assert.Contains(MobaDiagnosticIds.InvalidPlanActionModuleRuleId, ids);
        Assert.Contains(MobaDiagnosticIds.InvalidPlanActionModuleShapeRuleId, ids);
        Assert.Contains(MobaDiagnosticIds.InvalidPlanActionSelfTypeRuleId, ids);
        Assert.Contains(MobaDiagnosticIds.MissingPlanActionConstructorRuleId, ids);
        Assert.Contains(MobaDiagnosticIds.MissingPlanActionModuleAttributeRuleId, ids);
    }

    [Fact]
    public async Task Analyze_ReportsDuplicateConstantActionNames()
    {
        const string source = ContractSource + """
            namespace Game.Actions
            {
                using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;

                public sealed class FirstSchema : MobaPlanActionSchemaBase<int>
                {
                    protected override string ActionName => "duplicate";
                }

                public sealed class SecondSchema : MobaPlanActionSchemaBase<float>
                {
                    protected override string ActionName => "duplicate";
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.Equal(2, diagnostics.Count(item => item.Id == MobaDiagnosticIds.InvalidPlanActionNameRuleId));
    }

    [Fact]
    public async Task ForbiddenNamespaceAnalyzer_DefaultConfigurationDoesNotInventUnityEngineConstraint()
    {
        const string source = "using UnityEngine; public sealed class RuntimeType { }";
        var compilation = RoslynTestCompilation.Create(source);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new ForbiddenNamespaceAnalyzer());

        var diagnostics = await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();

        Assert.DoesNotContain(diagnostics, item => item.Id == DiagnosticIds.ForbiddenNamespaceAnalyzerRuleId);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = RoslynTestCompilation.Create(source);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new MobaPlanActionModuleAnalyzer());
        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
    }

    private const string ContractSource = """
        using System;

        namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
        {
            [AttributeUsage(AttributeTargets.Class)]
            public sealed class PlanActionModuleAttribute : Attribute
            {
                public PlanActionModuleAttribute(int order = 0) { }
            }

            public abstract class MobaPlanActionModuleBase<TArgs, TModule> { }

            public abstract class MobaPlanActionSchemaBase<TArgs>
            {
                protected abstract string ActionName { get; }
            }
        }
        """;
}
