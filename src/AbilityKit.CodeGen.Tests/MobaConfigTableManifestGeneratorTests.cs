using AbilityKit.Demo.Moba.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaConfigTableManifestGeneratorTests
{
    [Fact]
    public void Generate_EmitsStronglyTypedConfigTableSpecs()
    {
        var compilation = RoslynTestCompilation.Create(SourceWithDeclarations("""
            [assembly: AbilityKit.Demo.Moba.Config.Core.MobaConfigTable(
                "characters", typeof(Game.CharacterDTO), typeof(Game.CharacterMO), "LegacyJson", 10)]
            """));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaConfigTableManifestGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        Assert.Contains(
            "new MobaConfigTableSpec(\"characters\", typeof(global::Game.CharacterDTO), typeof(global::Game.CharacterMO), \"LegacyJson\", 10, CreateDtoTable0, CreateEntryTable0, CollectChangedIds0)",
            generated,
            StringComparison.Ordinal);
        Assert.Contains("dto => dto.Id", generated, StringComparison.Ordinal);
        Assert.Contains("dto => new global::Game.CharacterMO(dto)", generated, StringComparison.Ordinal);
        Assert.Contains("ConfigTableFactory.CollectChangedIds<global::Game.CharacterDTO>", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateTables", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_OrdersSpecsAndFiltersDuplicatePaths()
    {
        var compilation = RoslynTestCompilation.Create(SourceWithDeclarations("""
            [assembly: AbilityKit.Demo.Moba.Config.Core.MobaConfigTable(
                "later", typeof(Game.LaterDTO), typeof(Game.LaterMO), "LegacyJson", 20)]
            [assembly: AbilityKit.Demo.Moba.Config.Core.MobaConfigTable(
                "first", typeof(Game.CharacterDTO), typeof(Game.CharacterMO), "LegacyJson", 10)]
            [assembly: AbilityKit.Demo.Moba.Config.Core.MobaConfigTable(
                "first", typeof(Game.DuplicateDTO), typeof(Game.DuplicateMO), "LegacyJson", 30)]
            """));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MobaConfigTableManifestGenerator());
        driver = driver.RunGenerators(compilation);

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).GetText().ToString();
        var first = generated.IndexOf("global::Game.CharacterDTO", StringComparison.Ordinal);
        var later = generated.IndexOf("global::Game.LaterDTO", StringComparison.Ordinal);
        Assert.True(first >= 0);
        Assert.True(later > first);
        Assert.DoesNotContain("global::Game.DuplicateDTO", generated, StringComparison.Ordinal);
        Assert.DoesNotContain(
            driver.GetRunResult().Diagnostics,
            item => item.Id.StartsWith("AKSG100", StringComparison.Ordinal));
    }

    internal static string SourceWithDeclarations(string declarations)
    {
        return $$"""
            using System;
            using System.Collections.Generic;

            {{declarations}}

            namespace AbilityKit.Demo.Moba.Config.Core
            {
                [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
                internal sealed class MobaConfigTableAttribute : Attribute
                {
                    public MobaConfigTableAttribute(
                        string filePath,
                        Type dtoType,
                        Type moType,
                        string groupName,
                        int order) { }
                }

                internal readonly struct MobaConfigTableSpec
                {
                    public MobaConfigTableSpec(
                        string filePath,
                        Type dtoType,
                        Type moType,
                        string groupName,
                        int order,
                        Func<Array, object> dtoTableFactory,
                        Func<Array, object> entryTableFactory,
                        Action<Array, ISet<int>> changedIdCollector) { }
                }

                internal static partial class MobaGeneratedConfigTableManifest
                {
                    static partial void AddGenerated(List<MobaConfigTableSpec> specs, ref int count);
                }
            }

            namespace AbilityKit.Ability.Config
            {
                public static class ConfigTableFactory
                {
                    public static object CreateDtoTable<TDto>(Array source, Func<TDto, int> keySelector)
                        where TDto : class => new object();

                    public static object CreateEntryTable<TDto, TEntry>(
                        Array source,
                        Func<TDto, int> keySelector,
                        Func<TDto, TEntry> entryFactory)
                        where TDto : class
                        where TEntry : class => new object();

                    public static void CollectChangedIds<TDto>(
                        Array source,
                        ISet<int> changedIds,
                        Func<TDto, int> keySelector)
                        where TDto : class { }
                }
            }

            namespace Game
            {
                public sealed class CharacterDTO { public int Id; }
                public sealed class CharacterMO { public CharacterMO(CharacterDTO dto) { } }
                public sealed class LaterDTO { public int Id; }
                public sealed class LaterMO { public LaterMO(LaterDTO dto) { } }
                public sealed class DuplicateDTO { public int Id; }
                public sealed class DuplicateMO { public DuplicateMO(DuplicateDTO dto) { } }
            }
            """;
    }
}
