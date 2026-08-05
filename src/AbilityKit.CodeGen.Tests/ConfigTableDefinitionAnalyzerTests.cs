using System.Collections.Immutable;
using AbilityKit.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class ConfigTableDefinitionAnalyzerTests
{
    [Theory]
    [InlineData("null", "values => values")]
    [InlineData("values => values", "null")]
    public async Task Analyze_ReportsPartialFactoryConfiguration(
        string dtoFactory,
        string entryFactory)
    {
        var diagnostics = await GetDiagnosticsAsync(CreateSource($$"""
            _ = new ConfigTableDefinition(
                "skills",
                typeof(object),
                typeof(object),
                "LegacyJson",
                {{dtoFactory}},
                {{entryFactory}});
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticIds.PartialConfigTableFactoryRuleId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Theory]
    [InlineData("null", "null")]
    [InlineData("values => values", "values => values")]
    public async Task Analyze_AcceptsCompleteFactoryConfiguration(
        string dtoFactory,
        string entryFactory)
    {
        var diagnostics = await GetDiagnosticsAsync(CreateSource($$"""
            _ = new ConfigTableDefinition(
                "skills",
                typeof(object),
                typeof(object),
                "LegacyJson",
                {{dtoFactory}},
                {{entryFactory}});
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_AcceptsLegacyConstructor()
    {
        var diagnostics = await GetDiagnosticsAsync(CreateSource("""
            _ = new ConfigTableDefinition(
                "skills",
                typeof(object),
                typeof(object));
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_LeavesVariableNullabilityToRuntimeValidation()
    {
        var diagnostics = await GetDiagnosticsAsync(CreateSource("""
            Func<Array, object> dtoFactory = values => values;
            Func<Array, object> entryFactory = values => values;
            _ = new ConfigTableDefinition(
                "skills",
                typeof(object),
                typeof(object),
                "LegacyJson",
                dtoFactory,
                entryFactory);
            """));

        Assert.Empty(diagnostics);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = RoslynTestCompilation.Create(source);
        return await compilation
            .WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new ConfigTableDefinitionAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
    }

    private static string CreateSource(string statements)
    {
        return $$"""
            using System;
            using AbilityKit.Ability.Config;

            {{ConfigTableDefinitionContract}}

            public static class Usage
            {
                public static void Configure()
                {
                    {{statements}}
                }
            }
            """;
    }

    private const string ConfigTableDefinitionContract = """
        namespace AbilityKit.Ability.Config
        {
            public sealed class ConfigTableDefinition
            {
                public ConfigTableDefinition(
                    string filePath,
                    Type dtoType,
                    Type entryType,
                    string groupName = null)
                {
                }

                public ConfigTableDefinition(
                    string filePath,
                    Type dtoType,
                    Type entryType,
                    string groupName,
                    Func<Array, object> dtoTableFactory,
                    Func<Array, object> entryTableFactory)
                {
                }
            }
        }
        """;
}
