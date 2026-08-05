using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaPayloadFieldIdsGeneratorTests
{
    [Fact]
    public void Generate_CreatesCurrentLegacyIdsAndSupportsMethod()
    {
        const string source = """
            using System;

            namespace AbilityKit.Demo.Moba
            {
                [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
                public sealed class GeneratePayloadFieldIdsAttribute : Attribute
                {
                    public GeneratePayloadFieldIdsAttribute(Type catalog, string method, bool legacy, params string[] fields) { }
                }
            }

            namespace Game
            {
                using AbilityKit.Demo.Moba;

                public static class Fields
                {
                    public const string ActorId = "actor_id";
                    public const string Value = "value";
                    public static int FieldId(string value) => value.Length;
                    public static int LegacyFieldId(string value) => -value.Length;
                }

                [GeneratePayloadFieldIds(typeof(Fields), "SupportsField", true, nameof(Fields.ActorId), nameof(Fields.Value))]
                public sealed partial class Accessor { }
            }
            """;

        var compilation = RoslynTestCompilation.Create(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaPayloadFieldIdsGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        Assert.Contains("ActorIdLegacyId", generated, StringComparison.Ordinal);
        Assert.Contains("public static bool SupportsField(int fieldId)", generated, StringComparison.Ordinal);
        Assert.Contains("fieldId == ValueId || fieldId == ValueLegacyId", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_FiltersInvalidDeclarationWithoutReportingAnalyzerDiagnostic()
    {
        var compilation = RoslynTestCompilation.Create(ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba;

                [GeneratePayloadFieldIds(typeof(Fields), "SupportsField", false, nameof(Fields.Value))]
                public sealed class InvalidAccessor { }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaPayloadFieldIdsGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.DoesNotContain(diagnostics, item => item.Id == MobaDiagnosticIds.InvalidPayloadFieldIdsDeclarationRuleId);
        Assert.Empty(driver.GetRunResult().GeneratedTrees);
    }

    [Fact]
    public void Generate_SupportsStaticPartialAccessor()
    {
        var compilation = RoslynTestCompilation.Create(ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba;

                [GeneratePayloadFieldIds(typeof(Fields), "SupportsField", false, nameof(Fields.Value))]
                public static partial class StaticAccessor { }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaPayloadFieldIdsGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));
        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        Assert.Contains("public static partial class StaticAccessor", generated, StringComparison.Ordinal);
    }

    private const string ContractSource = """
        using System;

        namespace AbilityKit.Demo.Moba
        {
            [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
            public sealed class GeneratePayloadFieldIdsAttribute : Attribute
            {
                public GeneratePayloadFieldIdsAttribute(Type catalog, string method, bool legacy, params string[] fields) { }
            }
        }

        namespace Game
        {
            public static class Fields
            {
                public const string Value = "value";
                public static int FieldId(string value) => value.Length;
            }
        }
        """;
}
