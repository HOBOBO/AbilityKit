using System;
using System.Diagnostics;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Console.Bootstrap;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Continuous;
using AbilityKit.GameplayTags;
using AbilityKit.Protocol.Moba.StateSync;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using moba_luban;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaTagResultTests
{
    private readonly ITestOutputHelper _output;

    public MobaTagResultTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Workbook_rules_produce_expected_result_bits_and_control_gate()
    {
        var runtime = new MobaTagWorkbookRuntime(new ConsoleTextAssetLoader());
        Assert.Equal(MobaTagResultFlags.None, runtime.Evaluate(new GameplayTagContainer()));
        Assert.True(runtime.GetRule(10000001).Matches(new GameplayTagContainer()));
        var stunned = MobaGameplayTagCatalog.ToContainer(new[] { "State.Stunned" });
        Assert.Equal(MobaTagResultFlags.CannotMove | MobaTagResultFlags.CannotCast |
                     MobaTagResultFlags.Stunned | MobaTagResultFlags.CannotBeControlled |
                     MobaTagResultFlags.CannotAttack, runtime.Evaluate(stunned));
        var armor = MobaGameplayTagCatalog.ToContainer(new[] { "State.SuperArmor" });
        Assert.Equal(MobaTagResultFlags.ControlImmune, runtime.Evaluate(armor));
        Assert.False(runtime.GetRule(10000001).Matches(armor));
        Assert.True(runtime.TryGetBehaviorGateMask(MobaTagBehaviorGate.Controlled, out var controlMask));
        Assert.Equal(MobaTagResultFlags.ControlImmune | MobaTagResultFlags.CannotBeControlled, controlMask);
        Assert.False(runtime.TryGetBehaviorGateMask((MobaTagBehaviorGate)999999, out _));
    }

    [Fact]
    public void Manual_tag_snapshot_explains_sources_rules_and_lifecycle_without_following_later_changes()
    {
        var manager = new DefaultContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        var snapshots = new MobaTagDebugSnapshotService(manager, effective);
        var first = new TaggedContinuous(1, "State.Stunned");
        var second = new TaggedContinuous(1, "State.Stunned");
        var otherActor = new TaggedContinuous(2, "State.Silenced");
        Assert.True(manager.TryActivate(first));
        Assert.True(manager.TryActivate(second));
        Assert.True(manager.TryActivate(otherActor));

        var before = snapshots.Capture(1, "test-world", includeRules: true);
        Assert.Equal("test-world", before.WorldId);
        Assert.Equal(1, before.ActorId);
        var tag = Assert.Single(before.Tags, item => item.Name == "State.Stunned");
        Assert.Equal(2, tag.SourceIndices.Length);
        Assert.All(tag.SourceIndices, index => Assert.True(before.Sources[index].IsEffective));
        Assert.All(tag.SourceIndices, index =>
            Assert.Contains("State.Stunned", before.Sources[index].GrantedTagNames));
        Assert.DoesNotContain(before.Tags, item => item.Name == "State.Silenced");
        Assert.Contains("CannotMove", before.ResultFlags);
        Assert.Contains(before.Gates, gate => gate.Key == "Move" && gate.Blocked);
        Assert.Contains(before.Rules, rule => rule.Matches &&
            rule.Nodes.Any(node => node.TagName == "State.Stunned" &&
                node.Matches && node.SourceIndices.Length == 2));

        Assert.True(manager.TryPause(first));
        var paused = snapshots.Capture(1);
        Assert.Single(paused.Tags.Single(item => item.Name == "State.Stunned").SourceIndices);
        Assert.Contains(paused.Sources, source => !source.IsEffective);
        Assert.True(manager.TryEnd(second));
        var ended = snapshots.Capture(1);
        Assert.DoesNotContain(ended.Tags, item => item.Name == "State.Stunned");
        Assert.DoesNotContain("CannotMove", ended.ResultFlags);
        Assert.Contains(ended.Gates, gate => gate.Key == "Move" && !gate.Blocked);
        Assert.Equal(2, before.Tags.Single(item => item.Name == "State.Stunned").SourceIndices.Length);
        Assert.Equal(before.ResultMask,
            JsonConvert.DeserializeObject<MobaTagDebugSnapshot>(JsonConvert.SerializeObject(before))!.ResultMask);
    }

    [Fact]
    public void Debug_rule_tree_matches_compiled_queries_for_representative_tag_sets()
    {
        var workbook = new MobaTagWorkbookRuntime(new ConsoleTextAssetLoader());
        foreach (var names in new[]
                 {
                     Array.Empty<string>(),
                     new[] { "State.Stunned" },
                     new[] { "State.SuperArmor" },
                     new[] { "State.Silenced", "State.SuperArmor" }
                 })
        {
            var tags = MobaGameplayTagCatalog.ToContainer(names);
            foreach (var rule in workbook.GetDebugRules(tags, Array.Empty<MobaTagDebugSource>()))
            {
                Assert.Equal(workbook.GetRule(rule.GroupId).Matches(tags), rule.Matches);
                Assert.Equal(rule.Matches, rule.Nodes[0].Matches);
            }
        }
    }

    [Fact]
    public void Battle_world_registers_on_demand_tag_snapshot_service()
    {
        using var bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        bootstrapper.Initialize();
        bootstrapper.Start();
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
        bootstrapper.SetupBattle();
        for (var i = 0; i < 10; i++) bootstrapper.Tick();

        var services = bootstrapper.RuntimeServices!;
        var actorId = services.Resolve<MobaActorRegistry>().Entries.First().Key;
        var snapshots = services.Resolve<MobaTagDebugSnapshotService>();
        Assert.NotNull(snapshots);
        Assert.Equal(actorId, snapshots.Capture(actorId).ActorId);
    }

    [Fact]
    public void Rule_query_uses_active_continuous_tags_for_the_selected_actor()
    {
        var manager = new DefaultContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        Assert.False(effective.TryMatchesRule(1, 999999, out _));
        Assert.True(effective.TryMatchesRule(1, 2004, out var before));
        Assert.False(before);
        var stunned = new TaggedContinuous(1, "State.Stunned");
        Assert.True(manager.TryActivate(stunned));
        Assert.True(effective.TryMatchesRule(1, 2004, out var during));
        Assert.True(during);
        Assert.True(effective.TryMatchesRule(2, 2004, out var other));
        Assert.False(other);
        Assert.True(manager.TryEnd(stunned));
        Assert.True(effective.TryMatchesRule(1, 2004, out var after));
        Assert.False(after);
    }

    [Fact]
    public void Unregistered_continuous_does_not_remain_in_tag_rule_history()
    {
        using var manager = new MobaContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        using var rules = new MobaContinuousTagRuleService();
        rules.OnInit(new Resolver(manager, effective, new MobaContinuousTagRuleEvaluator(effective)));
        var continuous = new TaggedContinuous(1, "State.Stunned");
        rules.OnRegistered(continuous, null!);
        Assert.True(rules.GetLastResult(continuous).HasResult);
        rules.OnUnregistered(continuous, ContinuousEndReason.Completed, null!);
        Assert.False(rules.GetLastResult(continuous).HasResult);
    }

    [Fact]
    public void Nested_tag_reconciliation_processes_the_other_actor()
    {
        var manager = new MobaContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        Assert.True(manager.TryActivate(new TaggedContinuous(1, "State.Stunned",
            ongoingRequiredTag: "State.SuperArmor")));
        Assert.True(manager.TryActivate(new TaggedContinuous(2, "State.Silenced",
            ongoingRequiredTag: "State.SuperArmor")));
        using var rules = new MobaContinuousTagRuleService();
        var evaluator = new CallbackTagEvaluator();
        var nested = false;
        evaluator.OnEvaluate = continuous =>
        {
            if (continuous.Config.OwnerId == 1 && !nested)
            {
                nested = true;
                rules.ReconcileOwner(2);
            }
        };
        rules.OnInit(new Resolver(manager, effective, evaluator));
        rules.ReconcileOwner(1);
        Assert.True(nested);
        Assert.Equal(1, evaluator.ActorTwoEvaluations);
    }

    [Fact]
    public void Repeated_hot_result_reads_do_not_rebuild_tag_cache()
    {
        var manager = new DefaultContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        var active = new TaggedContinuous(1, "State.Stunned");
        Assert.True(manager.TryActivate(active));
        Assert.NotEqual(MobaTagResultFlags.None, effective.GetResultFlags(1));
        var before = effective.Metrics;
        for (var i = 0; i < 1000; i++)
            Assert.NotEqual(MobaTagResultFlags.None, effective.GetResultFlags(1));
        var after = effective.Metrics;
        Assert.Equal(before.CacheRebuilds, after.CacheRebuilds);
        Assert.Equal(before.ResultReads + 1000, after.ResultReads);
        Assert.True(manager.TryEnd(active));
        Assert.Equal(MobaTagResultFlags.None, effective.GetResultFlags(1));
        Assert.Equal(after.CacheRebuilds + 1, effective.Metrics.CacheRebuilds);
    }

    [Fact]
    public void Read_only_tag_queries_reuse_the_cache_without_allocating_snapshots()
    {
        var manager = new DefaultContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        Assert.True(manager.TryActivate(new TaggedContinuous(1, "State.Stunned")));
        Assert.True(MobaGameplayTagCatalog.TryGet("State", out var parent));
        Assert.True(MobaGameplayTagCatalog.TryGet("State.Stunned", out var stunned));
        var queried = MobaGameplayTagCatalog.ToContainer(new[] { "State.Stunned" });
        Assert.True(effective.HasTag(1, parent));
        Assert.False(effective.HasTag(1, parent, exact: true));
        Assert.True(effective.HasAnyTag(1, queried));

        var before = effective.Metrics;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            Assert.True(effective.HasTag(1, stunned));
            Assert.True(effective.HasAnyTag(1, queried));
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
        Assert.Equal(before.CacheRebuilds, effective.Metrics.CacheRebuilds);
        Assert.True(effective.Metrics.ScannedContinuous >= 1);
        Assert.Equal(effective.Metrics.CacheRebuilds * effective.DebugWorkbook.ResultMappingCount,
            effective.Metrics.MappingEvaluations);
    }

    [Fact]
    public void Tag_query_benchmark_reports_cached_reads_and_rebuilds()
    {
        var manager = new DefaultContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        Assert.True(manager.TryActivate(new TaggedContinuous(1, "State.Stunned")));
        effective.GetResultFlags(1);
        const int reads = 100_000;
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < reads; i++) effective.GetResultFlags(1);
        var elapsed = Stopwatch.GetElapsedTime(start);
        Assert.Equal(1, effective.Metrics.CacheRebuilds);
        _output.WriteLine($"cached tag result: {reads} reads in {elapsed.TotalMilliseconds:0.###} ms; " +
                          $"rebuilds={effective.Metrics.CacheRebuilds}");
    }

    [Fact]
    public void Multi_actor_gate_and_rule_reads_reuse_cached_tags()
    {
        var manager = new DefaultContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        const int actors = 100;
        const int rounds = 1000;
        for (var actorId = 1; actorId <= actors; actorId++)
            Assert.True(manager.TryActivate(new TaggedContinuous(actorId, "State.Stunned")));
        for (var actorId = 1; actorId <= actors; actorId++)
            Assert.True(effective.TryIsBlocked(actorId, MobaTagBehaviorGate.Move, out _));
        var before = effective.Metrics;
        var allocationsBefore = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        for (var round = 0; round < rounds; round++)
            for (var actorId = 1; actorId <= actors; actorId++)
            {
                effective.TryIsBlocked(actorId, MobaTagBehaviorGate.Move, out _);
                effective.TryMatchesRule(actorId, 2004, out _);
            }
        var elapsed = Stopwatch.GetElapsedTime(start);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationsBefore;
        Assert.Equal(before.CacheRebuilds, effective.Metrics.CacheRebuilds);
        Assert.Equal(before.ResultReads + actors * rounds, effective.Metrics.ResultReads);
        Assert.Equal(before.RuleReads + actors * rounds, effective.Metrics.RuleReads);
        _output.WriteLine($"{actors} actors, {rounds} rounds: {elapsed.TotalMilliseconds:0.###} ms, " +
                          $"allocated={allocated} bytes, cache rebuilds={effective.Metrics.CacheRebuilds}");
    }

    [Fact]
    public void Published_tag_definition_ids_resolve_only_active_combat_tags()
    {
        var runtime = new MobaTagWorkbookRuntime(new ConsoleTextAssetLoader());
        Assert.True(runtime.TryGetActiveTagName(1006, out var slow));
        Assert.Equal("Debuff.Slow", slow);
        Assert.False(runtime.TryGetActiveTagName(90, out _));
        Assert.False(runtime.TryGetActiveTagName(999999, out _));
    }

    [Fact]
    public void Catalog_exposes_stable_definition_ids_and_rejects_unknown_config_tags()
    {
        _ = new MobaTagWorkbookRuntime(new ConsoleTextAssetLoader());
        var version = MobaGameplayTagCatalog.CatalogVersion;
        Assert.False(string.IsNullOrEmpty(version));
        Assert.Equal(MobaGameplayTagCatalog.BundleVersion,
            MobaGameplayTagCatalog.VerifyPublishedBundle(new ConsoleTextAssetLoader()));
        Assert.True(MobaGameplayTagCatalog.TryGetDefinitionId("State.Stunned", out var id));
        Assert.Equal(1020, id);
        _ = new MobaTagWorkbookRuntime(new ConsoleTextAssetLoader());
        Assert.Equal(version, MobaGameplayTagCatalog.CatalogVersion);
        Assert.Throws<InvalidOperationException>(() =>
            MobaGameplayTagCatalog.ToConfigContainer(new[] { "State.MissingConfigurationTag" }));
        GameplayTagManager.Instance.RegisterTags(new[] { "State.ExternalTag" });
        Assert.Throws<InvalidOperationException>(() =>
            MobaGameplayTagCatalog.ToConfigContainer(new[] { "State.ExternalTag" }));
    }

    [Fact]
    public void Tag_bundle_rejects_a_mixed_binary_publication()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            MobaGameplayTagCatalog.VerifyPublishedBundle(new TamperedTagAssets()));
        Assert.Contains("tag_rule_nodes", error.Message);
    }

    [Fact]
    public void Repeated_tag_churn_rebuilds_only_the_changed_actor_cache()
    {
        var manager = new DefaultContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        effective.GetResultFlags(2);
        var otherRebuilds = effective.Metrics.CacheRebuilds;
        const int changes = 100;
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < changes; i++)
        {
            var tag = new TaggedContinuous(1, "State.Stunned");
            Assert.True(manager.TryActivate(tag));
            Assert.NotEqual(MobaTagResultFlags.None, effective.GetResultFlags(1));
            Assert.True(manager.TryEnd(tag));
            Assert.Equal(MobaTagResultFlags.None, effective.GetResultFlags(1));
            effective.GetResultFlags(2);
        }
        Assert.Equal(otherRebuilds + 2 * changes, effective.Metrics.CacheRebuilds);
        _output.WriteLine($"{changes} tag add/remove pairs: " +
                          $"{Stopwatch.GetElapsedTime(start).TotalMilliseconds:0.###} ms");
    }

    [Fact]
    public void Marker_tags_do_not_create_flags_and_hierarchical_state_tags_match()
    {
        var runtime = new MobaTagWorkbookRuntime(new ConsoleTextAssetLoader());
        var marker = MobaGameplayTagCatalog.ToContainer(new[] { "Debuff.Slow" });
        Assert.Equal(MobaTagResultFlags.None, runtime.Evaluate(marker));
        GameplayTagManager.Instance.RegisterTags(new[] { "State.Rooted.Special" });
        var child = MobaGameplayTagCatalog.ToContainer(new[] { "State.Rooted.Special" });
        Assert.Equal(MobaTagResultFlags.CannotMove, runtime.Evaluate(child));
        var silence = MobaGameplayTagCatalog.ToContainer(new[] { "State.Silenced" });
        Assert.Equal(MobaTagResultFlags.CannotCast, runtime.Evaluate(silence));
    }

    [Theory]
    [InlineData("State.Feared")]
    [InlineData("State.Asleep")]
    public void Fear_and_sleep_block_attacks_without_being_classified_as_stun(string tag)
    {
        var runtime = new MobaTagWorkbookRuntime(new ConsoleTextAssetLoader());
        var flags = runtime.Evaluate(MobaGameplayTagCatalog.ToContainer(new[] { tag }));
        Assert.Equal(MobaTagResultFlags.CannotCast | MobaTagResultFlags.CannotMove |
                     MobaTagResultFlags.CannotBeControlled | MobaTagResultFlags.CannotAttack, flags);
    }

    [Fact]
    public void Planned_tag_hierarchy_is_registered_without_activating_a_result_flag()
    {
        MobaGameplayTagCatalog.RegisterDefinitions(new ConsoleTextAssetLoader());
        Assert.True(MobaGameplayTagCatalog.TryGet("Skill", out _));
        Assert.False(MobaGameplayTagCatalog.TryGet("Skill.Hero.LianPo.S1", out _));
        Assert.True(MobaGameplayTagCatalog.TryGet("State.Movement.Dashing", out var dash));
        Assert.True(MobaGameplayTagCatalog.TryGet("State.Movement", out var movement));
        Assert.True(dash.IsChildOf(movement));
        var runtime = new MobaTagWorkbookRuntime(new ConsoleTextAssetLoader());
        Assert.Equal(MobaTagResultFlags.None,
            runtime.Evaluate(MobaGameplayTagCatalog.ToContainer(new[] { "State.Movement.Dashing" })));
    }

    [Fact]
    public void World_query_distinguishes_missing_result_provider_from_empty_results()
    {
        var missing = new AbilityKit.Moba.Behavior.MobaWorldQuery(null, null, null, tagResults: _ => null);
        var empty = new AbilityKit.Moba.Behavior.MobaWorldQuery(null, null, null, tagResults: _ => MobaTagResultFlags.None);
        var owner = new AbilityKit.Ability.Behavior.BehaviorEntityId(1);
        Assert.False(missing.TryHasResult(owner, MobaTagResultFlags.CannotMove, out _));
        Assert.True(empty.TryHasResult(owner, MobaTagResultFlags.CannotMove, out var blocked));
        Assert.False(blocked);
        var configured = new AbilityKit.Moba.Behavior.MobaWorldQuery(null, null, null,
            tagGate: (_, gateId) => gateId == MobaTagBehaviorGate.Move);
        Assert.True(configured.TryIsBlocked(owner, MobaTagBehaviorGate.Move, out var moveBlocked));
        Assert.True(moveBlocked);
        Assert.True(configured.TryIsBlocked(owner, MobaTagBehaviorGate.Cast, out var castBlocked));
        Assert.False(castBlocked);
    }

    [Fact]
    public void Only_continuous_lifecycles_change_results_and_query_snapshots_cannot_mutate_them()
    {
        var tags = new AbilityKit.Ability.Tags.GameplayTagService();
        var manager = new DefaultContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(tags, manager, new ConsoleTextAssetLoader()));
        MobaGameplayTagCatalog.TryGet("State.Stunned", out var stunned);
        Assert.Equal(MobaTagResultFlags.None, effective.GetResultFlags(1));
        tags.AddTag(1, stunned, new GameplayTagSource(101));
        Assert.Equal(MobaTagResultFlags.None, effective.GetResultFlags(1));
        var snapshot = effective.GetEffectiveTags(1);
        snapshot.Add(stunned);
        Assert.Equal(MobaTagResultFlags.None, effective.GetResultFlags(1));
        Assert.False(effective.GetEffectiveTags(1).HasTagExact(stunned));
        var continuous = new TaggedContinuous(1, "State.Stunned");
        Assert.True(manager.TryActivate(continuous));
        Assert.True((effective.GetResultFlags(1) & MobaTagResultFlags.CannotMove) != 0);
        Assert.True(manager.TryEnd(continuous, ContinuousEndReason.Completed));
        Assert.Equal(MobaTagResultFlags.None, effective.GetResultFlags(1));
        Assert.True(effective.RemoveActor(1));
        Assert.Equal(MobaTagResultFlags.None, effective.GetResultFlags(1));
    }

    [Fact]
    public void Continuous_lifecycle_refreshes_flags_and_preserves_other_sources()
    {
        var manager = new DefaultContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        var first = new TaggedContinuous(1, "State.Stunned");
        var second = new TaggedContinuous(1, "State.Stunned");
        var otherOwner = new TaggedContinuous(2, "State.Stunned");
        Assert.Equal(MobaTagResultFlags.None, effective.GetResultFlags(1));
        Assert.True(manager.Register(first));
        Assert.Equal(MobaTagResultFlags.None, effective.GetResultFlags(1));
        Assert.True(manager.TryActivate(first));
        var expected = effective.GetResultFlags(1);
        Assert.True((expected & MobaTagResultFlags.CannotMove) != 0);
        Assert.True(manager.TryPause(first));
        Assert.Equal(MobaTagResultFlags.None, effective.GetResultFlags(1));
        Assert.True(manager.TryResume(first));
        Assert.Equal(expected, effective.GetResultFlags(1));
        Assert.True(manager.TryActivate(second));
        Assert.True(manager.TryActivate(otherOwner));
        Assert.True(manager.TryEnd(first, ContinuousEndReason.Completed));
        Assert.Equal(expected, effective.GetResultFlags(1));
        Assert.True(manager.TryEnd(second, ContinuousEndReason.Interrupted));
        Assert.Equal(MobaTagResultFlags.None, effective.GetResultFlags(1));
        Assert.Equal(expected, effective.GetResultFlags(2));
        Assert.True(manager.TryEnd(otherOwner, ContinuousEndReason.Completed));
        Assert.Equal(MobaTagResultFlags.None, effective.GetResultFlags(2));
    }

    [Fact]
    public void External_pause_reconciles_other_continuous_requirements_without_rebuilding_unchanged_cache()
    {
        using var manager = new MobaContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        var evaluator = new MobaContinuousTagRuleEvaluator(effective);
        using var rules = new MobaContinuousTagRuleService();
        rules.OnInit(new Resolver(manager, effective, evaluator));
        var source = new TaggedContinuous(1, "State.SuperArmor");
        var dependent = new TaggedContinuous(1, "Debuff.Slow", ongoingRequiredTag: "State.SuperArmor");

        Assert.True(manager.TryActivate(source));
        Assert.True(manager.TryActivate(dependent));
        Assert.Equal(evaluator.Evaluate(dependent).Action, evaluator.EvaluateFast(dependent).Action);
        Assert.True(manager.TryPause(source));
        Assert.True(dependent.IsPaused);
        Assert.Equal(evaluator.Evaluate(dependent).Action, evaluator.EvaluateFast(dependent).Action);
        Assert.True(manager.TryResume(source));
        Assert.True(dependent.IsActive);

        var before = effective.Metrics.CacheRebuilds;
        rules.ReconcileOwner(1);
        Assert.Equal(before, effective.Metrics.CacheRebuilds);
    }

    [Fact]
    public void Removal_condition_ends_a_continuous_while_it_is_paused_by_tag_rules()
    {
        using var manager = new MobaContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        var evaluator = new MobaContinuousTagRuleEvaluator(effective);
        using var rules = new MobaContinuousTagRuleService();
        rules.OnInit(new Resolver(manager, effective, evaluator));
        var prerequisite = new TaggedContinuous(1, "State.SuperArmor");
        var subject = new TaggedContinuous(1, "Debuff.Slow", ongoingRequiredTag: "State.SuperArmor");
        ((TaggedConfig)subject.Config).TagRequirements.RemovalRequired = GameplayTagRequirements.Require(
            MobaGameplayTagCatalog.ToContainer(new[] { "State.Stunned" }).First());

        Assert.True(manager.TryActivate(prerequisite));
        Assert.True(manager.TryActivate(subject));
        Assert.True(manager.TryEnd(prerequisite));
        Assert.True(subject.IsPaused);

        Assert.True(manager.TryActivate(new TaggedContinuous(1, "State.Stunned")));
        Assert.True(subject.IsTerminated);
        Assert.DoesNotContain(subject, manager.GetOwnerContinuous(1));
    }

    [Fact]
    public void Fast_rule_evaluation_matches_diagnostics_for_activation_removal_and_ongoing_conditions()
    {
        var manager = new DefaultContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        var evaluator = new MobaContinuousTagRuleEvaluator(effective);
        var subject = new TaggedContinuous(1, "Debuff.Slow");
        var requirements = ((TaggedConfig)subject.Config).TagRequirements;
        var superArmor = MobaGameplayTagCatalog.ToContainer(new[] { "State.SuperArmor" }).First();
        requirements.ActivationRequired = GameplayTagRequirements.Require(superArmor);
        requirements.OngoingRequired = GameplayTagRequirements.Require(superArmor);
        requirements.RemovalRequired = GameplayTagRequirements.Block(superArmor);

        Assert.Equal(evaluator.Evaluate(subject).Action, evaluator.EvaluateFast(subject).Action);
        Assert.Equal(MobaContinuousTagRuleAction.BlockActivate, evaluator.EvaluateFast(subject).Action);
        var source = new TaggedContinuous(1, "State.SuperArmor");
        Assert.True(manager.TryActivate(source));
        Assert.Equal(evaluator.Evaluate(subject).Action, evaluator.EvaluateFast(subject).Action);
        Assert.True(manager.TryActivate(subject));
        Assert.Equal(evaluator.Evaluate(subject).Action, evaluator.EvaluateFast(subject).Action);
        Assert.True(manager.TryEnd(source));
        Assert.Equal(MobaContinuousTagRuleAction.Remove, evaluator.EvaluateFast(subject).Action);
        Assert.Equal(evaluator.Evaluate(subject).Action, evaluator.EvaluateFast(subject).Action);
        requirements.RemovalRequired = new GameplayTagRequirements();
        Assert.Equal(MobaContinuousTagRuleAction.Pause, evaluator.EvaluateFast(subject).Action);
        Assert.Equal(evaluator.Evaluate(subject).Action, evaluator.EvaluateFast(subject).Action);
    }

    [Fact]
    public void Refreshing_active_continuous_tags_rebuilds_cache_and_reconciles_dependents()
    {
        using var manager = new MobaContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        var evaluator = new MobaContinuousTagRuleEvaluator(effective);
        using var rules = new MobaContinuousTagRuleService();
        rules.OnInit(new Resolver(manager, effective, evaluator));
        manager.OnInit(new Resolver(manager, effective, rules));
        var source = new TaggedContinuous(1, "State.SuperArmor");
        var dependent = new TaggedContinuous(1, "Debuff.Slow", ongoingRequiredTag: "State.SuperArmor");
        Assert.True(manager.TryActivate(source));
        Assert.True(manager.TryActivate(dependent));
        Assert.True(effective.GetEffectiveTags(1).HasTagExact(
            MobaGameplayTagCatalog.ToContainer(new[] { "State.SuperArmor" }).First()));

        ((TaggedConfig)source.Config).TagRequirements.ApplicationTags =
            MobaGameplayTagCatalog.ToContainer(new[] { "State.Silenced" });
        manager.RefreshTagRequirements(source);

        Assert.True(dependent.IsPaused);
        Assert.False(effective.GetEffectiveTags(1).HasTagExact(
            MobaGameplayTagCatalog.ToContainer(new[] { "State.SuperArmor" }).First()));
        Assert.True((effective.GetResultFlags(1) & MobaTagResultFlags.CannotCast) != 0);
    }

    [Fact]
    public void Replacing_active_grants_updates_cues_and_reactive_continuous_objects()
    {
        using var manager = new MobaContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        var evaluator = new MobaContinuousTagRuleEvaluator(effective);
        using var rules = new MobaContinuousTagRuleService();
        rules.OnInit(new Resolver(manager, effective, evaluator));
        manager.OnInit(new Resolver(manager, effective, rules));
        var phase = new MobaLogicWorldRunGateService();
        phase.SetInGame("grant replacement test");
        using var cues = new MobaPresentationCueSnapshotService(phase);
        using var reporter = new MobaTagPresentationCueReporter();
        reporter.OnInit(new Resolver(manager, effective, cues));

        var source = new TaggedContinuous(1, "State.SuperArmor");
        var dependent = new TaggedContinuous(1, "Debuff.Slow", ongoingRequiredTag: "State.SuperArmor");
        Assert.True(manager.TryActivate(source));
        Assert.True(manager.TryActivate(dependent));
        ((TaggedConfig)source.Config).TagRequirements.ApplicationTags =
            MobaGameplayTagCatalog.ToContainer(new[] { "State.Silenced" });
        manager.RefreshTagRequirements(source);

        Assert.True(dependent.IsPaused);
        Assert.True(effective.HasTag(1, MobaGameplayTagCatalog.ToContainer(new[] { "State.Silenced" }).First()));
        var changed = ReadCues(cues, 3);
        Assert.Contains(changed, cue => cue.ContextEventId == "tag.removed" && cue.StringParamValues[0] == "State.SuperArmor");
        Assert.Contains(changed, cue => cue.ContextEventId == "tag.added" && cue.StringParamValues[0] == "State.Silenced");

        ((TaggedConfig)source.Config).TagRequirements.ApplicationTags = new GameplayTagContainer();
        manager.RefreshTagRequirements(source);
        Assert.False(effective.HasTag(1, MobaGameplayTagCatalog.ToContainer(new[] { "State.Silenced" }).First()));
        Assert.Contains(ReadCues(cues, 4), cue => cue.ContextEventId == "tag.removed" && cue.StringParamValues[0] == "State.Silenced");

        ((TaggedConfig)source.Config).TagRequirements.ApplicationTags =
            MobaGameplayTagCatalog.ToContainer(new[] { "State.SuperArmor" });
        manager.RefreshTagRequirements(source);
        Assert.True(dependent.IsActive);
    }

    [Fact]
    public void Untagged_lifecycle_does_not_rebuild_an_existing_result_cache()
    {
        using var manager = new MobaContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        var evaluator = new MobaContinuousTagRuleEvaluator(effective);
        using var rules = new MobaContinuousTagRuleService();
        rules.OnInit(new Resolver(manager, effective, evaluator));
        Assert.True(manager.TryActivate(new TaggedContinuous(1, "State.Stunned")));
        var expected = effective.GetResultFlags(1);
        var before = effective.Metrics.CacheRebuilds;
        var untagged = new TaggedContinuous(1, null);
        Assert.True(manager.TryActivate(untagged));
        Assert.Equal(expected, effective.GetResultFlags(1));
        Assert.True(manager.TryPause(untagged));
        Assert.True(manager.TryResume(untagged));
        Assert.True(manager.TryEnd(untagged));
        Assert.Equal(expected, effective.GetResultFlags(1));
        Assert.Equal(before, effective.Metrics.CacheRebuilds);
    }

    [Fact]
    public void Removal_blocked_tag_alone_removes_when_that_tag_disappears()
    {
        var requirements = new ContinuousTagRequirements
        {
            RemovalRequired = GameplayTagRequirements.Block(
                MobaGameplayTagCatalog.ToContainer(new[] { "State.SuperArmor" }).First())
        };
        var protectedTags = MobaGameplayTagCatalog.ToContainer(new[] { "State.SuperArmor" });
        Assert.False(requirements.ShouldRemove(protectedTags));
        Assert.True(requirements.ShouldRemove(new GameplayTagContainer()));
    }

    [Fact]
    public void Published_execution_gate_uses_cached_results_and_restores_after_last_source_ends()
    {
        var workbook = new MobaTagWorkbookRuntime(new ConsoleTextAssetLoader());
        var template = new JObject { ["ExecutionBlockedFlagIds"] = new JArray(3) };
        workbook.RestoreTemplate(template);
        var mask = template.Value<ulong>("ExecutionBlockedResultMask");
        Assert.Equal((ulong)MobaTagResultFlags.CannotCast, mask);

        var manager = new DefaultContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        var action = new TaggedContinuous(1, "State.SuperArmor", mask);
        var first = new TaggedContinuous(1, "State.Silenced");
        var second = new TaggedContinuous(1, "State.Silenced");
        Assert.True(manager.TryActivate(action));
        Assert.True(effective.CanExecute(action));
        Assert.True(manager.TryActivate(first));
        Assert.True(manager.TryActivate(second));
        Assert.False(effective.CanExecute(action));
        Assert.True(manager.TryEnd(first));
        Assert.False(effective.CanExecute(action));
        Assert.True(manager.TryEnd(second));
        Assert.True(effective.CanExecute(action));
        Assert.True(manager.TryPause(action));
        Assert.False(effective.CanExecute(action));
    }

    [Fact]
    public void Tag_cues_follow_effective_tag_changes_across_sources_and_pause()
    {
        var manager = new DefaultContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        var phase = new MobaLogicWorldRunGateService();
        phase.SetInGame("tag cue test");
        using var cues = new MobaPresentationCueSnapshotService(phase);
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        using var reporter = new MobaTagPresentationCueReporter();
        reporter.OnInit(new Resolver(manager, effective, cues));

        var first = new TaggedContinuous(1, "State.Stunned");
        var second = new TaggedContinuous(1, "State.Stunned");
        Assert.True(manager.TryActivate(first));
        var added = Assert.Single(ReadCues(cues, 1));
        Assert.Equal("tag.added", added.ContextEventId);
        Assert.Equal("tag:1:1020", added.InstanceKey);
        Assert.Equal(1020f, added.NumericParamValues[0]);
        Assert.Equal(1, cues.ActiveCueCount);
        Assert.True(manager.TryActivate(second));
        Assert.False(cues.TryGetSnapshot(new FrameIndex(2), out _));
        Assert.True(manager.TryPause(first));
        Assert.False(cues.TryGetSnapshot(new FrameIndex(3), out _));
        Assert.True(manager.TryEnd(second));
        Assert.Equal("tag.removed", Assert.Single(ReadCues(cues, 4)).ContextEventId);
        Assert.Equal(0, cues.ActiveCueCount);
        Assert.True(manager.TryResume(first));
        Assert.Equal("tag.added", Assert.Single(ReadCues(cues, 5)).ContextEventId);
        Assert.True(manager.TryEnd(first));
        Assert.Equal("tag.removed", Assert.Single(ReadCues(cues, 6)).ContextEventId);
    }

    [Fact]
    public void Reporter_initialization_snapshots_existing_active_tags_without_replaying_additions()
    {
        var manager = new DefaultContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        var phase = new MobaLogicWorldRunGateService();
        phase.SetInGame("tag cue initialization test");
        using var cues = new MobaPresentationCueSnapshotService(phase);
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        var active = new TaggedContinuous(1, "State.Stunned");
        Assert.True(manager.TryActivate(active));
        using var reporter = new MobaTagPresentationCueReporter();
        reporter.OnInit(new Resolver(manager, effective, cues));
        Assert.False(cues.TryGetSnapshot(new FrameIndex(1), out _));
        Assert.True(manager.TryEnd(active));
        Assert.Equal("tag.removed", Assert.Single(ReadCues(cues, 2)).ContextEventId);
    }

    [Fact]
    public void Directly_unregistering_an_active_grant_removes_its_tag_and_cue_once()
    {
        var manager = new DefaultContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        var phase = new MobaLogicWorldRunGateService();
        phase.SetInGame("tag direct unregister test");
        using var cues = new MobaPresentationCueSnapshotService(phase);
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        using var reporter = new MobaTagPresentationCueReporter();
        reporter.OnInit(new Resolver(manager, effective, cues));

        var active = new TaggedContinuous(1, "State.Stunned");
        Assert.True(manager.TryActivate(active));
        Assert.Equal("tag.added", Assert.Single(ReadCues(cues, 1)).ContextEventId);
        manager.Unregister(active);
        Assert.Equal("tag.removed", Assert.Single(ReadCues(cues, 2)).ContextEventId);
        Assert.Equal(MobaTagResultFlags.None, effective.GetResultFlags(1));
        Assert.Equal(0, cues.ActiveCueCount);
        manager.Unregister(active);
        Assert.False(cues.TryGetSnapshot(new FrameIndex(3), out _));
    }

    [Fact]
    public void Brain_tag_query_reads_continuous_projection()
    {
        var manager = new DefaultContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        var adapter = new AbilityKit.Demo.Moba.Services.Behavior.MobaBrainBuffManager(null, () => effective);
        var active = new TaggedContinuous(1, "State.Stunned");
        Assert.False(adapter.HasTag(1, "State.Stunned"));
        Assert.True(manager.TryActivate(active));
        Assert.True(adapter.HasTag(1, "State.Stunned"));
        Assert.False(adapter.HasTag(1, "State.DoesNotExist"));
        Assert.True(manager.TryPause(active));
        Assert.False(adapter.HasTag(1, "State.Stunned"));
        Assert.True(manager.TryResume(active));
        Assert.True(adapter.HasTag(1, "State.Stunned"));
        Assert.True(manager.TryEnd(active));
        Assert.False(adapter.HasTag(1, "State.Stunned"));
    }

    private static MobaPresentationCueSnapshotEntry[] ReadCues(MobaPresentationCueSnapshotService cues, int frame)
    {
        Assert.True(cues.TryGetSnapshot(new FrameIndex(frame), out var snapshot));
        return MobaPresentationCueSnapshotCodec.Deserialize(snapshot.Payload);
    }

    [Fact]
    public void Missing_workbook_loader_fails_initialization()
    {
        using var effective = new MobaEffectiveTagQueryService();
        var error = Assert.Throws<InvalidOperationException>(() => effective.OnInit(new Resolver()));
        Assert.Contains("ITextAssetLoader", error.Message);
        Assert.Throws<InvalidOperationException>(() => effective.GetResultFlags(1));
    }

    [Fact]
    public void Combat_gates_read_result_bits_and_distinguish_attacks_from_casts()
    {
        var contexts = new Contexts();
        using var registry = new MobaActorRegistry();
        using var entities = new MobaEntityManager(null);
        using var index = new ActorIdIndex(contexts);
        var actor = contexts.actor.CreateEntity();
        actor.AddActorId(1);
        registry.Register(1, actor);
        var manager = new DefaultContinuousManager();
        using var effective = new MobaEffectiveTagQueryService();
        effective.OnInit(new Resolver(manager, new ConsoleTextAssetLoader()));
        using var combat = new MobaCombatRulesService(new MobaActorLookupService(index, registry, entities, contexts), effective);
        try
        {
            Assert.False(combat.IsUntargetable(0));
            var silence = new TaggedContinuous(1, "State.Silenced");
            Assert.True(manager.TryActivate(silence));
            Assert.False(combat.CanCastSkill(1, (ulong)MobaTagResultFlags.CannotCast).Passed);
            Assert.True(combat.CanCastSkill(1, (ulong)MobaTagResultFlags.CannotAttack).Passed);
            Assert.True(combat.CanMove(1));
            Assert.True(manager.TryEnd(silence));

            var stunned = new TaggedContinuous(1, "State.Stunned");
            Assert.True(manager.TryActivate(stunned));
            Assert.False(combat.CanCastSkill(1, (ulong)MobaTagResultFlags.CannotCast).Passed);
            Assert.False(combat.CanCastSkill(1, (ulong)MobaTagResultFlags.CannotAttack).Passed);
            Assert.False(combat.CanMove(1));
            Assert.True(manager.TryEnd(stunned));

            var armor = new TaggedContinuous(1, "State.SuperArmor");
            Assert.True(manager.TryActivate(armor));
            Assert.False(combat.CanBeControlled(1));
            Assert.True(combat.CanMove(1));
            Assert.True(combat.CanCastSkill(1, (ulong)MobaTagResultFlags.CannotCast).Passed);
            Assert.False(combat.CanCastSkill(1, (ulong)MobaTagResultFlags.ControlImmune).Passed);
            Assert.True(manager.TryEnd(armor));
            Assert.True(combat.CanBeControlled(1));

            var untargetable = new TaggedContinuous(1, "State.Untargetable");
            Assert.True(manager.TryActivate(untargetable));
            Assert.Equal(MobaCombatRuleFailure.Untargetable, combat.CanBeSearchedTarget(2, 1).Failure);
            Assert.True(combat.CanReceiveDamage(2, 1).Passed);
            Assert.True(manager.TryEnd(untargetable));

            var invulnerable = new TaggedContinuous(1, "State.Invulnerable");
            Assert.True(manager.TryActivate(invulnerable));
            Assert.False(combat.CanReceiveDamage(2, 1).Passed);
            Assert.True(manager.TryEnd(invulnerable));
            Assert.True(combat.CanReceiveDamage(2, 1).Passed);
        }
        finally
        {
            contexts.actor.DestroyAllEntities();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Real_world_silence_and_stun_pause_running_skills(bool silenceBeforeCast)
    {
        var start = BattleStartConfig.CreateDefault();
        var casterConfig = start.Players[0];
        var targetConfig = start.Players[3];
        casterConfig.EnableBrainOnSpawn = false;
        targetConfig.EnableBrainOnSpawn = false;
        targetConfig.PositionX = 0;
        targetConfig.PositionZ = 2;
        start.Players = new() { casterConfig, targetConfig };
        using var bootstrapper = new ConsoleBattleBootstrapper(start);
        bootstrapper.Initialize();
        bootstrapper.Start();
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
        bootstrapper.SetupBattle();
        for (var i = 0; i < 10; i++) bootstrapper.Tick();
        var services = bootstrapper.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var caster = registry.Entries.First(entry => entry.Value.hasTeam && (int)entry.Value.team.Value == 1);
        var continuous = services.Resolve<IContinuousManager>();
        var skills = services.Resolve<SkillCastCoordinator>();
        skills.AllowParallel = true;
        var silence = new TaggedContinuous(caster.Key, "State.Silenced");
        var stunned = new TaggedContinuous(caster.Key, "State.Stunned");
        if (silenceBeforeCast) Assert.True(continuous.TryActivate(silence));
        var active = skills.TryCastSkill(caster.Key, 10010201, slot: 2);
        Assert.Equal(!silenceBeforeCast, active.Success);
        var attack = skills.TryCastSkill(caster.Key, 10010001);
        Assert.True(attack.Success, attack.FailReason);
        Assert.True(skills.TryGetRunningByInstanceId(caster.Key, attack.RuntimeHandle.RootContextId, out _));
        Assert.Contains(continuous.GetOwnerActiveContinuous(caster.Key),
            item => item is SkillPipelineContinuousRuntime pipeline && pipeline.SkillId == 10010001);
        Assert.True(skills.TryGetRunningByInstanceId(caster.Key, attack.RuntimeHandle.RootContextId, out var before));
        if (!silenceBeforeCast) Assert.True(continuous.TryActivate(silence));
        bootstrapper.Tick();
        Assert.True(skills.TryGetRunningByInstanceId(caster.Key, attack.RuntimeHandle.RootContextId, out var silenced));
        Assert.True(silenced.ElapsedMs > before.ElapsedMs);
        Assert.Equal(silenceBeforeCast, !skills.TryGetRunningBySlot(caster.Key, 2, out _));
        Assert.True(continuous.TryActivate(stunned));
        bootstrapper.Tick();
        Assert.True(skills.TryGetRunningByInstanceId(caster.Key, attack.RuntimeHandle.RootContextId, out var blocked));
        Assert.Equal(silenced.ElapsedMs, blocked.ElapsedMs);
        Assert.False(skills.TryCastSkill(caster.Key, 10010001).Success);
        Assert.True(continuous.TryEnd(stunned));
        Assert.True(continuous.TryEnd(silence));
        bootstrapper.Tick();
        Assert.True(skills.TryGetRunningByInstanceId(caster.Key, attack.RuntimeHandle.RootContextId, out var resumed));
        Assert.True(resumed.ElapsedMs > blocked.ElapsedMs);
    }

    [Fact]
    public void LianPo_pipeline_execution_waits_for_configured_result_flag_to_clear()
    {
        var start = BattleStartConfig.CreateDefault();
        var casterConfig = start.Players[0];
        var targetConfig = start.Players[3];
        casterConfig.EnableBrainOnSpawn = false;
        targetConfig.EnableBrainOnSpawn = false;
        targetConfig.PositionX = 0;
        targetConfig.PositionZ = 2;
        start.Players = new() { casterConfig, targetConfig };
        using var bootstrapper = new ConsoleBattleBootstrapper(start);
        bootstrapper.Initialize();
        bootstrapper.Start();
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
        bootstrapper.SetupBattle();
        for (var i = 0; i < 10; i++) bootstrapper.Tick();

        var services = bootstrapper.RuntimeServices!;
        var actorId = services.Resolve<MobaActorRegistry>().Entries.First(entry => entry.Value.hasTeam && (int)entry.Value.team.Value == 1).Key;
        var continuous = services.Resolve<IContinuousManager>();
        var skills = services.Resolve<SkillCastCoordinator>();
        var cast = skills.TryCastSkill(actorId, 10010101, slot: 1);
        Assert.True(cast.Success, cast.FailReason);
        Assert.True(skills.TryGetRunningBySlot(actorId, 1, out var before));

        var probe = new TaggedContinuous(actorId, "Debuff.Slow", (ulong)MobaTagResultFlags.CannotCast);
        Assert.True(continuous.TryActivate(probe));
        var manager = services.Resolve<MobaContinuousManager>();
        manager.Tick(0.01f);
        Assert.Equal(1, probe.TickCount);

        var silence = new TaggedContinuous(actorId, "State.Silenced");
        Assert.True(continuous.TryActivate(silence));
        manager.Tick(0.01f);
        Assert.Equal(1, probe.BlockedCount);
        Assert.Equal(1, probe.TickCount);
        skills.Step(actorId);
        Assert.True(skills.TryGetRunningBySlot(actorId, 1, out var blocked));
        Assert.Equal(before.ElapsedMs, blocked.ElapsedMs);
        bootstrapper.Tick();
        Assert.True(skills.TryGetRunningBySlot(actorId, 1, out var worldBlocked));
        Assert.Equal(blocked.ElapsedMs, worldBlocked.ElapsedMs);

        Assert.True(continuous.TryEnd(silence));
        manager.Tick(0.01f);
        Assert.Equal(2, probe.TickCount);
        skills.Step(actorId);
        Assert.True(skills.TryGetRunningBySlot(actorId, 1, out var resumed));
        Assert.True(resumed.ElapsedMs > blocked.ElapsedMs);
    }

    private sealed class TaggedContinuous : MobaContinuousRuntimeBase,
        IMobaTickableContinuous, IMobaContinuousExecutionBlockedHandler
    {
        private readonly TaggedConfig _config;
        public TaggedContinuous(long ownerId, string tagName, ulong executionBlockedMask = 0, string ongoingRequiredTag = null) =>
            _config = new TaggedConfig(ownerId, tagName, executionBlockedMask, ongoingRequiredTag);
        public override IContinuousConfig Config => _config;
        public int TickCount { get; private set; }
        public int BlockedCount { get; private set; }
        public void TickManaged(float deltaTimeSeconds) => TickCount++;
        public void OnExecutionBlocked() => BlockedCount++;
    }

    private sealed class CallbackTagEvaluator : IMobaContinuousTagRuleEvaluator
    {
        public Action<IContinuous> OnEvaluate { get; set; }
        public int ActorTwoEvaluations { get; private set; }

        public MobaContinuousTagRuleResult Evaluate(IContinuous continuous)
        {
            if (continuous.Config.OwnerId == 2) ActorTwoEvaluations++;
            OnEvaluate?.Invoke(continuous);
            return new MobaContinuousTagRuleResult((int)continuous.Config.OwnerId,
                MobaContinuousTagRuleAction.KeepActive, true, "pass", null, null);
        }

        public MobaContinuousTagRuleResult EvaluateFast(IContinuous continuous) => Evaluate(continuous);

        public bool CanActivate(IContinuous continuous, out string reason)
        {
            reason = null;
            return true;
        }
    }

    private sealed class TamperedTagAssets : ITextAssetLoader
    {
        private readonly ConsoleTextAssetLoader _inner = new ConsoleTextAssetLoader();

        public bool TryLoadText(string path, out string text) => _inner.TryLoadText(path, out text);

        public bool TryLoadBytes(string path, out byte[] bytes)
        {
            if (!_inner.TryLoadBytes(path, out bytes)) return false;
            if (path.EndsWith("tag_rule_nodes.bytes", StringComparison.Ordinal))
            {
                bytes = (byte[])bytes.Clone();
                bytes[0] ^= 1;
            }
            return true;
        }
    }

    private sealed class TaggedConfig : IContinuousConfig, IMobaContinuousTagConfig
    {
        public TaggedConfig(long ownerId, string tagName, ulong executionBlockedMask, string ongoingRequiredTag)
        {
            OwnerId = ownerId;
            TagRequirements = new ContinuousTagRequirements
            {
                ApplicationTags = string.IsNullOrEmpty(tagName)
                    ? new GameplayTagContainer()
                    : MobaGameplayTagCatalog.ToContainer(new[] { tagName }),
                OngoingRequired = string.IsNullOrEmpty(ongoingRequiredTag)
                    ? new GameplayTagRequirements()
                    : new GameplayTagRequirements(MobaGameplayTagCatalog.ToContainer(new[] { ongoingRequiredTag }), null),
                ExecutionBlockedResultMask = executionBlockedMask,
            };
            Id = $"test.{ownerId}.{tagName}";
        }
        public string Id { get; }
        public long OwnerId { get; }
        public bool CanBeInterrupted => true;
        public ContinuousTagRequirements TagRequirements { get; }
    }

    private sealed class Resolver : IWorldResolver
    {
        private readonly object[] _services;
        private readonly MobaContinuousModifierCaptureStore _captureStore = new();
        public Resolver(params object[] services) => _services = services;
        public object Resolve(Type serviceType) => serviceType == typeof(MobaContinuousModifierCaptureStore)
            ? _captureStore : Array.Find(_services, serviceType.IsInstanceOfType);
        public T Resolve<T>() => TryResolve<T>(out var result) ? result : default!;
        public bool TryResolve(Type serviceType, out object instance)
        {
            instance = Resolve(serviceType);
            return instance != null;
        }
        public bool TryResolve<T>(out T instance)
        {
            var value = Resolve(typeof(T));
            instance = value is T resolved ? resolved : default!;
            return value is T;
        }
    }
}
