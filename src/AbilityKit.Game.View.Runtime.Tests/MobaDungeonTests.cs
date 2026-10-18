using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Dungeons;
using AbilityKit.Game.View.Flow;
using Xunit;

namespace AbilityKit.Game.View.Runtime.Tests;

public sealed class MobaDungeonDefTests
{
    private static MobaDungeonDef Def(
        int id = 1, string profile = "moba.standard", string handler = "team_deathmatch",
        string outcome = "team_deathmatch", string[] modes = null, int min = 1, int max = 0) =>
        new MobaDungeonDef(id, "Dungeon", 1, profile, 0, min, max, modes,
            "none", handler, outcome, Array.Empty<string>());

    [Fact]
    public void Rejects_MissingFlowProfile()
    {
        Assert.Throws<ArgumentException>(() => Def(profile: " "));
    }

    [Fact]
    public void Rejects_MissingGameplayHandlerAndOutcomeRule()
    {
        Assert.Throws<ArgumentException>(() => Def(handler: null));
        Assert.Throws<ArgumentException>(() => Def(outcome: ""));
    }

    [Fact]
    public void EmptyAllowedHostModes_AllowsAnyHostMode()
    {
        var dungeon = Def(modes: Array.Empty<string>());
        Assert.True(dungeon.AllowsHostMode("Local"));
        Assert.True(dungeon.AllowsHostMode("GatewayRemote"));
    }

    [Fact]
    public void DeclaredHostModes_RejectOthers()
    {
        var dungeon = Def(modes: new[] { "Local" });
        Assert.True(dungeon.AllowsHostMode("Local"));
        Assert.False(dungeon.AllowsHostMode("GatewayRemote"));
        Assert.False(dungeon.AllowsHostMode(null));
    }

    [Fact]
    public void ZeroMaxPlayerCount_MeansUnbounded()
    {
        var dungeon = Def(min: 2, max: 0);
        Assert.False(dungeon.AcceptsPlayerCount(1));
        Assert.True(dungeon.AcceptsPlayerCount(2));
        Assert.True(dungeon.AcceptsPlayerCount(99));
    }

    [Fact]
    public void PlayerCount_RespectsMinimumAndMaximum()
    {
        var dungeon = Def(min: 2, max: 4);
        Assert.False(dungeon.AcceptsPlayerCount(1));
        Assert.True(dungeon.AcceptsPlayerCount(3));
        Assert.False(dungeon.AcceptsPlayerCount(5));
    }
}

public sealed class MobaDungeonRegistryTests
{
    [Fact]
    public void Standard_RegistersDeathmatchDefaults()
    {
        var registry = MobaDungeonRegistry.CreateStandard();

        Assert.True(registry.ContainsGameplayHandler("team_deathmatch"));
        Assert.True(registry.ContainsOutcomeRule("team_deathmatch"));
        Assert.True(registry.ContainsSceneProvider("none"));
        Assert.True(registry.ContainsPortGroup("deathmatch"));
        // score 端口组已移除：积分由玩法配置下发、通用协议处理，客户端不预声明。
        Assert.False(registry.ContainsPortGroup("score"));
    }

    [Fact]
    public void Rejects_DuplicateRegistrations()
    {
        var registry = MobaDungeonRegistry.CreateStandard();

        Assert.Throws<InvalidOperationException>(
            () => registry.RegisterGameplayHandler(new TeamDeathmatchGameplayHandler()));
        Assert.Throws<InvalidOperationException>(
            () => registry.RegisterOutcomeRule(new TeamDeathmatchOutcomeRule()));
        Assert.Throws<InvalidOperationException>(
            () => registry.RegisterSceneProvider(new NullBattleSceneProvider()));
    }

    [Fact]
    public void Get_UnknownId_ThrowsWithId()
    {
        var registry = MobaDungeonRegistry.CreateStandard();

        Assert.Throws<InvalidOperationException>(() => registry.GetGameplayHandler("nope"));
        Assert.Throws<InvalidOperationException>(() => registry.GetSceneProvider("nope"));
        Assert.Throws<InvalidOperationException>(() => registry.GetOutcomeRule("nope"));
    }
}

public sealed class MobaDungeonValidatorTests
{
    private static MobaDungeonDef Def(
        int id = 1, int gameplay = 1, string profile = "moba.standard",
        string handler = "team_deathmatch", string outcome = "team_deathmatch",
        string scene = "none", string[] ports = null, string[] modes = null, int min = 1) =>
        new MobaDungeonDef(id, "Dungeon", gameplay, profile, 0, min, 0, modes,
            scene, handler, outcome, ports ?? new[] { "deathmatch" });

    private static MobaDungeonValidator Validator(
        Func<string, bool> profile = null, Func<int, bool> gameplay = null,
        Func<int, bool> map = null) =>
        new MobaDungeonValidator(MobaDungeonRegistry.CreateStandard(), profile, gameplay, map);

    [Fact]
    public void StandardDungeon_PassesValidation()
    {
        var errors = Validator().Validate(Def(), "Local", 2);
        Assert.Empty(errors);
    }

    [Fact]
    public void Reports_UnknownGameplay()
    {
        var errors = Validator(gameplay: _ => false, map: _ => false)
            .Validate(Def(), "Local", 2);

        // 地图已从副本表移除，改由场景装配器按玩法解析，因此这里只剩玩法一条错误。
        Assert.Single(errors);
        Assert.Contains(errors, e => e.Contains("玩法"));
    }

    [Fact]
    public void Reports_UnknownFlowProfile()
    {
        var errors = Validator(profile: _ => false).Validate(Def(), "Local", 2);
        Assert.Contains(errors, e => e.Contains("Profile"));
    }

    [Fact]
    public void Reports_UnregisteredExtensions()
    {
        var errors = Validator().Validate(Def(handler: "ghost", outcome: "phantom", scene: "nonesuch"), "Local", 2);

        Assert.Contains(errors, e => e.Contains("ghost"));
        Assert.Contains(errors, e => e.Contains("phantom"));
        Assert.Contains(errors, e => e.Contains("nonesuch"));
    }

    [Fact]
    public void Reports_UndeclaredPortGroup()
    {
        var errors = Validator().Validate(Def(ports: new[] { "undeclared" }), "Local", 2);
        Assert.Contains(errors, e => e.Contains("undeclared"));
    }

    [Fact]
    public void Reports_PortGroupNotDeclaredByHandler()
    {
        // deathmatch 处理器只依赖 deathmatch，不认识 wave（PvE 波次）端口组。
        var errors = Validator().Validate(Def(ports: new[] { "deathmatch", "wave" }), "Local", 2);
        Assert.Contains(errors, e => e.Contains("wave") && e.Contains("未声明该依赖"));
    }

    [Fact]
    public void EmptySceneProviderId_IsAllowed()
    {
        Assert.Empty(Validator().Validate(Def(scene: ""), "Local", 2));
    }

    [Fact]
    public void Reports_HostModeAndPlayerCountMismatch()
    {
        var errors = Validator().Validate(Def(modes: new[] { "Local" }, min: 2), "GatewayRemote", 1);

        Assert.Contains(errors, e => e.Contains("宿主模式"));
        Assert.Contains(errors, e => e.Contains("玩家"));
    }

    [Fact]
    public void NullDungeon_IsReportedNotThrown()
    {
        Assert.Single(Validator().Validate(null, "Local", 2));
    }

    [Fact]
    public void ValidateOrThrow_AggregatesAllReasons()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Validator(profile: _ => false).ValidateOrThrow(Def(), "Local", 2));

        Assert.Contains("Profile", error.Message);
    }

    [Fact]
    public void ValidateOrThrow_PassesForValidDungeon()
    {
        Assert.True(Validator().ValidateOrThrow(Def(), "Local", 2));
    }
}

public sealed class MobaDungeonCatalogTests
{
    private static MobaDungeonDef Def(int id, string name, string[] modes = null, int min = 1, int max = 0) =>
        new MobaDungeonDef(id, name, 1, "moba.standard", 0, min, max, modes,
            "none", "team_deathmatch", "team_deathmatch", new[] { "deathmatch" });

    [Fact]
    public void Rejects_DuplicateIds()
    {
        Assert.Throws<InvalidOperationException>(() => new MobaDungeonCatalog(
            new[] { Def(1, "A"), Def(1, "B") }));
    }

    [Fact]
    public void Rejects_EmptyCatalog()
    {
        Assert.Throws<InvalidOperationException>(() => new MobaDungeonCatalog(Array.Empty<MobaDungeonDef>()));
    }

    [Fact]
    public void Get_UnknownId_Throws()
    {
        var catalog = new MobaDungeonCatalog(new[] { Def(1, "A") });

        Assert.Throws<InvalidOperationException>(() => catalog.Get(2));
        Assert.False(catalog.TryGet(2, out _));
        Assert.True(catalog.TryGet(1, out var dungeon));
        Assert.Equal("A", dungeon.Name);
    }

    [Fact]
    public void Select_FiltersByHostModeAndPlayerCount()
    {
        var catalog = new MobaDungeonCatalog(new[]
        {
            Def(1, "open", modes: null, min: 1),
            Def(2, "local-only", modes: new[] { "Local" }, min: 2),
        });

        Assert.Equal(2, catalog.Select("Local", 2).Count);
        var remote = catalog.Select("GatewayRemote", 2);
        Assert.Single(remote);
        Assert.Equal(1, remote[0].Id);
    }
}

public sealed class BuiltInDungeonExtensionTests
{
    [Fact]
    public void DeathmatchHandler_DeclaresOnlyDeathmatchPortGroup()
    {
        var handler = new TeamDeathmatchGameplayHandler();

        Assert.True(handler.RequiresPortGroup("deathmatch"));
        // 积分不作为客户端端口组声明，走玩法配置 + 通用协议下发。
        Assert.False(handler.RequiresPortGroup("score"));
        // PvE 才需要的波次端口组不属于 deathmatch。
        Assert.False(handler.RequiresPortGroup("wave"));
    }

    [Fact]
    public void NullSceneProvider_AssemblesReleasableHandleWithMapId()
    {
        var dungeon = new MobaDungeonDef(1, "D", 1, "moba.standard", 0, 1, 0, null,
            "none", "team_deathmatch", "team_deathmatch", Array.Empty<string>());
        var handle = new NullBattleSceneProvider().Assemble(new DungeonSceneContext(dungeon, 2, 77));

        Assert.True(handle.IsLoaded);
        Assert.Equal(77, handle.BattleMapId);
        handle.Dispose();
        Assert.False(handle.IsLoaded);
        handle.Dispose();
    }

    [Fact]
    public void BuiltInOutcomeRule_DefersToAuthoritativeLogic()
    {
        var rule = new TeamDeathmatchOutcomeRule();
        var dungeon = new MobaDungeonDef(1, "D", 1, "moba.standard", 0, 1, 0, null,
            "none", "team_deathmatch", "team_deathmatch", Array.Empty<string>());

        // 平局且未超时时不判定胜方。
        Assert.False(rule.TryEvaluate(
            new BattleOutcomeEvaluationContext(dungeon, 1, 10, 1000, 1, 3d, 3d), out var winner));
        Assert.Equal(0, winner);
    }

    [Fact]
    public void BuiltInOutcomeRule_DecidesByScore()
    {
        var rule = new TeamDeathmatchOutcomeRule();
        var dungeon = new MobaDungeonDef(1, "D", 1, "moba.standard", 0, 1, 0, null,
            "none", "team_deathmatch", "team_deathmatch", Array.Empty<string>());

        Assert.True(rule.TryEvaluate(
            new BattleOutcomeEvaluationContext(dungeon, 1, 10, 1000, 1, 5d, 2d), out var winner));
        Assert.Equal(1, winner);
        Assert.True(rule.TryEvaluate(
            new BattleOutcomeEvaluationContext(dungeon, 1, 10, 1000, 1, 2d, 5d), out var other));
        Assert.Equal(2, other);
    }

    [Fact]
    public void BuiltInOutcomeRule_TreatsTimeoutAsDraw()
    {
        var rule = new TeamDeathmatchOutcomeRule();
        var dungeon = new MobaDungeonDef(1, "D", 1, "moba.standard", 60000, 1, 0, null,
            "none", "team_deathmatch", "team_deathmatch", Array.Empty<string>());

        // 超时按平局处理，不臆造胜方。
        Assert.False(rule.TryEvaluate(
            new BattleOutcomeEvaluationContext(dungeon, 1, 10, 60000, 1, 2d, 5d), out var other));
        Assert.Equal(0, other);
    }

    [Fact]
    public void TriggersOnlyHandler_DeclaresNoPortGroupDependency()
    {
        // 「玩法由触发器驱动」的装配实现不提供玩法能力，因此不声明任何端口组。
        var handler = new TriggersOnlyGameplayHandler();

        Assert.False(handler.RequiresPortGroup("deathmatch"));
        Assert.False(handler.RequiresPortGroup("score"));
    }

    [Fact]
    public void TriggersAuthoritativeRule_NeverDecidesOnItsOwn()
    {
        var rule = new TriggersAuthoritativeOutcomeRule();
        var dungeon = new MobaDungeonDef(1, "D", 1, "moba.standard", 0, 1, 0, null,
            "none", "triggers_only", "triggers_authoritative", Array.Empty<string>());

        // 即使比分悬殊也不判定：胜负只由逻辑层 trigger 产出。
        Assert.False(rule.TryEvaluate(
            new BattleOutcomeEvaluationContext(dungeon, 1, 10, 1000, 1, 9d, 1d), out var winner));
        Assert.Equal(0, winner);
    }

    [Fact]
    public void StandardRegistry_ExposesBothAssemblyImplementations()
    {
        var registry = MobaDungeonRegistry.CreateStandard();

        // 扩展点可插拔：同一注册中心能同时发现两套装配实现。
        Assert.True(registry.ContainsGameplayHandler("team_deathmatch"));
        Assert.True(registry.ContainsGameplayHandler("triggers_only"));
        Assert.True(registry.ContainsOutcomeRule("team_deathmatch"));
        Assert.True(registry.ContainsOutcomeRule("triggers_authoritative"));
    }

    [Fact]
    public void TriggerDrivenDungeon_PassesDryRunValidation()
    {
        var dungeon = new MobaDungeonDef(4, "Trigger-Driven Scenario", 1, "moba.quiet",
            0, 1, 10, new[] { "Local" }, "none",
            "triggers_only", "triggers_authoritative", Array.Empty<string>());

        // 不声明端口组时不会被任何 feature 裁剪掉，且扩展点全部命中。
        var validator = new MobaDungeonValidator(MobaDungeonRegistry.CreateStandard());
        Assert.Empty(validator.Validate(dungeon, "Local", 1));
    }

    [Fact]
    public void OutcomeState_FallsBackToRuleOnlyWhenAuthoritativeResultIsMissing()
    {
        var dungeon = new MobaDungeonDef(1, "D", 1, "moba.standard", 0, 1, 0, null,
            "none", "team_deathmatch", "team_deathmatch", Array.Empty<string>());
        var state = new BattleOutcomeState();
        state.BindRules(dungeon, new IBattleOutcomeRule[] { new TeamDeathmatchOutcomeRule() });

        // 未收到任何快照时兜底判定不了。
        Assert.False(state.TryEvaluateRule(out _));

        state.Apply(new GameplayMatchData(1, GameplayClockPhase.Running, 7d, 3d, 0, 0,
            string.Empty, "m1", 5));
        Assert.False(state.HasAuthoritativeResult);
        Assert.True(state.TryEvaluateRule(out var winner));
        Assert.Equal(1, winner);

        // 权威结果到达后不再由规则判定，避免与逻辑层分歧。
        state.Apply(new GameplayMatchData(1, GameplayClockPhase.Ended, 1d, 9d, 2, 10,
            "team2_wins", "m1", 10));
        Assert.True(state.HasAuthoritativeResult);
        Assert.False(state.TryEvaluateRule(out _));
    }

    [Fact]
    public void OutcomeState_Reset_ClearsBoundRulesAndScores()
    {
        var dungeon = new MobaDungeonDef(1, "D", 1, "moba.standard", 0, 1, 0, null,
            "none", "team_deathmatch", "team_deathmatch", Array.Empty<string>());
        var state = new BattleOutcomeState();
        state.BindRules(dungeon, new IBattleOutcomeRule[] { new TeamDeathmatchOutcomeRule() });
        state.Apply(new GameplayMatchData(1, GameplayClockPhase.Running, 7d, 3d, 0, 0,
            string.Empty, "m1", 5));

        state.Reset();

        Assert.Equal(0d, state.Team1Score);
        Assert.Equal(0d, state.Team2Score);
        Assert.False(state.TryEvaluateRule(out _));
    }
}

public sealed class MobaDungeonRandomSourceTests
{
    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var first = new MobaDungeonRandomSource(1234);
        var second = new MobaDungeonRandomSource(1234);

        for (var i = 0; i < 32; i++)
 {
            Assert.Equal(first.NextInt(0, 100, i, "wave"), second.NextInt(0, 100, i, "wave"));
        }
        Assert.Equal(first.RawValues, second.RawValues);
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentSequence()
    {
        var first = new MobaDungeonRandomSource(1);
        var other = new MobaDungeonRandomSource(2);

        for (var i = 0; i < 16; i++)
        {
            first.NextInt(0, 1000, i, "wave");
            other.NextInt(0, 1000, i, "wave");
        }

        Assert.NotEqual(first.RawValues, other.RawValues);
    }

    [Fact]
    public void Decisions_RecordFrameAndChannel()
    {
        var random = new MobaDungeonRandomSource(7);
        random.NextInt(0, 10, 42, "wave");
        random.NextInt(0, 10, 43, "boss");

        Assert.Equal(2, random.Decisions.Count);
        Assert.Equal(42, random.Decisions[0].Frame);
        Assert.Equal("wave", random.Decisions[0].Channel);
        Assert.Equal(0, random.Decisions[0].Ordinal);
        Assert.Equal(43, random.Decisions[1].Frame);
        Assert.Equal("boss", random.Decisions[1].Channel);
        Assert.Equal(1, random.Decisions[1].Ordinal);
    }

    [Fact]
    public void TryVerify_AcceptsRecordedRawValuesAndRejectsDivergence()
    {
        var random = new MobaDungeonRandomSource(99);
        random.NextInt(0, 50, 1, "spawn");
        var recorded = random.RawValues;

        Assert.True(random.TryVerify(0, recorded[0], out _));
        Assert.False(random.TryVerify(0, recorded[0] + 1u, out var error));
        Assert.Contains("diverged", error);
        Assert.False(random.TryVerify(5, 0u, out var rangeError));
        Assert.Contains("out of range", rangeError);
    }

    [Fact]
    public void NextInt_StaysInRange()
    {
        var random = new MobaDungeonRandomSource(2024);
        for (var i = 0; i < 200; i++)
        {
            var value = random.NextInt(5, 9, i, "wave");
            Assert.InRange(value, 5, 8);
        }
    }

    [Fact]
    public void NextChance_BoundsDoNotConsumeRandomness()
    {
        var random = new MobaDungeonRandomSource(3);
        Assert.False(random.NextChance(0d, 0, "wave"));
        Assert.True(random.NextChance(1d, 0, "wave"));
        // 概率为 0 或 1 时不应产生决策记录。
        Assert.Empty(random.Decisions);
    }

    [Fact]
    public void NextInt_RejectsEmptyRange()
    {
        var random = new MobaDungeonRandomSource(5);
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(3, 3, 0, "wave"));
    }

    [Fact]
    public void DungeonRuntimeState_ClearsRandomSourceOnDispose()
    {
        using var host = new BattleWorldScopeHost();
        host.BeginBattle();
        var state = host.Resolve<MobaDungeonRuntimeState>();
        state.Random = new MobaDungeonRandomSource(11);

        host.EndBattle();

        host.BeginBattle();
        Assert.Null(host.Resolve<MobaDungeonRuntimeState>().Random);
    }
}

public sealed class MobaDungeonPortGroupTests
{
    private static MobaFeatureFactoryRegistry Registries()
    {
        var registry = new MobaFeatureFactoryRegistry();
        // context 是标准 profile 里的基础 feature，不声明任何端口组。
        registry.Register("context", (in GamePhaseContext ctx) => new PassThroughGamePhaseFeature(),
                new MobaFeatureContract(MobaFeatureLifetime.Battle))
            .Register("hud", (in GamePhaseContext ctx) => new PassThroughGamePhaseFeature(),
                new MobaFeatureContract(requiredFeatureIds: new[] { "context" },
                    satisfiedPortGroups: new[] { "deathmatch" }))
            .Register("wave_hud", (in GamePhaseContext ctx) => new PassThroughGamePhaseFeature(),
                new MobaFeatureContract(requiredFeatureIds: new[] { "context" },
                    satisfiedPortGroups: new[] { "wave" }));
        return registry;
    }

    [Fact]
    public void FeatureWithoutPortGroup_IsAlwaysInstalled()
    {
        var composition = MobaBattleFlowProfile.CreateStandard().Resolve()
            .FilterByPortGroups(Array.Empty<string>(), Registries());

        Assert.Contains("context", composition.GetFeatures("Battle.Prepare"));
    }

    [Fact]
    public void PortGroupFeature_IsInstalledOnlyWhenGroupIsDeclared()
    {
        var withoutDeathmatch = MobaBattleFlowProfile.CreateStandard().Resolve()
            .FilterByPortGroups(new[] { "wave" }, Registries());
        Assert.DoesNotContain("hud", withoutDeathmatch.GetFeatures("Battle.InMatch"));

        var withDeathmatch = MobaBattleFlowProfile.CreateStandard().Resolve()
            .FilterByPortGroups(new[] { "deathmatch" }, Registries());
        Assert.Contains("hud", withDeathmatch.GetFeatures("Battle.InMatch"));
    }

    [Fact]
    public void RetainedFeatures_AreFilteredInStepWithInstalledOnes()
    {
        var composition = MobaBattleFlowProfile.CreateStandard().Resolve()
            .FilterByPortGroups(Array.Empty<string>(), Registries());

        // 被裁掉的 feature 不能继续留在保留列表里，否则重新进入阶段时会重复安装。
        Assert.DoesNotContain("hud", composition.GetRetainedFeatures("Battle.Prepare"));
    }

    [Fact]
    public void PortGroupContract_RejectsEmptyGroupId()
    {
        Assert.Throws<ArgumentException>(() => new MobaFeatureContract(
            satisfiedPortGroups: new[] { " " }));
    }

    private sealed class PassThroughGamePhaseFeature : IGamePhaseFeature
    {
        public void OnAttach(in GamePhaseContext ctx) { }
        public void OnDetach(in GamePhaseContext ctx) { }
        public void Tick(in GamePhaseContext ctx, float deltaTime) { }
    }
}

public sealed class MobaDungeonScopeLifecycleTests
{
    private static MobaDungeonDef Def(int id = 1, string scene = "none") =>
        new MobaDungeonDef(id, "Dungeon", 1, "moba.standard", 0, 1, 0, null,
            scene, "team_deathmatch", "team_deathmatch", new[] { "deathmatch" });

    [Fact]
    public void DungeonRuntimeState_IsScopedPerBattle()
    {
        using var host = new BattleWorldScopeHost();
        host.BeginBattle();
        var first = host.Resolve<MobaDungeonRuntimeState>();
        first.PlayerCount = 4;

        Assert.Same(first, host.Resolve<MobaDungeonRuntimeState>());

        host.EndBattle();
        host.BeginBattle();

        // 新局拿到干净实例，无需手工清零。
        Assert.Equal(0, host.Resolve<MobaDungeonRuntimeState>().PlayerCount);
    }

    [Fact]
    public void SeedPlayerCount_SetsPlayerCountOnContainerInstance()
    {
        using var host = new BattleWorldScopeHost();
        host.BeginBattle();

        host.SeedPlayerCount(3);

        Assert.Equal(3, host.Resolve<MobaDungeonRuntimeState>().PlayerCount);
    }

    [Fact]
    public void SeedPlayerCount_ClampsNonPositiveToOne()
    {
        using var host = new BattleWorldScopeHost();
        host.BeginBattle();

        host.SeedPlayerCount(0);

        Assert.Equal(1, host.Resolve<MobaDungeonRuntimeState>().PlayerCount);
    }

    [Fact]
    public void SeedPlayerCount_WithoutActiveScope_Throws()
    {
        using var host = new BattleWorldScopeHost();

        Assert.Throws<InvalidOperationException>(() => host.SeedPlayerCount(2));
    }

    [Fact]
    public void SeededDungeonDef_IsResolvableFromScope()
    {
        using var host = new BattleWorldScopeHost();
        var dungeon = Def();
        host.BeginBattle(seeder => seeder.Seed(dungeon));

        Assert.Same(dungeon, host.Resolve<MobaDungeonDef>());
    }

    [Fact]
    public void DungeonRegistry_IsScopedPerBattle()
    {
        using var host = new BattleWorldScopeHost();
        host.BeginBattle();

        Assert.True(host.Resolve<MobaDungeonRegistry>().ContainsGameplayHandler("team_deathmatch"));
    }

    [Fact]
    public void EndingBattle_DisposesSceneHandleHeldByRuntimeState()
    {
        using var host = new BattleWorldScopeHost();
        host.BeginBattle();
        var handle = new NullBattleSceneProvider()
            .Assemble(new DungeonSceneContext(Def(), 1));
        host.Resolve<MobaDungeonRuntimeState>().SceneHandle = handle;

        host.EndBattle();

        // scope 释放时应回放已装配的副本场景句柄。
        Assert.False(handle.IsLoaded);
    }

    [Fact]
    public void DungeonRuntimeState_Dispose_IsIdempotent()
    {
        var state = new MobaDungeonRuntimeState { PlayerCount = 2 };
        state.SceneHandle = new NullBattleSceneProvider()
            .Assemble(new DungeonSceneContext(Def(), 2));

        state.Dispose();
        state.Dispose();

        Assert.Null(state.SceneHandle);
        Assert.Equal(0, state.PlayerCount);
    }
}

public sealed class MobaDungeonFeatureOrderTests
{
    [Fact]
    public void DungeonContext_AttachesBeforeDungeonScene()
    {
        // dungeon_scene 依赖 dungeon_context 已经把副本上下文发布到 feature store，
        // 因此安装顺序必须固定，否则场景装配会找不到副本。
        var features = MobaBattleFlowProfile.CreateStandard().GetFeatures("Battle.Prepare");

        var context = Array.IndexOf(features, "dungeon_context");
        var scene = Array.IndexOf(features, "dungeon_scene");

        Assert.True(context >= 0, "dungeon_context must be installed in Battle.Prepare.");
        Assert.True(scene > context, "dungeon_scene must attach after dungeon_context.");
    }

    [Fact]
    public void DungeonFeatures_AreBattleScopedSoTheySurviveEveryPhase()
    {
        var profile = MobaBattleFlowProfile.CreateStandard();
        var retained = profile.Resolve().GetRetainedFeatures("Battle.Prepare");

        Assert.Contains("dungeon_context", retained);
        Assert.Contains("dungeon_scene", retained);
    }

    [Fact]
    public void NoBattleViewProfile_StillCarriesDungeonFeatures()
    {
        // 副本上下文与场景装配属于副本能力，不应被「无战斗视图」剔除。
        var profile = MobaBattleFlowProfile.CreateWithoutBattleView();
        profile.ValidateOrThrow();

        var features = profile.GetFeatures("Battle.Prepare");
        Assert.Contains("dungeon_context", features);
        Assert.Contains("dungeon_scene", features);
    }

    [Fact]
    public void PublishedProfileJson_MatchesStandardComposition()
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "battle_flow_profiles.json")));
        var profile = System.Text.Json.JsonSerializer.Deserialize<MobaBattleFlowProfile>(
            document.RootElement.GetProperty("Profiles")[0].GetRawText(),
            new System.Text.Json.JsonSerializerOptions { IncludeFields = true });

        Assert.NotNull(profile);
        var features = profile.GetFeatures("Battle.Prepare");
        Assert.Contains("dungeon_context", features);
        Assert.Contains("dungeon_scene", features);
    }
}
