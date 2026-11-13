using System;
using System.Linq;
using System.IO;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.World.DI;
using AbilityKit.Combat.Projectile;
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
    public void Published_model_animation_facts_match_binary_and_jinx_actions_use_shared_references()
    {
        var assets = new ConsoleTextAssetLoader();
        Assert.True(assets.TryLoadText("luban/moba/model_animations.json", out var json));
        Assert.True(assets.TryLoadBytes("luban/moba_bytes/model_animations.bytes", out var bytes));
        var rows = JArray.Parse(json).Cast<JObject>().ToDictionary(r => r.Value<int>("Id"));
        var binary = new moba_luban.ModelAnimations(Luban.ByteBuf.Wrap(bytes)).DataList;
        Assert.Equal(rows.Count, binary.Count);
        Assert.NotEmpty(binary);
        var keys = new HashSet<(int ModelId, string ActionKey)>();
        foreach (var row in binary)
        {
            Assert.True(keys.Add((row.ModelId, row.ActionKey)));
            var fact = rows[row.Id];
            Assert.Equal(fact.Value<int>("ModelId"), row.ModelId);
            Assert.Equal(fact.Value<string>("ActionKey"), row.ActionKey);
            Assert.Equal(fact.Value<string>("AnimatorState"), row.AnimatorState);
            Assert.Equal(fact.Value<int>("FrameCount"), row.FrameCount);
            Assert.Equal(fact.Value<double>("FrameRate"), row.FrameRate);
            Assert.Equal(fact.Value<int>("Layer"), row.Layer);
        }
        Assert.True(assets.TryLoadText("luban/moba/character_view_actions.json", out var actions));
        var count = 0;
        foreach (var variant in JObject.Parse(actions)["variants"]!.OfType<JObject>().Where(r => r.Value<int>("entityCode") == 3002))
            foreach (var node in ((JObject)variant["action"]!).DescendantsAndSelf().OfType<JObject>().Where(n => n.Value<string>("type") == "play"))
            {
                Assert.Contains((node.Value<int>("modelId"), node.Value<string>("animationKey")!), keys);
                Assert.All(new[] { "animatorState", "frameCount", "frameRate", "layer" }, field => Assert.Null(node[field]));
                count++;
            }
        Assert.Equal(22, count);
    }

    [Fact]
    public void Published_counter_callbacks_reference_global_callable_triggers()
    {
        var assets = new ConsoleTextAssetLoader();
        Assert.True(assets.TryLoadText("luban/ability/ability_trigger_plans.json", out var json));
        var triggers = (JArray)JObject.Parse(json)["Triggers"]!;
        var byId = triggers.ToDictionary(row => row.Value<int>("TriggerId"));
        var callbackCount = 0;
        foreach (var trigger in triggers.Cast<JObject>())
        {
            // Search both legacy action arrays and nested ExecutionRoot branches.
            foreach (var action in trigger.Descendants().OfType<JObject>())
            {
                if (action.Value<int?>("ActionId") is not (327267303 or 903018631)) continue;
                var callback = action["Args"]?["trigger_id"];
                if (callback == null || callback.Value<string>("Kind") != "Const") continue;
                var id = callback.Value<int>("ConstValue");
                if (id <= 0) continue;
                callbackCount++;
                var reference = $"Counter in trigger {trigger.Value<int>("TriggerId")} calls {id}";
                Assert.True(byId.TryGetValue(id, out var target), reference + ": target is missing.");
                Assert.True((target!.Value<int?>("Scope") ?? 0) == 0,
                    reference + ": callback must use Global scope.");
                Assert.True((target.Value<int?>("EventId") ?? 0) == 0 &&
                    string.IsNullOrEmpty(target.Value<string>("EventName")),
                    reference + ": callback must be Callable, not an event subscription.");
            }
        }
        Assert.True(callbackCount > 0, "Published production counters must be covered by this contract.");
    }

    [Fact]
    public void Presentation_lifecycle_fields_survive_parser_and_model_projection()
    {
        var row = new JObject
        {
            ["Id"] = 91, ["AssetId"] = 90013001, ["StackPolicy"] = 2, ["StopPolicy"] = 1,
            ["TargetLossPolicy"] = 3, ["CompletionPolicy"] = 2, ["StopGraceMs"] = 1500,
        };
        var parsed = (PresentationTemplateDTO[])JsonNetMobaConfigDtoDeserializer.Instance.DeserializeDtoArray(
            new JArray(row).ToString(), typeof(PresentationTemplateDTO));
        var model = new PresentationTemplateMO(parsed[0]);
        Assert.Equal(2, model.StackPolicy);
        Assert.Equal(1, model.StopPolicy);
        Assert.Equal(3, model.TargetLossPolicy);
        Assert.Equal(2, model.CompletionPolicy);
        Assert.Equal(1500, model.StopGraceMs);
        var legacy = new PresentationTemplateMO(new PresentationTemplateDTO());
        Assert.Equal(0, legacy.TargetLossPolicy);
        Assert.Equal(0, legacy.CompletionPolicy);
        Assert.Equal(0, legacy.StopGraceMs);
    }

    [Fact]
    public void Published_template_binary_contains_lifecycle_defaults()
    {
        var assets = new ConsoleTextAssetLoader();
        Assert.True(assets.TryLoadBytes("luban/moba_bytes/presentation_templates.bytes", out var bytes));
        var rows = new moba_luban.PresentationTemplates(Luban.ByteBuf.Wrap(bytes)).DataList;
        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
        {
            Assert.InRange(row.TargetLossPolicy, 0, 3);
            Assert.InRange(row.CompletionPolicy, 0, 2);
            Assert.True(row.StopGraceMs >= 0);
        });
    }

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
        var definitions = new moba_luban.Brains(Luban.ByteBuf.Wrap(bytes)).DataList;
        Assert.NotEmpty(definitions);

        using var catalog = new MobaActorBrainCatalog();
        Assert.Equal(definitions.Count, MobaActorBrainCatalogJsonLoader.Load(assets, catalog));
        foreach (var definition in definitions)
        {
            Assert.True(catalog.TryGet(definition.BrainId, out var loaded));
            Assert.Equal(definition.DecisionName, loaded.DecisionName);
        }
        Assert.True(catalog.TryGet(100, out var brain));
        Assert.Equal("generic_hero_combat_bt", brain.DecisionName);
        Assert.True(catalog.TryGet(101, out var heading));
        Assert.Equal("launch_heading_combat_bt", heading.DecisionName);
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
        Assert.Contains("State.Immunity.Slow", slowTemplate.ActivationBlockedTagNames);
        Assert.Empty(slowTemplate.OngoingBlockedTagNames);
        Assert.Equal(3013, database.GetDto<BuffDTO>(10060101).ContinuousTagTemplateId);
        Assert.Equal(3013, database.GetDto<BuffDTO>(10060211).ContinuousTagTemplateId);
        // XiaoQiao's self movement bonus is not a slow debuff.
        var movementBonus = database.GetDto<BuffDTO>(10020001);
        Assert.Equal(0, movementBonus.ContinuousTagTemplateId);
        Assert.True(Assert.Single(movementBonus.Modifiers).Value > 0);
        Assert.Equal(3013, database.GetDto<BuffDTO>(32010301).ContinuousTagTemplateId);
        Assert.Equal(3013, database.GetDto<BuffDTO>(32010403).ContinuousTagTemplateId);
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
        // Compare stable identities with published content, so adding authored
        // hero plans does not invalidate an unrelated loading contract.
        var assets = scope.Resolve<ITextAssetLoader>();
        Assert.True(assets.TryLoadText("luban/ability/ability_trigger_plans.json", out var aggregateJson));
        var expectedIds = JObject.Parse(aggregateJson)["Triggers"]!
            .Select(row => row.Value<int>("TriggerId")).ToList();
        foreach (var rulePath in new[]
        {
            "luban/ability/rules/commit/skill_commit_default.json",
            "luban/ability/rules/release/skill_release_default.json"
        })
        {
            Assert.True(assets.TryLoadText(rulePath, out var ruleJson));
            expectedIds.Add(JObject.Parse(ruleJson).Value<int>("id"));
        }
        Assert.Equal(expectedIds.Distinct().OrderBy(id => id),
            triggers.Records.Select(record => record.TriggerId).OrderBy(id => id));
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
        Assert.NotEmpty(triggers);
        var publishedIds = triggers.Select(row => row.Value<int>("TriggerId")).ToArray();
        Assert.Equal(publishedIds.Length, publishedIds.Distinct().Count());
        Assert.Contains(triggers, row => row.Value<int>("TriggerId") == 10070011);
        foreach (var id in new[] { 10010301, 10010311, 10010321 })
        {
            var trigger = triggers.Single(row => row.Value<int>("TriggerId") == id);
            Assert.Contains(trigger["Actions"]!.Children(), action =>
                action.Value<int>("ActionId") == 1872489131 && action["Args"]?["landing_trigger_ids"] != null);
        }

        var database = new TriggerPlanJsonDatabase();
        database.LoadFromJson(unity.ToString(), "moba-editor-trigger-aggregate");
        Assert.Equal(publishedIds.OrderBy(id => id),
            database.Records.Select(record => record.TriggerId).OrderBy(id => id));
        Assert.True(database.TryGetRecordByTriggerId(10010301, out _));
    }

    [Fact]
    public void Jhin_q_published_configuration_chain_is_resolvable_for_runtime_and_console()
    {
        var assets = new ConsoleTextAssetLoader();
        var database = new MobaConfigDatabase(textAssetLoader: assets);
        database.LoadFromGroups(MobaLubanConfigGroups.Create(assets,
            MobaRuntimeConfigTableRegistry.Tables.Select(table => table.FilePath).ToArray()));

        Assert.True(database.TryGetCharacter(3001, out var hero));
        Assert.Equal(3001, hero.Id);
        Assert.Contains(30010101, hero.SkillIds);

        Assert.True(database.TryGetSkill(30010101, out var skill));
        Assert.Equal(30010101, skill.CastFlowId);
        Assert.Equal(30010101, skill.LevelTableId);
        Assert.Equal(52010102, skill.RequiredTargetQueryId);

        Assert.True(database.TryGetSkillLevelTable(30010101, out var levelTable));
        Assert.Equal(
            new[] { "damage", "physicalAttackRatio", "range", "speed", "maxHits", "bounceRadius", "arcHeight", "arcDurationSeconds", "arcHeightPerUnit" },
            levelTable.ParamNames);
        Assert.True(levelTable.TryGetParameter(1, "damage", out var damage));
        Assert.Equal(200f, damage);
        Assert.True(levelTable.TryGetParameter(1, "maxHits", out var maxHits));
        Assert.Equal(4f, maxHits);

        Assert.True(database.TryGetProjectile(32010101, out var projectile));
        Assert.Equal(ProjectileHitPolicyKind.Bounce, projectile.HitPolicyKind);
        Assert.Equal(4, projectile.HitsRemaining);
        Assert.Equal(6f, projectile.BounceSearchRadius);
        Assert.Equal(1f, projectile.TrackingArcHeight);
        Assert.Equal(0.1f, projectile.TrackingArcHeightPerUnit);
        Assert.Equal(0f, projectile.TrackingArcDurationSeconds);
        Assert.Equal(30010111, projectile.OnHitEffectId);
        Assert.True(projectile.TryResolveRuntimeSettings(out var policyKind, out var policyParam,
            out var hitsRemaining, out var policyError), policyError);
        Assert.Equal(ProjectileHitPolicyKind.Bounce, policyKind);
        Assert.Equal(4, policyParam);
        Assert.Equal(4, hitsRemaining);

        Assert.True(database.TryGetProjectileLauncher(33010101, out var launcher));
        Assert.Equal(1, launcher.CountPerShot);
        Assert.Equal(0, launcher.DurationMs);
        Assert.Equal(0, launcher.IntervalMs);

        Assert.True(database.TryGetSearchQueryTemplate(52010101, out var query));
        Assert.Equal(1, query.MaxCount);
        Assert.Contains(query.Rules, rule => rule.Center == 1 && rule.Radius == 6f);
        Assert.Contains(query.Scorers, scorer => scorer.Source == 1);

        Assert.True(database.TryGetSearchQueryTemplate(52010102, out var initialTargetQuery));
        Assert.Equal(1, initialTargetQuery.MaxCount);
        Assert.Contains(initialTargetQuery.Rules, rule => rule.Center == 0 && rule.Radius == 12f);
        Assert.Contains(initialTargetQuery.Scorers, scorer => scorer.Source == 0);

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Unity", "Packages")))
            root = root.Parent;
        Assert.NotNull(root);
        var triggerPath = Path.Combine(root!.FullName, "Unity", "Packages",
            "com.abilitykit.demo.moba.view.runtime", "Resources", "ability", "ability_trigger_plans.json");
        var triggerJson = JObject.Parse(File.ReadAllText(triggerPath));
        var triggers = (JArray)triggerJson["Triggers"]!;
        var launchTrigger = triggers.Single(row => row.Value<int>("TriggerId") == 30010101);
        var launchAction = launchTrigger["Actions"]!.Children()
            .Single(action => action.Value<int>("ActionId") == 508656420);
        Assert.Equal(33010101, launchAction["Args"]!["launcher_id"]!.Value<int>("ConstValue"));
        Assert.Equal(32010101, launchAction["Args"]!["projectile_id"]!.Value<int>("ConstValue"));
        var trackTarget = launchAction["Args"]!["track_target"];
        Assert.True(trackTarget == null || trackTarget["ConstValue"] == null ||
                    trackTarget["ConstValue"]!.Value<int>() == 0);

        var hitTrigger = triggers.Single(row => row.Value<int>("TriggerId") == 30010111);
        var damageAction = hitTrigger["Actions"]!.Children()
            .Single(action => action.Value<int>("ActionId") == 176148626);
        Assert.Equal(30010101, damageAction["Args"]!["reason_param"]!["ConstValue"]!.Value<int>());
        Assert.Equal(500f, damageAction["Args"]!["damage_value"]!["ConstValue"]!.Value<float>());
        Assert.Equal(0.8f, damageAction["Args"]!["source_attack_ratio"]!["ConstValue"]!.Value<float>());
    }

    [Fact]
    public void Jhin_w_published_configuration_chain_is_resolvable_for_runtime_and_console()
    {
        var assets = new ConsoleTextAssetLoader();
        var database = new MobaConfigDatabase(textAssetLoader: assets);
        database.LoadFromGroups(MobaLubanConfigGroups.Create(assets,
            MobaRuntimeConfigTableRegistry.Tables.Select(table => table.FilePath).ToArray()));

        Assert.True(database.TryGetCharacter(3001, out var hero));
        Assert.Contains(30010201, hero.SkillIds);
        Assert.True(database.TryGetSkill(30010201, out var skill));
        Assert.Equal(30010201, skill.CastFlowId);
        Assert.Equal(30010201, skill.LevelTableId);
        Assert.Equal(0, skill.RequiredTargetQueryId);

        Assert.True(database.TryGetSkillLevelTable(30010201, out var levelTable));
        Assert.Equal(new[] { "damage", "physicalAttackRatio", "range", "speed" }, levelTable.ParamNames);
        Assert.True(levelTable.TryGetParameter(1, "damage", out var damage));
        Assert.Equal(80f, damage);
        Assert.True(database.TryGetProjectile(32010201, out var projectile));
        Assert.Equal(ProjectileHitPolicyKind.ExitOnHit, projectile.HitPolicyKind);
        Assert.Equal(1, projectile.HitsRemaining);
        Assert.Equal(30010212, projectile.OnHitEffectId);
        Assert.True(database.TryGetProjectileLauncher(33010201, out var launcher));
        Assert.Equal(1, launcher.CountPerShot);
        Assert.Equal(0, launcher.DurationMs);

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Unity", "Packages")))
            root = root.Parent;
        Assert.NotNull(root);
        var triggerPath = Path.Combine(root!.FullName, "Unity", "Packages",
            "com.abilitykit.demo.moba.view.runtime", "Resources", "ability", "ability_trigger_plans.json");
        var triggerJson = JObject.Parse(File.ReadAllText(triggerPath));
        var triggers = (JArray)triggerJson["Triggers"]!;
        var launchTrigger = triggers.Single(row => row.Value<int>("TriggerId") == 30010201);
        var launchAction = launchTrigger["Actions"]!.Children()
            .Single(action => action.Value<int>("ActionId") == 508656420);
        Assert.Equal(33010201, launchAction["Args"]!["launcher_id"]!["ConstValue"]!.Value<int>());
        Assert.Equal(32010201, launchAction["Args"]!["projectile_id"]!["ConstValue"]!.Value<int>());
        // The Trigger Authoring compiler omits false boolean constants; absence is the false default.
        var trackTarget = launchAction["Args"]!["track_target"];
        Assert.True(trackTarget == null || trackTarget["ConstValue"] == null ||
                    trackTarget["ConstValue"]!.Value<int>() == 0);

        var hitTrigger = triggers.Single(row => row.Value<int>("TriggerId") == 30010212);
        var damageAction = hitTrigger["Actions"]!.Children()
            .Single(action => action.Value<int>("ActionId") == 176148626);
        Assert.Equal(30010201, damageAction["Args"]!["reason_param"]!["ConstValue"]!.Value<int>());
        Assert.Equal(80f, damageAction["Args"]!["damage_value"]!["ConstValue"]!.Value<float>());
        Assert.Equal(0.5f, damageAction["Args"]!["source_attack_ratio"]!["ConstValue"]!.Value<float>());
    }

    [Fact]
    public void Jhin_e_published_configuration_chain_uses_dedicated_area_and_slow_buff()
    {
        const int heroId = 3001;
        const int skillId = 30010301;
        const int areaId = 42010301;
        const int slowBuffId = 32010301;
        const int launchTriggerId = 30010301;
        const int projectileId = 32010301;
        const int launcherId = 33010301;
        const int landingTriggerId = 30010312;
        const int enterTriggerId = 30010311;

        var assets = new ConsoleTextAssetLoader();
        var database = new MobaConfigDatabase(textAssetLoader: assets);
        database.LoadFromGroups(MobaLubanConfigGroups.Create(assets,
            MobaRuntimeConfigTableRegistry.Tables.Select(table => table.FilePath).ToArray()));

        Assert.True(database.TryGetCharacter(heroId, out var hero));
        Assert.Contains(skillId, hero.SkillIds);
        Assert.True(database.TryGetSkill(skillId, out var skill));
        Assert.Equal(skillId, skill.CastFlowId);
        Assert.Equal(skillId, skill.LevelTableId);
        Assert.True(database.TryGetSkillLevelTable(skillId, out var levelTable));
        Assert.Contains("damage", levelTable.ParamNames);

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Unity", "Packages")))
            root = root.Parent;
        Assert.NotNull(root);
        var repoRoot = root!.FullName;
        var unityResources = Path.Combine(repoRoot, "Unity", "Packages",
            "com.abilitykit.demo.moba.view.runtime", "Resources");
        var consoleResources = Path.Combine(repoRoot, "src", "AbilityKit.Demo.Moba.Console", "Configs", "luban");

        var aoes = JArray.Parse(File.ReadAllText(Path.Combine(unityResources, "luban", "moba", "aoes.json")));
        var area = aoes.Single(row => row!.Value<int>("Id") == areaId);
        Assert.Equal(500, area!["DelayMs"]!.Value<int>());
        Assert.Equal(5000, area["DurationMs"]!.Value<int>());
        Assert.True(area["ConsumeOnFirstEnter"]!.Value<bool>());
        Assert.Contains(enterTriggerId, area["OnEnterTriggerIds"]!.Values<int>());

        var buffs = JArray.Parse(File.ReadAllText(Path.Combine(unityResources, "luban", "moba", "buffs.json")));
        var slowBuff = buffs.Single(row => row!.Value<int>("Id") == slowBuffId);
        Assert.Equal(1500, slowBuff!["DurationMs"]!.Value<int>());
        Assert.Contains(100003, slowBuff["ModifierIds"]!.Values<int>());

        var unityTriggers = JObject.Parse(File.ReadAllText(Path.Combine(unityResources, "ability", "ability_trigger_plans.json")));
        var consoleTriggers = JObject.Parse(File.ReadAllText(Path.Combine(consoleResources, "ability", "ability_trigger_plans.json")));
        Assert.True(JToken.DeepEquals(unityTriggers, consoleTriggers));
        var triggers = (JArray)unityTriggers["Triggers"]!;
        var launch = triggers.Single(row => row!.Value<int>("TriggerId") == launchTriggerId);
        var projectileLaunch = Assert.Single(launch["Actions"]!.Children(), action =>
            action["ActionId"]?.Value<int>() == 508656420);
        Assert.Equal(projectileId, projectileLaunch["Args"]!["projectile_id"]!["ConstValue"]!.Value<int>());
        Assert.Equal(launcherId, projectileLaunch["Args"]!["launcher_id"]!["ConstValue"]!.Value<int>());
        var launchers = JArray.Parse(File.ReadAllText(Path.Combine(unityResources, "luban", "moba", "projectile_launchers.json")));
        Assert.Single(launchers, row => row!.Value<int>("Id") == launcherId);
        var projectiles = JArray.Parse(File.ReadAllText(Path.Combine(unityResources, "luban", "moba", "projectiles.json")));
        var projectile = Assert.Single(projectiles, row => row!.Value<int>("Id") == projectileId);
        Assert.Equal(new[] { landingTriggerId }, projectile["OnExitTriggerIds"]!.Values<int>());
        var landing = triggers.Single(row => row!.Value<int>("TriggerId") == landingTriggerId);
        Assert.Contains(landing["Actions"]!.Children(), action =>
            action["Args"]?["area_id"]?["ConstValue"]?.Value<int>() == areaId);

        var enter = triggers.Single(row => row!.Value<int>("TriggerId") == enterTriggerId);
        var actions = enter["Actions"]!.Children().ToArray();
        Assert.Contains(actions, action =>
            action["ActionId"]?.Value<int>() == 176148626 &&
            action["Args"]?["reason_param"]?["ConstValue"]?.Value<int>() == skillId);
        Assert.Contains(actions, action =>
            action["Args"]?["buff_ids"]?["ConstValue"]?.Value<int>() == slowBuffId);
        Assert.Contains(actions, action =>
            action["ActionId"]?.Value<int>() == 613578853 &&
            action["Args"]?["template_id"]?["ConstValue"]?.Value<int>() == areaId);
    }

    [Fact]
    public void Jhin_r_published_configuration_chain_applies_refreshing_slow_after_damage()
    {
        const int slowBuffId = 32010403;
        const int regularHitTriggerId = 30010402;
        const int finalHitTriggerId = 30010412;
        const int damageActionId = 176148626;

        var assets = new ConsoleTextAssetLoader();
        var database = new MobaConfigDatabase(textAssetLoader: assets);
        database.LoadFromGroups(MobaLubanConfigGroups.Create(assets,
            MobaRuntimeConfigTableRegistry.Tables.Select(table => table.FilePath).ToArray()));

        var slowBuff = database.GetDto<BuffDTO>(slowBuffId);
        Assert.Equal(500, slowBuff.DurationMs);
        Assert.Equal(1, slowBuff.MaxStacks);
        Assert.Equal(4, slowBuff.StackingPolicy); // RefreshDuration
        Assert.Equal(2, slowBuff.RefreshPolicy); // ResetRemaining
        Assert.NotNull(slowBuff.Modifiers);
        Assert.NotEmpty(slowBuff.Modifiers);
        Assert.All(slowBuff.Modifiers, Assert.NotNull);
        Assert.Contains(slowBuff.Modifiers, modifier => modifier.TargetId == 17 && modifier.DefinitionId == 1000003);

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Unity", "Packages")))
            root = root.Parent;
        Assert.NotNull(root);
        var repoRoot = root!.FullName;
        var unityResources = Path.Combine(repoRoot, "Unity", "Packages",
            "com.abilitykit.demo.moba.view.runtime", "Resources");
        var consoleResources = Path.Combine(repoRoot, "src", "AbilityKit.Demo.Moba.Console", "Configs", "luban");
        var unityTriggers = JObject.Parse(File.ReadAllText(Path.Combine(unityResources, "ability", "ability_trigger_plans.json")));
        var consoleTriggers = JObject.Parse(File.ReadAllText(Path.Combine(consoleResources, "ability", "ability_trigger_plans.json")));
        Assert.True(JToken.DeepEquals(unityTriggers, consoleTriggers));

        var triggers = (JArray)unityTriggers["Triggers"]!;
        foreach (var triggerId in new[] { regularHitTriggerId, finalHitTriggerId })
        {
            var trigger = triggers.Single(row => row!.Value<int>("TriggerId") == triggerId);
            var actions = trigger["Actions"]!.Children().ToArray();
            Assert.True(actions.Length >= 2, $"R hit trigger {triggerId} should damage before adding its slow.");
            Assert.Equal(damageActionId, actions[0]["ActionId"]!.Value<int>());
            Assert.Equal(30010401, actions[0]["Args"]!["reason_param"]!["ConstValue"]!.Value<int>());
            Assert.Equal(slowBuffId, actions[1]["Args"]!["buff_ids"]!["ConstValue"]!.Value<int>());
        }
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

    [Fact]
    public void Veigar_published_presentation_contract_uses_formal_model_vfx_and_action_frames()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Unity", "Packages")))
            root = root.Parent;
        Assert.NotNull(root);
        var resources = Path.Combine(root!.FullName, "Unity", "Packages",
            "com.abilitykit.demo.moba.view.runtime", "Resources");
        var luban = Path.Combine(resources, "luban", "moba");

        var models = JArray.Parse(File.ReadAllText(Path.Combine(luban, "models.json")));
        var model = Assert.Single(models, row => row!.Value<int>("Id") == 3004);
        Assert.Equal("ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_Model",
            model!["PrefabPath"]!.Value<string>());

        var allVfx = JArray.Parse(File.ReadAllText(Path.Combine(luban, "vfx.json")))
            .Cast<JObject>().ToDictionary(row => row.Value<int>("Id"));
        var vfx = allVfx.Values.Where(row => row.Value<int>("Id") is >= 90034001 and <= 90034015)
            .ToDictionary(row => row.Value<int>("Id"));
        Assert.Equal(15, vfx.Count);
        Assert.All(vfx.Values, row => Assert.StartsWith("ChampionVfx/09.Veigar_Hextech/",
            row.Value<string>("Resource")));
        Assert.Equal("ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_Q_Projectile",
            vfx[90034008].Value<string>("Resource"));
        Assert.Equal("ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_W_Explosion",
            vfx[90034014].Value<string>("Resource"));

        var projectiles = JArray.Parse(File.ReadAllText(Path.Combine(luban, "projectiles.json")))
            .Cast<JObject>().ToDictionary(row => row.Value<int>("Id"));
        Assert.Equal(90034008, projectiles[32040101].Value<int>("VfxId"));
        Assert.Equal(90034007, projectiles[32040101].Value<int>("OnHitVfxId"));
        Assert.Equal(90034011, projectiles[32040501].Value<int>("VfxId"));
        Assert.Equal(90034010, projectiles[32040501].Value<int>("OnHitVfxId"));

        var aoes = JArray.Parse(File.ReadAllText(Path.Combine(luban, "aoes.json")))
            .Cast<JObject>().ToDictionary(row => row.Value<int>("Id"));
        // Areas use anchored derivatives; projectile and timeline source resources stay intact.
        Assert.Equal(90034016, aoes[42040301].Value<int>("VfxId"));
        Assert.Equal("MobaVfx/Veigar/W_Area", allVfx[90034016].Value<string>("Resource"));
        Assert.Equal(0, aoes[42040401].Value<int>("VfxId"));
        Assert.Equal(90034017, aoes[42040402].Value<int>("VfxId"));
        Assert.Equal(new[] { 30040413 }, aoes[42040401]["OnCrossTriggerIds"]!.Values<int>());
        Assert.Empty(aoes[42040401]["OnExitTriggerIds"]?.Values<int>() ?? Array.Empty<int>());
        Assert.Equal("MobaVfx/Veigar/E_Area", allVfx[90034017].Value<string>("Resource"));

        var skills = JArray.Parse(File.ReadAllText(Path.Combine(luban, "skills.json")))
            .Cast<JObject>().ToDictionary(row => row.Value<int>("Id"));
        Assert.Equal(new[] { 90034001, 90034008, 90034013, 90034004, 90034011 },
            new[] { 30040101, 30040201, 30040301, 30040401, 30040501 }
                .Select(id => skills[id].Value<int>("PreviewVfxId")).ToArray());

        var actions = JObject.Parse(File.ReadAllText(Path.Combine(resources, "moba", "character_view_actions.json")));
        var variants = (JArray)actions["variants"]!;
        var expected = new Dictionary<int, (string State, int Frames)>
        {
            [30040101] = ("Veigar_Model_AA", 48),
            [30040201] = ("Veigar_Model_SpellQ", 38),
            [30040301] = ("Veigar_Model_SpellW", 80),
            [30040401] = ("Veigar_Model_SpellE", 50),
            [30040501] = ("Veigar_Model_SpellR", 38),
        };
        foreach (var pair in expected)
        {
            var variant = Assert.Single(variants, row => row!.Value<int?>("entityCode") == 3004 &&
                row.Value<int?>("skillId") == pair.Key);
            var play = Assert.Single(variant!["action"]!["actions"]!, row => row!.Value<string>("type") == "play");
            Assert.Equal(pair.Value.State, play!["animatorState"]!.Value<string>());
            Assert.Equal(pair.Value.Frames, play["frameCount"]!.Value<int>());
            Assert.Equal(Math.Round(pair.Value.Frames / 24m, 6),
                variant["action"]!["actions"]![1]!["seconds"]!.Value<decimal>());
        }
    }

    [Fact]
    public void Veigar_r_published_configuration_uses_target_missing_health_formula_and_cap()
    {
        var assets = new ConsoleTextAssetLoader();
        Assert.True(assets.TryLoadText("luban/ability/ability_trigger_plans.json", out var text));
        var triggers = (JArray)JObject.Parse(text)["Triggers"]!;
        var trigger = triggers.Single(row => row!.Value<int>("TriggerId") == 30040511);
        var action = trigger["Actions"]!.Children()
            .Single(row => row.Value<int>("ActionId") == 176148626);
        var args = action["Args"]!;

        Assert.Equal(150f, args["damage_value"]!["ConstValue"]!.Value<float>());
        Assert.Equal(0.5f, args["source_attack_ratio"]!["ConstValue"]!.Value<float>());
        Assert.Equal(0.5f, args["target_missing_hp_ratio_coefficient"]!["ConstValue"]!.Value<float>());
        Assert.Equal(300f, args["target_missing_hp_bonus_max"]!["ConstValue"]!.Value<float>());
        Assert.Equal(600f, args["damage_max"]!["ConstValue"]!.Value<float>());
    }

    [Theory]
    [InlineData(30040101, 90034001)]
    [InlineData(30040201, 90034008)]
    [InlineData(30040301, 90034013)]
    [InlineData(30040401, 90034004)]
    [InlineData(30040501, 90034011)]
    public void Veigar_action_timeline_presentation_references_published_vfx(int skillId, int vfxId)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Unity", "Packages")))
            root = root.Parent;
        Assert.NotNull(root);
        var path = Path.Combine(root!.FullName, "Unity", "Packages",
            "com.abilitykit.demo.moba.view.runtime", "Resources", "moba", "action_timeline",
            $"skill_{skillId}.moba.presentation.json");
        var timeline = JObject.Parse(File.ReadAllText(path));
        var ids = timeline.Descendants().OfType<JObject>()
            .Where(row => row.Value<string>("type") == "AbilityKit.ActionEditorImpl.PlayParticle")
            .Select(row => row["args"]?["presentationTemplateId"]?.Value<int>())
            .Where(id => id.HasValue).Select(id => id!.Value).ToArray();
        Assert.Contains(vfxId, ids);
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
