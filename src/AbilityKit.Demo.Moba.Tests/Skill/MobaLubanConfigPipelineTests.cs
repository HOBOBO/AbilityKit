using System;
using System.Linq;
using System.IO;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Config.BattleDemo;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Console.Bootstrap;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs.Tagging;
using AbilityKit.GameplayTags;
using AbilityKit.Triggering.Runtime.Plan.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaLubanConfigPipelineTests
{
    private static readonly int[] HeroIds = { 1001, 1002, 1003, 1004, 1005, 1006, 1007 };

    [Fact]
    public void Published_catalog_has_no_numeric_or_per_skill_tags()
    {
        var assets = new ConsoleTextAssetLoader();
        Assert.True(assets.TryLoadBytes("luban/moba_bytes/tag_definitions.bytes", out var tagBytes));
        var definitions = new moba_luban.TagDefinitions(Luban.ByteBuf.Wrap(tagBytes)).DataList;
        Assert.DoesNotContain(definitions, row => row.Name.All(char.IsDigit));
        Assert.DoesNotContain(definitions, row => row.Name.StartsWith("Skill.Hero", StringComparison.Ordinal));
        Assert.Contains(definitions, row => row.Id == 1504 && row.Name == "Debuff.Control.Airborne" &&
            row.Kind == moba_luban.MobaTagKind.Fact && row.Status == moba_luban.MobaTagStatus.Active);

        Assert.True(assets.TryLoadBytes("luban/moba_bytes/skills.bytes", out var skillBytes));
        var skills = new moba_luban.Skills(Luban.ByteBuf.Wrap(skillBytes)).DataList;
        Assert.All(skills, row => Assert.Empty(row.Tags));
    }

    [Fact]
    public void Published_tag_presentations_cover_super_armor_and_airborne_head_text()
    {
        var assets = new ConsoleTextAssetLoader();
        Assert.True(assets.TryLoadBytes("luban/moba_bytes/tag_hud_presentations.bytes", out var bytes));
        var rows = new moba_luban.TagHudPresentations(Luban.ByteBuf.Wrap(bytes)).DataList;

        Assert.Contains(rows, row => row.DefinitionId == 1002 && row.Visible &&
            (row.ChannelMask & 2) != 0 && row.HeadText == "霸体" && row.HeadTextDurationMs > 0);
        Assert.Contains(rows, row => row.DefinitionId == 1504 && row.Visible &&
            (row.ChannelMask & 2) != 0 && row.HeadText == "击飞" && row.HeadTextDurationMs > 0);
    }

    [Theory]
    [InlineData("Tags", "DispelBlockedByTags")]
    [InlineData("TagNames", "DispelBlockedByTagNames")]
    public void Buff_parser_preserves_authored_and_exported_tag_field_names(string tags, string blocked)
    {
        var row = new JObject
        {
            ["Id"] = 1,
            [tags] = new JArray("Debuff.Slow"),
            [blocked] = new JArray("State.SuperArmor"),
        };
        var parsed = (BuffDTO[])JsonNetMobaConfigDtoDeserializer.Instance.DeserializeDtoArray(
            new JArray(row).ToString(), typeof(BuffDTO));
        Assert.Equal(new[] { "Debuff.Slow" }, parsed[0].TagNames);
        Assert.Equal(new[] { "State.SuperArmor" }, parsed[0].DispelBlockedByTagNames);
    }

    [Fact]
    public void Attribute_types_are_row_definitions_matching_runtime_ids()
    {
        var assets = new ConsoleTextAssetLoader();
        Assert.True(assets.TryLoadBytes("luban/moba_bytes/attr_types.bytes", out var bytes));
        var definitions = new moba_luban.AttrTypes(Luban.ByteBuf.Wrap(bytes)).DataList;
        Assert.Equal(23, definitions.Count);
        foreach (var definition in definitions)
        {
            Assert.True(Enum.IsDefined(typeof(AbilityKit.Demo.Moba.BattleAttributeType), definition.Id));
            Assert.Equal(((AbilityKit.Demo.Moba.BattleAttributeType)definition.Id).ToString(), definition.Key);
        }

        var database = new MobaConfigDatabase(textAssetLoader: assets);
        database.LoadFromGroups(MobaLubanConfigGroups.Create(assets, new[] { "attr_types" }));
        Assert.Equal("MOVE_SPEED", database.GetAttrType(17).Key);
        Assert.Equal(0, database.GetAttrType(17).ValueKind);
        Assert.Equal(1, database.GetAttrType(12).ValueKind);
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
        Assert.Equal(17, database.GetDto<BuffDTO>(10020001).Modifiers[0].TargetId);
    }

    [Fact]
    public void Luban_binary_and_skill_pipeline_JSON_load_together()
    {
        var assets = new ConsoleTextAssetLoader();
        var database = new MobaConfigDatabase(textAssetLoader: assets);
        var names = MobaRuntimeConfigTableRegistry.Tables.Select(table => table.FilePath).ToArray();
        database.LoadFromGroups(MobaLubanConfigGroups.Create(assets, names));

        AssertHeroes(database);
        Assert.NotNull(database.GetSkill(10020101));
        Assert.Equal((ulong)MobaTagResultFlags.CannotCast, database.GetSkill(10020101).ExecutionBlockedResultMask);
        Assert.Equal((ulong)MobaTagResultFlags.CannotAttack, database.GetSkill(10020001).ExecutionBlockedResultMask);
        Assert.NotNull(database.GetSkillFlow(10020101));
        Assert.NotNull(database.GetSkillFlow(10010301));
    }

    [Fact]
    public void Gameplay_catalog_carries_the_authoritative_map_binding()
    {
        var assets = new ConsoleTextAssetLoader();
        Assert.True(assets.TryLoadBytes("luban/moba_bytes/gameplays.bytes", out var bytes));
        var rows = new moba_luban.Gameplays(Luban.ByteBuf.Wrap(bytes)).DataList;
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal(1, row.MapId));

        var database = new MobaConfigDatabase(textAssetLoader: assets);
        database.LoadFromGroups(MobaLubanConfigGroups.Create(assets, new[] { "gameplays" }));
        Assert.True(database.TryGetGameplay(1, out var defaultGameplay));
        Assert.True(database.TryGetGameplay(2, out var timedGameplay));
        Assert.Equal(1, defaultGameplay.MapId);
        Assert.Equal(1, timedGameplay.MapId);
    }

    [Fact]
    public void Binary_effect_references_resolve_modifiers_and_tags()
    {
        var assets = new ConsoleTextAssetLoader();
        Assert.True(assets.TryLoadBytes("luban/moba_bytes/buffs.bytes", out var buffBytes));
        var rawBuff = new moba_luban.Buffs(Luban.ByteBuf.Wrap(buffBytes)).Get(101);
        Assert.Equal(new[] { 400001, 400002 }, rawBuff.ModifierIds);

        var database = new MobaConfigDatabase(textAssetLoader: assets);
        database.LoadFromGroups(MobaLubanConfigGroups.Create(assets,
            MobaRuntimeConfigTableRegistry.Tables.Select(table => table.FilePath).ToArray()));

        var buff = database.GetDto<BuffDTO>(101);
        Assert.Equal(2, buff.Modifiers.Length);
        Assert.Equal(new[] { 30002, 30004 }, buff.Modifiers.Select(modifier => modifier.TargetId));
        Assert.Equal(17, database.GetDto<BuffDTO>(10020001).Modifiers[0].TargetId);
        var process = database.GetDto<ContinuousProcessDTO>(10010003);
        Assert.Equal(3, process.Modifiers.Length);
        Assert.All(process.Modifiers, modifier => Assert.Equal("resource.rage.ratio", modifier.MagnitudeContextKey));
        Assert.Contains("State.SuperArmor",
            database.GetDto<ContinuousTagTemplateDTO>(10010001).ApplicationTagNames);
        var slowTemplate = database.GetDto<ContinuousTagTemplateDTO>(3013);
        Assert.Contains("Debuff.Slow", slowTemplate.ApplicationTagNames);
        Assert.Equal(3013, database.GetDto<BuffDTO>(10060101).ContinuousTagTemplateId);
        Assert.Equal(3013, database.GetDto<BuffDTO>(10060211).ContinuousTagTemplateId);
        Assert.Empty(database.GetDto<BuffDTO>(10060101).TagNames);
        Assert.Empty(database.GetDto<BuffDTO>(10060211).TagNames);
    }

    [Fact]
    public void Published_modifier_showcase_and_summon_inheritance_load_from_binary()
    {
        var assets = new ConsoleTextAssetLoader();
        Assert.True(assets.TryLoadBytes("luban/moba_bytes/buffs.bytes", out var buffBytes));
        var rawBuff = new moba_luban.Buffs(Luban.ByteBuf.Wrap(buffBytes)).Get(102);
        Assert.Equal(new[] { 100023, 100024, 200001, 500001, 500002, 600001, 700001, 800001 },
            rawBuff.ModifierIds);

        var database = new MobaConfigDatabase(textAssetLoader: assets);
        database.LoadFromGroups(MobaLubanConfigGroups.Create(assets,
            MobaRuntimeConfigTableRegistry.Tables.Select(table => table.FilePath).ToArray()));
        var buff = database.GetDto<BuffDTO>(102);
        Assert.Equal(8, buff.Modifiers.Length);
        var speed = Assert.Single(buff.Modifiers.Where(row => row.TargetId == 40002));
        Assert.Equal(10020101, speed.SelectorBaseSkillId);
        Assert.Equal(30020101, speed.SelectorProjectileTemplateId);
        var movement = Assert.Single(buff.Modifiers.Where(row => row.TargetId == 17));
        Assert.Equal(new[] { 1f, 0.5f, 0.1f }, movement.MagnitudeCurve);
        Assert.Equal(3f, movement.MagnitudeDuration);
        var formula = Assert.Single(buff.Modifiers.Where(row => row.TargetId == 4));
        Assert.Equal(5f, formula.FormulaConstant);
        Assert.Equal(2f, formula.FormulaLevelCoefficient);

        var summon = database.GetDto<SummonDTO>(1);
        Assert.Equal(1, summon.InheritAttributeConfigId);
        var inherit = database.GetDto<SummonAttrInheritDTO>(1);
        var scale = Assert.Single(inherit.Scales);
        Assert.Equal(4, scale.AttrId);
        Assert.Equal(0.25f, scale.Ratio);
        Assert.Equal(5f, scale.Add);
    }

    [Fact]
    public void Authored_tag_conditions_control_continuous_activation()
    {
        var assets = new ConsoleTextAssetLoader();
        var database = new MobaConfigDatabase(textAssetLoader: assets);
        database.LoadFromGroups(MobaLubanConfigGroups.Create(assets,
            MobaRuntimeConfigTableRegistry.Tables.Select(table => table.FilePath).ToArray()));

        var registry = new MobaContinuousTagTemplateRegistry(database);
        Assert.True(registry.TryGet(10000001, out var control));
        var empty = new GameplayTagContainer();
        Assert.True(control.CanActivate(empty));
        Assert.True(control.IsOngoingSatisfied(empty));

        Assert.True(MobaGameplayTagCatalog.TryGet("State.SuperArmor", out var armor));
        var protectedTarget = new GameplayTagContainer(armor);
        Assert.False(control.CanActivate(protectedTarget));
        Assert.False(control.IsOngoingSatisfied(protectedTarget));
        Assert.True(MobaGameplayTagCatalog.TryGet("State.ControlImmune", out var immune));
        var immuneTarget = new GameplayTagContainer(immune);
        Assert.False(control.CanActivate(immuneTarget));
        Assert.False(control.IsOngoingSatisfied(immuneTarget));

        Assert.True(registry.TryGet(10010001, out var caster));
        Assert.True(caster.ApplicationTags.HasTagExact(armor));
        caster.ApplicationTags.Remove(armor);
        Assert.True(registry.TryGet(10010001, out var secondCaster));
        Assert.True(secondCaster.ApplicationTags.HasTagExact(armor));

        Assert.True(registry.TryGet(3005, out var stun));
        Assert.True(stun.CanActivate(empty));
        Assert.False(stun.CanActivate(protectedTarget));
        Assert.False(stun.CanActivate(immuneTarget));
        Assert.True(MobaGameplayTagCatalog.TryGet("State.Stunned", out var stunned));
        Assert.True(stun.ApplicationTags.HasTagExact(stunned));
        for (var templateId = 3005; templateId <= 3011; templateId++)
        {
            Assert.True(registry.TryGet(templateId, out var ordinaryControl));
            Assert.True(ordinaryControl.CanActivate(empty));
            Assert.False(ordinaryControl.CanActivate(protectedTarget));
            Assert.False(ordinaryControl.CanActivate(immuneTarget));
            Assert.True(ordinaryControl.IsOngoingSatisfied(protectedTarget));
            Assert.False(ordinaryControl.ShouldRemove(protectedTarget));
        }

        Assert.True(registry.TryGet(3013, out var slow));
        Assert.True(MobaGameplayTagCatalog.TryGet("Debuff.Slow", out var slowed));
        Assert.True(slow.ApplicationTags.HasTagExact(slowed));
        Assert.True(registry.TryGet(3015, out var movement));
        Assert.Equal((ulong)MobaTagResultFlags.CannotMove, movement.ExecutionBlockedResultMask);
    }

    [Fact]
    public void Buff_with_missing_tag_template_is_rejected_instead_of_losing_its_rules()
    {
        var buff = new BuffMO(new BuffDTO { Id = 77, ContinuousTagTemplateId = 999999 });
        var error = Assert.Throws<InvalidOperationException>(() =>
            BuffTagLifecycle.ResolveRequirements(buff, new MobaContinuousTagTemplateRegistry(null)));
        Assert.Contains("999999", error.Message);
    }

    [Fact]
    public void Tag_condition_compiler_rejects_unknown_tags()
    {
        var condition = new TagConditionDTO { Operator = "Tag", Tag = "State.Unknown" };
        Assert.Throws<InvalidOperationException>(() => MobaTagConditionCompiler.Compile(condition));
    }

    [Fact]
    public void Shared_modifiers_and_hero_loadouts_resolve_from_binary_definitions()
    {
        var assets = new ConsoleTextAssetLoader();
        Assert.True(assets.TryLoadBytes("luban/moba_bytes/buffs.bytes", out var buffBytes));
        var rawBuffs = new moba_luban.Buffs(Luban.ByteBuf.Wrap(buffBytes));
        Assert.Contains(100010, rawBuffs.Get(10030102).ModifierIds);
        Assert.Contains(100010, rawBuffs.Get(10060211).ModifierIds);

        Assert.True(assets.TryLoadBytes("luban/moba_bytes/attribute_templates.bytes", out var attributeBytes));
        var rawAttribute = new moba_luban.AttributeTemplates(Luban.ByteBuf.Wrap(attributeBytes)).Get(1001);
        Assert.Equal(1001, rawAttribute.Id);
        Assert.Null(typeof(moba_luban.DRAttributeTemplates).GetField("ActiveSkills"));

        var database = new MobaConfigDatabase(textAssetLoader: assets);
        database.LoadFromGroups(MobaLubanConfigGroups.Create(assets, new[] { "attribute_templates", "buffs" }));
        var hero = database.GetDto<CharacterDTO>(1001);
        var attributes = database.GetDto<BattleAttributeTemplateDTO>(hero.AttributeTemplateId);
        Assert.Equal(hero.SkillIds, attributes.ActiveSkills);
        Assert.Equal(hero.PassiveSkillIds, attributes.PassiveSkills);
        Assert.Equal(database.GetDto<BuffDTO>(10030102).Modifiers[0].Value,
            database.GetDto<BuffDTO>(10060211).Modifiers[0].Value);
    }

    [Fact]
    public void Console_trigger_module_resolves_editor_aggregate()
    {
        var builder = new WorldContainerBuilder();
        new ConsoleConfigModule().Configure(builder);
        builder.RegisterInstance(new MobaPresentationCueSnapshotService(new MobaLogicWorldRunGateService()));
        var services = builder.Build();
        using var scope = services.CreateScope();
        var triggers = scope.Resolve<TriggerPlanJsonDatabase>();
        // The editor aggregate includes the authored hero trigger plans in
        // addition to the two published rule plans.
        Assert.Equal(135, triggers.Records.Count);
        Assert.True(triggers.TryGetRecordByTriggerId(10070011, out _));
        Assert.True(triggers.TryGetRecordByTriggerId(10010301, out _));
    }

    [Fact]
    public void Editor_trigger_publication_is_identical_and_loadable_for_both_runtimes()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Unity", "Packages")))
            root = root.Parent;
        Assert.NotNull(root);

        var unityPath = Path.Combine(root!.FullName, "Unity", "Packages",
            "com.abilitykit.demo.moba.view.runtime", "Resources", "ability", "ability_trigger_plans.json");
        var consolePath = Path.Combine(root.FullName, "src", "AbilityKit.Demo.Moba.Console",
            "Configs", "luban", "ability", "ability_trigger_plans.json");
        var unity = JObject.Parse(File.ReadAllText(unityPath));
        var console = JObject.Parse(File.ReadAllText(consolePath));
        Assert.True(JToken.DeepEquals(unity, console));

        var triggers = (JArray)unity["Triggers"]!;
        Assert.Equal(133, triggers.Count);
        Assert.Contains(triggers, row => row.Value<int>("TriggerId") == 10070011);
        foreach (var id in new[] { 10010301, 10010311, 10010321 })
        {
            var trigger = triggers.Single(row => row.Value<int>("TriggerId") == id);
            Assert.Contains(trigger["Actions"]!.Children(), action =>
                action.Value<int>("ActionId") == 1872489131 && action["Args"]?["landing_trigger_ids"] != null);
        }

        var database = new TriggerPlanJsonDatabase();
        database.LoadFromJson(unity.ToString(), "moba-editor-trigger-aggregate");
        Assert.Equal(133, database.Records.Count);
        Assert.True(database.TryGetRecordByTriggerId(10010301, out _));
    }

    [Fact]
    public void Mixed_pipeline_DTOs_match_published_Luban_JSON()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Unity", "Packages")))
            root = root.Parent;
        Assert.NotNull(root);
        var assets = new ConsoleTextAssetLoader(Path.Combine(root!.FullName,
            "Unity", "Packages", "com.abilitykit.demo.moba.view.runtime", "Resources"));
        var json = new MobaConfigDatabase(textAssetLoader: assets);
        json.LoadFromGroups(MobaLubanConfigGroups.Create(assets, Array.Empty<string>()));
        var binary = new MobaConfigDatabase(textAssetLoader: assets);
        binary.LoadFromGroups(MobaLubanConfigGroups.Create(assets,
            MobaRuntimeConfigTableRegistry.Tables.Select(table => table.FilePath).ToArray()));

        var getDto = typeof(MobaConfigDatabase).GetMethod(nameof(MobaConfigDatabase.GetDto))!;
        foreach (var table in MobaRuntimeConfigTableRegistry.Tables)
        {
            var directory = table.FilePath == "skill_flows" ? "moba" : "luban/moba";
            Assert.True(assets.TryLoadText(directory + "/" + table.FilePath + ".json", out var text));
            foreach (var row in JArray.Parse(text))
            {
                var id = row.Value<int>("Id");
                var method = getDto.MakeGenericMethod(table.DtoType);
                var expected = JToken.FromObject(method.Invoke(json, new object[] { id })!);
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
            if (path == "moba/brains")
                throw new InvalidOperationException($"Promoted table fell back to JSON: {path}");
            return _inner.TryLoadText(path, out text);
        }

        public bool TryLoadBytes(string path, out byte[] bytes) => _inner.TryLoadBytes(path, out bytes);
    }
}
