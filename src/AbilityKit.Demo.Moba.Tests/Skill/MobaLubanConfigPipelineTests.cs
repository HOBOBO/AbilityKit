using System;
using System.Linq;
using System.IO;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Config.BattleDemo;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Console.Bootstrap;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Services.Behavior;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaLubanConfigPipelineTests
{
    private static readonly int[] HeroIds = { 1001, 1002, 1003, 1004, 1005, 1006 };

    [Fact]
    public void Effects_load_from_promoted_Luban_binary()
    {
        var assets = new BinaryOnlyPromotedTableLoader();
        Assert.True(assets.TryLoadBytes("luban/moba_bytes/effects.bytes", out var bytes));
        Assert.Equal(60, new moba_luban.Effects(Luban.ByteBuf.Wrap(bytes)).DataList.Count);

        var database = new ConsoleMobaConfigDatabase(assets);
        database.LoadFromResources();
        Assert.Equal(60, database.EffectCount);
        Assert.True(database.TryGetEffect(10001, out var effect));
        Assert.Equal(200f, effect.BaseDamage);
    }

    [Fact]
    public void Brains_load_from_promoted_Luban_binary()
    {
        var assets = new BinaryOnlyPromotedTableLoader();
        Assert.True(assets.TryLoadBytes("luban/moba_bytes/brains.bytes", out var bytes));
        Assert.Equal(3, new moba_luban.Brains(Luban.ByteBuf.Wrap(bytes)).DataList.Count);

        using var catalog = new MobaActorBrainCatalog();
        Assert.Equal(3, MobaActorBrainCatalogJsonLoader.Load(assets, catalog));
        Assert.True(catalog.TryGet(100, out var brain));
        Assert.Equal("generic_hero_combat_bt", brain.DecisionName);
    }

    [Fact]
    public void Characters_can_move_to_Luban_binary_without_moving_other_tables()
    {
        var assets = new ConsoleTextAssetLoader();
        var database = new MobaConfigDatabase(textAssetLoader: assets);
        database.LoadFromGroups(MobaLubanConfigGroups.Create(assets, new[] { "characters" }));

        AssertHeroes(database);
        Assert.NotNull(database.GetSkill(10020101));
    }

    [Fact]
    public void All_registered_tables_can_load_from_Luban_binary()
    {
        var assets = new ConsoleTextAssetLoader();
        var database = new MobaConfigDatabase(textAssetLoader: assets);
        var names = MobaRuntimeConfigTableRegistry.Tables.Select(table => table.FilePath).ToArray();
        database.LoadFromGroups(MobaLubanConfigGroups.Create(assets, names));

        AssertHeroes(database);
        Assert.NotNull(database.GetSkill(10020101));
        Assert.NotNull(database.GetSkillFlow(10020101));
    }

    [Fact]
    public void Binary_DTOs_match_the_current_JSON_baseline()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Unity", "Packages")))
            root = root.Parent;
        Assert.NotNull(root);
        var assets = new ConsoleTextAssetLoader(Path.Combine(root!.FullName,
            "Unity", "Packages", "com.abilitykit.demo.moba.view.runtime", "Resources"));
        var legacy = new MobaConfigDatabase(textAssetLoader: assets);
        legacy.LoadFromResources("moba", strict: true);
        var binary = new MobaConfigDatabase(textAssetLoader: assets);
        binary.LoadFromGroups(MobaLubanConfigGroups.Create(assets,
            MobaRuntimeConfigTableRegistry.Tables.Select(table => table.FilePath).ToArray()));

        var getDto = typeof(MobaConfigDatabase).GetMethod(nameof(MobaConfigDatabase.GetDto))!;
        foreach (var table in MobaRuntimeConfigTableRegistry.Tables)
        {
            Assert.True(assets.TryLoadText("moba/" + table.FilePath + ".json", out var text));
            foreach (var row in JArray.Parse(text))
            {
                var id = row.Value<int>("Id");
                var method = getDto.MakeGenericMethod(table.DtoType);
                var expected = JToken.FromObject(method.Invoke(legacy, new object[] { id })!);
                var actual = JToken.FromObject(method.Invoke(binary, new object[] { id })!);
                Assert.True(JToken.DeepEquals(expected, actual),
                    $"{table.FilePath}/{id}: expected={expected}, actual={actual}");
            }
        }
    }

    private static void AssertHeroes(MobaConfigDatabase database)
    {
        foreach (var id in HeroIds)
        {
            var hero = database.GetCharacter(id);
            var dto = database.GetDto<CharacterDTO>(id);
            Assert.Equal(id, hero.Id);
            Assert.Equal(id, dto.Id);
            Assert.Equal(3, hero.SkillIds.Count);
            Assert.NotEmpty(hero.PassiveSkillIds);
            Assert.True(database.GetAttributeTemplate(hero.AttributeTemplateId).MaxHp > 0);
        }
    }

    private sealed class BinaryOnlyPromotedTableLoader : ITextAssetLoader
    {
        private readonly ConsoleTextAssetLoader _inner = new ConsoleTextAssetLoader();

        public bool TryLoadText(string path, out string text)
        {
            if (path == "moba/brains" || path.Replace('\\', '/') == "luban/moba/effects.json")
                throw new InvalidOperationException($"Promoted table fell back to JSON: {path}");
            return _inner.TryLoadText(path, out text);
        }

        public bool TryLoadBytes(string path, out byte[] bytes) => _inner.TryLoadBytes(path, out bytes);
    }
}
