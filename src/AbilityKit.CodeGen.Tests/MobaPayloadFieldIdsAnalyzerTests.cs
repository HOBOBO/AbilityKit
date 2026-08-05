using System.Collections.Immutable;
using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaPayloadFieldIdsAnalyzerTests
{
    [Fact]
    public async Task Analyze_ReportsNonPartialAccessor()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba;

                [GeneratePayloadFieldIds(typeof(Fields), "SupportsField", false, nameof(Fields.Value))]
                internal sealed class NonPartialAccessor { }
            }
            """;

        var diagnostic = Assert.Single(await GetDiagnosticsAsync(source));

        Assert.Equal(MobaDiagnosticIds.InvalidPayloadFieldIdsDeclarationRuleId, diagnostic.Id);
        Assert.Contains("must be partial", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyze_ReportsUnsupportedAccessorShapes()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba;

                [GeneratePayloadFieldIds(typeof(Fields), "SupportsGeneric", false, nameof(Fields.Value))]
                internal partial class GenericAccessor<T> { }

                internal static class Owner
                {
                    [GeneratePayloadFieldIds(typeof(Fields), "SupportsNested", false, nameof(Fields.Value))]
                    internal partial class NestedAccessor { }
                }

                [GeneratePayloadFieldIds(typeof(Fields), "SupportsRecord", false, nameof(Fields.Value))]
                internal partial record RecordAccessor;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Equal(3, diagnostics.Count(item => item.Id == MobaDiagnosticIds.InvalidPayloadFieldIdsDeclarationRuleId));
    }

    [Fact]
    public async Task Analyze_ReportsInvalidGroupContracts()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba;

                public sealed class WrongResolverFields
                {
                    public const string Value = "value";
                    public int FieldId(string value) => value.Length;
                }

                public static class CurrentOnlyFields
                {
                    public const string Value = "value";
                    public static int FieldId(string value) => value.Length;
                }

                [GeneratePayloadFieldIds(typeof(Fields), "bad-name", false, nameof(Fields.Value))]
                internal partial class InvalidMethodName { }

                [GeneratePayloadFieldIds(typeof(Fields), "SupportsEmpty", false)]
                internal partial class EmptyFields { }

                [GeneratePayloadFieldIds(typeof(Fields), "SupportsMissing", false, "Missing")]
                internal partial class MissingField { }

                [GeneratePayloadFieldIds(typeof(WrongResolverFields), "SupportsWrongResolver", false, nameof(WrongResolverFields.Value))]
                internal partial class WrongResolver { }

                [GeneratePayloadFieldIds(typeof(CurrentOnlyFields), "SupportsLegacy", true, nameof(CurrentOnlyFields.Value))]
                internal partial class MissingLegacyResolver { }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Equal(5, diagnostics.Count(item => item.Id == MobaDiagnosticIds.InvalidPayloadFieldIdsDeclarationRuleId));
    }

    [Fact]
    public async Task Analyze_ReportsCrossAttributeGenerationConflicts()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba;

                public static class OtherFields
                {
                    public const string Value = "other";
                    public static int FieldId(string value) => value.Length;
                }

                public static class CollisionFields
                {
                    public const string Foo = "foo";
                    public const string FooLegacy = "foo_legacy";
                    public static int FieldId(string value) => value.Length;
                    public static int LegacyFieldId(string value) => -value.Length;
                }

                [GeneratePayloadFieldIds(typeof(Fields), "SupportsDuplicate", false, nameof(Fields.Value))]
                [GeneratePayloadFieldIds(typeof(Fields), "SupportsDuplicate", false, nameof(Fields.Value))]
                internal partial class DuplicateMethodAccessor { }

                [GeneratePayloadFieldIds(typeof(Fields), "SupportsFirst", false, nameof(Fields.Value))]
                [GeneratePayloadFieldIds(typeof(OtherFields), "SupportsSecond", false, nameof(OtherFields.Value))]
                internal partial class CrossCatalogAccessor { }

                [GeneratePayloadFieldIds(
                    typeof(CollisionFields),
                    "SupportsCollision",
                    true,
                    nameof(CollisionFields.Foo),
                    nameof(CollisionFields.FooLegacy))]
                internal partial class GeneratedNameCollisionAccessor { }

                [GeneratePayloadFieldIds(typeof(Fields), "SupportsExisting", false, nameof(Fields.Value))]
                internal partial class ExistingMemberAccessor
                {
                    private static readonly int ValueId = 0;
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Equal(4, diagnostics.Count(item => item.Id == MobaDiagnosticIds.InvalidPayloadFieldIdsDeclarationRuleId));
    }

    [Fact]
    public async Task Analyze_AcceptsValidStaticAccessorAndRepeatedFieldReferences()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba;

                [GeneratePayloadFieldIds(typeof(Fields), "SupportsFirst", false, nameof(Fields.Value))]
                [GeneratePayloadFieldIds(typeof(Fields), "SupportsSecond", false, nameof(Fields.Value))]
                internal static partial class ValidAccessor { }
            }
            """;

        Assert.Empty(await GetDiagnosticsAsync(source));
    }

    [Fact]
    public async Task GeneratorAndAnalyzer_ReportInvalidDeclarationOnlyOnce()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba;

                [GeneratePayloadFieldIds(typeof(Fields), "SupportsField", false, nameof(Fields.Value))]
                internal sealed class InvalidAccessor { }
            }
            """;
        var compilation = RoslynTestCompilation.Create(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaPayloadFieldIdsGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
        var analyzerDiagnostics = await outputCompilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MobaPayloadFieldIdsAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.DoesNotContain(
            generatorDiagnostics,
            item => item.Id == MobaDiagnosticIds.InvalidPayloadFieldIdsDeclarationRuleId);
        Assert.Single(
            analyzerDiagnostics,
            item => item.Id == MobaDiagnosticIds.InvalidPayloadFieldIdsDeclarationRuleId);
    }

    [Fact]
    public async Task GeneratorAndAnalyzer_AcceptValidDeclarationWithoutGeneratedMemberConflicts()
    {
        const string source = ContractSource + """
            namespace Game
            {
                using AbilityKit.Demo.Moba;

                [GeneratePayloadFieldIds(typeof(Fields), "SupportsField", false, nameof(Fields.Value))]
                internal sealed partial class ValidAccessor { }
            }
            """;
        var compilation = RoslynTestCompilation.Create(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaPayloadFieldIdsGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
        var analyzerDiagnostics = await outputCompilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MobaPayloadFieldIdsAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.DoesNotContain(
            generatorDiagnostics,
            item => item.Id == MobaDiagnosticIds.InvalidPayloadFieldIdsDeclarationRuleId);
        Assert.DoesNotContain(
            analyzerDiagnostics,
            item => item.Id == MobaDiagnosticIds.InvalidPayloadFieldIdsDeclarationRuleId);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = RoslynTestCompilation.Create(source);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new MobaPayloadFieldIdsAnalyzer());
        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
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
