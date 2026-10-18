using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Flow;
using AbilityKit.Game.View.Flow;
using Xunit;

namespace AbilityKit.Game.View.Runtime.Tests;

public sealed class FeatureSchedulerCompositionTests
{
    [Fact]
    public void BattleEntryFailure_RollsBackInstalledFeatures()
    {
        var events = new List<string>();
        var plan = new PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>()
            .Add("context", (in GamePhaseContext ctx) => new RecordingFeature("context", events))
            .Add("entity", (in GamePhaseContext ctx) => throw new InvalidOperationException("factory failed"));
        var scheduler = CreateScheduler(plan);
        var ctx = default(GamePhaseContext);

        Assert.Throws<InvalidOperationException>(() =>
            scheduler.BattleStateBindings.Enter(MobaBattleState.Prepare, in ctx));

        Assert.Equal(new[] { "attach:context", "detach:context" }, events);
        Assert.Empty(scheduler.Features.Features);
    }

    [Fact]
    public async Task FailedAsyncDetach_RemainsOwnedUntilRetrySucceeds()
    {
        var events = new List<string>();
        var feature = new FlakyAsyncFeature();
        var scheduler = CreateScheduler(new PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>());
        var context = new RecordingFeature("context", events);
        scheduler.AttachFeature(context);
        scheduler.AttachFeature(feature);

        await Assert.ThrowsAsync<AggregateException>(() => scheduler.ClearFeaturesAsync());
        Assert.Equal(2, scheduler.Features.Features.Count);
        Assert.Empty(events.Where(e => e == "detach:context"));

        await scheduler.ClearFeaturesAsync();
        Assert.Equal(2, feature.DetachAttempts);
        Assert.Empty(scheduler.Features.Features);
        Assert.Contains("detach:context", events);
    }

    [Fact]
    public async Task FailedAttach_RemainsOwnedForCleanup()
    {
        var feature = new ThrowingAttachFeature();
        var scheduler = CreateScheduler(new PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>());

        Assert.Throws<InvalidOperationException>(() => scheduler.AttachFeature(feature));
        Assert.Empty(scheduler.Features.Features);

        await scheduler.ClearFeaturesAsync();
        Assert.Equal(1, feature.DetachAttempts);
    }

    [Fact]
    public void TickFailure_IsPropagatedToTheFlowDomain()
    {
        var scheduler = CreateScheduler(new PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>());
        scheduler.AttachFeature(new ThrowingTickFeature());
        var ctx = default(GamePhaseContext);

        Assert.Throws<InvalidOperationException>(() => scheduler.Tick(in ctx, 0.1f));
    }

    [Fact]
    public void OptionalFeature_CanDisableItselfAfterTickFailure()
    {
        var scheduler = CreateScheduler(new PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>());
        var feature = new DisableOnTickFailureFeature();
        scheduler.AttachFeature(feature);
        var ctx = default(GamePhaseContext);

        scheduler.Tick(in ctx, 0.1f);
        scheduler.Tick(in ctx, 0.1f);

        Assert.Equal(1, feature.TickAttempts);
        Assert.Equal(1, feature.DetachAttempts);
        Assert.Empty(scheduler.Features.Features);
    }

    [Fact]
    public async Task GlobalCleanup_ReleasesStateOwnershipBeforeStateExit()
    {
        var events = new List<string>();
        var plan = new PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>()
            .Add("debug_ongui", (in GamePhaseContext ctx) =>
                new RecordingFeature("debug_ongui", events));
        var scheduler = CreateScheduler(plan);
        var ctx = default(GamePhaseContext);

        scheduler.BattleStateBindings.Enter(MobaBattleState.Connect, in ctx);
        await scheduler.ClearFeaturesAsync();
        scheduler.BattleStateBindings.Exit(MobaBattleState.Connect, in ctx);

        Assert.Equal(1, events.Count(e => e == "detach:debug_ongui"));
    }

    private static FeatureScheduler CreateScheduler(
        PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature> plan) =>
        new FeatureScheduler(NullLogSink.Instance,
            new FeatureScheduler.Callbacks
            {
                FeatureBinderAttach = _ => { }, FeatureBinderDetach = _ => { },
                ClearBattleSessionEvents = () => { }, ExecuteFlowAction = (_, _) => { },
                ExecuteSwitchFlow = (_, _) => { }
            }, default, MobaFlowConfiguration.CreateDefault(),
            new PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>(), plan);

    [Fact]
    public async Task ClearFeaturesAsync_WaitsForBattleSessionDetach()
    {
        var events = new List<string>();
        var session = new AsyncRecordingFeature(events);
        var plan = new PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>()
            .Add("context", (in GamePhaseContext ctx) => new RecordingFeature("context", events))
            .Add("entity", (in GamePhaseContext ctx) => new RecordingFeature("entity", events))
            .Add("session", (in GamePhaseContext ctx) => session)
            .Add("gameplay_presentation", (in GamePhaseContext ctx) =>
                new RecordingFeature("gameplay_presentation", events));
        var scheduler = new FeatureScheduler(
            NullLogSink.Instance,
            new FeatureScheduler.Callbacks
            {
                FeatureBinderAttach = _ => { },
                FeatureBinderDetach = _ => { },
                ClearBattleSessionEvents = () => events.Add("clear"),
                ExecuteFlowAction = (_, _) => { },
                ExecuteSwitchFlow = (_, _) => { }
            },
            default,
            MobaFlowConfiguration.CreateDefault(),
            new PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>(),
            plan);
        var ctx = default(GamePhaseContext);
        scheduler.BattleStateBindings.Enter(MobaBattleState.Prepare, in ctx);
        scheduler.BattleStateBindings.Exit(MobaBattleState.Prepare, in ctx);

        var clear = scheduler.ClearFeaturesAsync();
        Assert.False(clear.IsCompleted);
        Assert.DoesNotContain("detach:context", events);

        session.CompleteDetach();
        await clear;
        Assert.Contains("detach:context", events);
        Assert.Equal("clear", events.Last());
    }

    [Fact]
    public void BattleBindings_UseResolvedFeaturesAndKeepLifecycleCallbacks()
    {
        var composition = MobaBattleFlowProfile.CreateStandard().Resolve();
        var config = MobaFlowConfiguration.CreateResolved(composition);
        var events = new List<string>();
        var battlePlan = new PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>();
        foreach (var featureId in composition.FeaturesByState.Values.SelectMany(ids => ids).Distinct())
        {
            var id = featureId;
            battlePlan.Add(id, (in GamePhaseContext ctx) => new RecordingFeature(id, events));
        }

        var scheduler = new FeatureScheduler(
            NullLogSink.Instance,
            new FeatureScheduler.Callbacks
            {
                FeatureBinderAttach = _ => { },
                FeatureBinderDetach = _ => { },
                ClearBattleSessionEvents = () => events.Add("clear"),
                ExecuteFlowAction = (id, count) => events.Add($"action:{id}:{count}"),
                ExecuteSwitchFlow = (id, count) => events.Add($"switch:{id}:{count}")
            },
            default,
            config,
            new PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>(),
            battlePlan);
        var ctx = default(GamePhaseContext);

        Assert.Equal(config.BattleMachine.States.Count, scheduler.BattleStateBindings.Count);
        Assert.Equal(7, scheduler.BattleStateBindings.Enter(MobaBattleState.Prepare, in ctx));
        Assert.Equal(new[]
        {
            $"action:{MobaFlowActionIds.ResetBattleSessionRuntimeState}:0", "clear",
            "attach:context", "attach:entity", "attach:session", "attach:battle_outcome",
            "attach:gameplay_presentation", "attach:dungeon_context", "attach:dungeon_scene"
        }, events);

        events.Clear();
        Assert.Equal(1, scheduler.BattleStateBindings.Enter(MobaBattleState.Connect, in ctx));
        Assert.Equal(new[] { "attach:debug_ongui",
            $"switch:{MobaFlowSwitchIds.AdvanceOnConnectEnter}:1" }, events);

        events.Clear();
        scheduler.BattleStateBindings.Exit(MobaBattleState.Connect, in ctx);
        Assert.Equal(new[] { "detach:debug_ongui" }, events);

        events.Clear();
        Assert.Equal(2, scheduler.BattleStateBindings.Enter(MobaBattleState.LoadAssets, in ctx));
        scheduler.BattleStateBindings.Exit(MobaBattleState.LoadAssets, in ctx);
        Assert.Equal(new[] { "attach:loading_screen", "attach:debug_ongui",
            $"switch:{MobaFlowSwitchIds.AdvanceOnLoadAssetsEnter}:2",
            "detach:debug_ongui", "detach:loading_screen" }, events);

        events.Clear();
        Assert.Equal(5, scheduler.BattleStateBindings.Enter(MobaBattleState.InMatch, in ctx));
        scheduler.BattleStateBindings.Exit(MobaBattleState.InMatch, in ctx);
        Assert.Equal(5, events.Count(e => e.StartsWith("detach:")));

        events.Clear();
        Assert.Equal(2, scheduler.BattleStateBindings.Enter(MobaBattleState.End, in ctx));
        // 进入 Battle.End（clearBeforeEnter）时：先解醒上一阶段保留的 battle 级 feature（含副本上下文与副本场景），
        // 再 clear 本阶段 feature，最后挂开结算 feature 并执行 enter-after 动作。
        Assert.Equal(new[]
        {
            "detach:dungeon_scene", "detach:dungeon_context", "detach:gameplay_presentation",
            "detach:battle_outcome", "detach:session", "detach:entity", "detach:context",
            "clear", "attach:end_settlement", "attach:debug_ongui",
            $"action:{MobaFlowActionIds.ReturnLobbyAfterBattleEnd}:2"
        }, events);
    }

    [Fact]
    public void ConfigureBattle_ReplacesBindingsBetweenTwoBattles()
    {
        var events = new List<string>();
        var standard = MobaBattleFlowProfile.CreateStandard();
        var alternate = MobaBattleFlowProfile.CreateStandard();
        alternate.ProfileId = "moba.alternate";
        alternate.Phases[1].FeatureRefs = Array.Empty<MobaBattleFlowFeatureRef>();
        alternate.Phases[1].FeatureIds = new[] { "alternate_connect" };
        var standardComposition = standard.Resolve();
        var alternateComposition = alternate.Resolve();
        var plan = new PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>();
        foreach (var id in standardComposition.FeaturesByState.Values
                     .Concat(alternateComposition.FeaturesByState.Values)
                     .SelectMany(ids => ids).Distinct())
        {
            var featureId = id;
            plan.Add(featureId, (in GamePhaseContext ctx) => new RecordingFeature(featureId, events));
        }
        var scheduler = new FeatureScheduler(NullLogSink.Instance,
            new FeatureScheduler.Callbacks
            {
                FeatureBinderAttach = _ => { }, FeatureBinderDetach = _ => { },
                ClearBattleSessionEvents = () => { }, ExecuteFlowAction = (_, _) => { },
                ExecuteSwitchFlow = (_, _) => { }
            }, default, MobaFlowConfiguration.CreateResolved(standardComposition),
            new PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>(), plan);
        var ctx = default(GamePhaseContext);

        scheduler.BattleStateBindings.Enter(MobaBattleState.Connect, in ctx);
        Assert.Contains("attach:debug_ongui", events);
        scheduler.BattleStateBindings.Exit(MobaBattleState.Connect, in ctx);
        scheduler.ClearFeatures();
        scheduler.ConfigureBattle(MobaFlowConfiguration.CreateResolved(alternateComposition), plan);
        events.Clear();
        scheduler.BattleStateBindings.Enter(MobaBattleState.Connect, in ctx);
        Assert.Equal(new[] { "attach:alternate_connect" }, events);
    }

    private sealed class RecordingFeature : IGamePhaseFeature
    {
        private readonly string _id;
        private readonly List<string> _events;

        public RecordingFeature(string id, List<string> events)
        {
            _id = id;
            _events = events;
        }

        public void OnAttach(in GamePhaseContext ctx) => _events.Add("attach:" + _id);
        public void OnDetach(in GamePhaseContext ctx) => _events.Add("detach:" + _id);
        public void Tick(in GamePhaseContext ctx, float deltaTime) { }
    }

    private sealed class AsyncRecordingFeature : IGamePhaseFeature, IAsyncPhaseFeature<GamePhaseContext>
    {
        private readonly List<string> _events;
        private readonly TaskCompletionSource<bool> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public AsyncRecordingFeature(List<string> events) => _events = events;
        public void OnAttach(in GamePhaseContext ctx) => _events.Add("attach:session");
        public void OnDetach(in GamePhaseContext ctx) => _events.Add("detach:session");
        public void Tick(in GamePhaseContext ctx, float deltaTime) { }
        public async Task DetachAsync(GamePhaseContext ctx)
        {
            await _completion.Task;
            _events.Add("detach:session");
        }
        public void CompleteDetach() => _completion.SetResult(true);
    }

    private sealed class FlakyAsyncFeature : IGamePhaseFeature, IAsyncPhaseFeature<GamePhaseContext>
    {
        public int DetachAttempts { get; private set; }
        public void OnAttach(in GamePhaseContext ctx) { }
        public void OnDetach(in GamePhaseContext ctx) { }
        public void Tick(in GamePhaseContext ctx, float deltaTime) { }
        public Task DetachAsync(GamePhaseContext ctx)
        {
            DetachAttempts++;
            return DetachAttempts == 1
                ? Task.FromException(new InvalidOperationException("transient failure"))
                : Task.CompletedTask;
        }
    }

    private sealed class ThrowingAttachFeature : IGamePhaseFeature
    {
        public int DetachAttempts { get; private set; }
        public void OnAttach(in GamePhaseContext ctx) => throw new InvalidOperationException("attach failed");
        public void OnDetach(in GamePhaseContext ctx) => DetachAttempts++;
        public void Tick(in GamePhaseContext ctx, float deltaTime) { }
    }

    private sealed class ThrowingTickFeature : IGamePhaseFeature
    {
        public void OnAttach(in GamePhaseContext ctx) { }
        public void OnDetach(in GamePhaseContext ctx) { }
        public void Tick(in GamePhaseContext ctx, float deltaTime) =>
            throw new InvalidOperationException("tick failed");
    }

    private sealed class DisableOnTickFailureFeature : IGamePhaseFeature,
        IGamePhaseFeatureTickFailurePolicy
    {
        public GamePhaseFeatureTickFailureMode TickFailureMode =>
            GamePhaseFeatureTickFailureMode.DisableFeature;
        public int TickAttempts { get; private set; }
        public int DetachAttempts { get; private set; }
        public void OnAttach(in GamePhaseContext ctx) { }
        public void OnDetach(in GamePhaseContext ctx) => DetachAttempts++;
        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            TickAttempts++;
            throw new InvalidOperationException("optional feature failed");
        }
    }
}
