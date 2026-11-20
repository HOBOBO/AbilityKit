using System.Reflection;
using AbilityKit.Ability.Behavior;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Management;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Input;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.Behavior.AI;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Network.Runtime;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed partial class MobaSkillLifecycleRollbackPreparationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Active_path_replans_identically_across_two_client_prediction_corrections(bool productionSummon)
    {
        const float delta = 1f / 30f;
        const int brainId = 79999;
        var actors = Get<MobaActorRegistry>();
        Assert.True(actors.TryGet(1, out var movingActor));
        foreach (var pair in actors.Entries)
            if (pair.Value.hasTransform) PlacePathReplayActor(pair.Value, new Vec3(40f + pair.Key, 0, 40));
        var movingId = 1;
        if (productionSummon)
        {
            PlacePathReplayActor(movingActor, new Vec3(4, 0, -10));
            var spawn = new Vec3(-10, 0, -10);
            Assert.True(Get<MobaSummonService>().TrySummon(1, 30030502, in spawn, Vec3.Right, default, out movingId));
        }
        else
        {
            PlacePathReplayActor(movingActor, new Vec3(-10, 0, -10));
            var brains = Get<MobaBrainService>();
            var driver = new PathReplayDecision();
            brains.DecisionDrivers.Register(driver);
            Assert.IsType<MobaActorBrainCatalog>(Get<IMobaActorBrainCatalog>()).Register(
                new MobaActorBrainDefinition(brainId, driver.Kind, "fixed-target"));
            Assert.True(brains.ActivateBrain(movingActor, brainId, 0, 0));
        }
        for (var step = 0; step < 8; step++) _bootstrapper.Tick();
        Assert.True(actors.TryGet(movingId, out movingActor));
        var path = Get<MobaPathFollowingRuntimeService>();
        Assert.True(path.TryGet(movingId, out var active));
        Assert.True(active.Source.IsActive);
        if (productionSummon)
        {
            Assert.True(Get<IMobaActorBrainCatalog>().TryGet(movingActor.actorBrain.BrainId, out var definition));
            Assert.Equal(MobaBrainDriverKeys.BehaviorTree, definition.DriverKind);
            Assert.Equal("summon_warden_bt", definition.DecisionName);
            for (var step = 0; step < 25 && active.Data.FramesSinceRepath != 18; step++) _bootstrapper.Tick();
            Assert.Equal(18, active.Data.FramesSinceRepath);
        }
        else active.Data.FramesSinceRepath = 18;

        var worlds = (WorldManager)typeof(ConsoleBattleBootstrapper)
            .GetField("_runtimeWorldManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_bootstrapper)!;
        var registry = MobaRollbackRegistryBuilder.CreateForPrediction(_world);
        Assert.True(registry.TryGet(MobaSkillLifecycleRollbackProvider.DefaultKey, out var lifecycle));
        Assert.True(registry.TryGet(MobaActorMotionRollbackProvider.DefaultKey, out var motion));
        Assert.True(registry.TryGet(MobaBrainRollbackProvider.DefaultKey, out var brain));
        using var remote = new FrameJitterBuffer<PlayerInputCommand[]>(0);
        var hostOptions = new HostRuntimeOptions();
        var host = new HostRuntime(worlds, hostOptions);
        var first = _bootstrapper.Context.LastFrame + 1;
        var last = first + 3;
        var injectedMismatch = first + 1;
        var gate = Get<MobaLogicWorldRunGateService>();
        var calculator = new MobaAuthoritativeStateHashCalculator();
        uint BattleHash() => calculator.Compute(gate.InGame, actors);
        var prediction = new ClientPredictionDriverModule(_ => remote, _ => null!,
            resolveIdealFrameLimit: _ => last, minPredictionWindow: 4, enableRollback: true,
            buildRollbackRegistry: MobaRollbackRegistryBuilder.CreateForPrediction,
            buildComputeHash: _ => frame => new WorldStateHash(BattleHash() ^ (frame.Value == injectedMismatch ? 1u : 0u)));
        prediction.Install(host, hostOptions);
        try
        {
            hostOptions.WorldCreated.Invoke(_world);
            Assert.True(prediction.TryRebase(_world.Id, new FrameIndex(first - 1)));
            var baseline = new Dictionary<int, (byte[] Lifecycle, byte[] Motion, byte[] Brain, uint Hash, Vec3 Position, int Age, long Revision)>();
            for (var frame = first; frame <= last; frame++)
            {
                Advance(frame, confirmed: false);
                baseline.Add(frame, Observe(frame));
            }
            Assert.NotEqual(baseline[first].Position, baseline[last].Position);
            Assert.True(baseline[last].Revision > baseline[first].Revision);
            Assert.Equal(20, baseline[first + 1].Age);
            Assert.Equal(0, baseline[first + 2].Age);
            injectedMismatch = first + 2;
            Correct(first, 1, supplyAuthority: true);
            injectedMismatch = -1;
            Correct(first + 1, 2, supplyAuthority: false);

            void Correct(int checkpoint, int count, bool supplyAuthority)
            {
                prediction.OnAuthoritativeStateHash(_world.Id, new FrameIndex(checkpoint + 1),
                    new WorldStateHash(baseline[checkpoint + 1].Hash));
                Assert.Equal(count, prediction.TotalRollbackCount);
                Assert.Equal(0, prediction.TotalRollbackRestoreFailed);
                Assert.Equal(checkpoint, prediction.LastRollbackFrame.Value);
                Assert.True(prediction.IsReplaying);
                Compare(checkpoint);
                for (var frame = checkpoint + 1; frame <= last; frame++)
                {
                    if (supplyAuthority) remote.Add(frame, Array.Empty<PlayerInputCommand>());
                    Advance(frame, confirmed: true);
                    Compare(frame);
                }
                host.Tick(delta);
                Assert.False(prediction.IsReplaying);
            }

            void Compare(int frame)
            {
                var expected = baseline[frame];
                var actual = Observe(frame);
                Assert.Equal(expected.Lifecycle, actual.Lifecycle);
                Assert.Equal(expected.Motion, actual.Motion);
                Assert.Equal(expected.Brain, actual.Brain);
                Assert.Equal(expected.Hash, actual.Hash);
                Assert.Equal(expected.Position, actual.Position);
                Assert.Equal(expected.Age, actual.Age);
            }

            (byte[] Lifecycle, byte[] Motion, byte[] Brain, uint Hash, Vec3 Position, int Age, long Revision) Observe(int frame)
            {
                var slot = path.CaptureState(movingId);
                Assert.True(slot.State.IsPipelineMember);
                Assert.True(slot.Source.IsActive);
                var index = new FrameIndex(frame);
                return (lifecycle.Export(index), motion.Export(index), brain.Export(index), BattleHash(), movingActor.transform.Value.Position,
                    slot.State.FramesSinceRepath, slot.Revision);
            }

            void Advance(int frame, bool confirmed)
            {
                host.Tick(delta);
                Assert.True(prediction.TryGetFrames(_world.Id, out var confirmedFrame, out var predictedFrame));
                Assert.Equal(frame, predictedFrame.Value);
                if (confirmed) Assert.Equal(frame, confirmedFrame.Value);
            }
        }
        finally
        {
            prediction.Uninstall(host, hostOptions);
        }
    }

    private static void PlacePathReplayActor(ActorEntity actor, Vec3 position) => actor.ReplaceTransform(
        new Transform3(position, actor.transform.Value.Rotation, actor.transform.Value.Scale));

    private sealed class PathReplayDecision : IMobaBrainDecisionDriver, IBehaviorDecision, IBehaviorSnapshotValidator, IMobaActorIntentDecision
    {
        public string Kind => "path-replay-probe";
        public string DecisionType => Kind;
        public string CurrentState => "Running";
        public string SnapshotType => Kind;
        public MobaActorIntent CurrentIntent => MobaActorIntent.MoveTo(new Vec3(4, 0, -10));
        public bool TryCreate(in MobaBrainDecisionCreateContext context, out IBehaviorDecision decision)
        {
            decision = this;
            return true;
        }
        public DecisionResult Decide(IBehaviorContext context, IWorldQuery world) => DecisionResult.Continue(CurrentState);
        public byte[] CaptureSnapshot() => new byte[] { 1 };
        public void RestoreSnapshot(byte[] payload) => ValidateSnapshot(payload);
        public void ValidateSnapshot(byte[] payload)
        {
            if (payload == null || payload.Length != 1 || payload[0] != 1)
                throw new InvalidOperationException("Invalid Path intent probe state.");
        }
    }

    [Fact]
    public void Continuous_progress_survives_two_prediction_corrections()
    {
        const float delta = 1f / 30f;
        const int buffId = 79001;
        const int buffTick = 79101;
        const int intervalTick = 79102;
        const int blockedTemplate = 79002;
        const int grantTemplate = 79003;
        const string blockingTag = "State.Stunned";
        const float buffLifetime = 0.09f;
        const float buffPeriod = 0.05f;

        var configs = Get<MobaConfigDatabase>();
        var continuous = Get<MobaContinuousManager>();
        var intervals = Get<MobaTriggerIntervalContinuousService>();
        var tagRules = Get<IMobaContinuousTagRuleService>();
        var contexts = Get<MobaExecutionContextRegistry>();
        var calls = new List<int>();
        RegisterProbe(buffTick);
        RegisterProbe(intervalTick);
        AddConfig(buffId, new BuffMO(new BuffDTO
        {
            Id = buffId, MaxStacks = 1, DurationMs = 90, IntervalMs = 50, OnIntervalEffects = new[] { buffTick }
        }));
        AddConfig(blockedTemplate, new ContinuousTagTemplateMO(new ContinuousTagTemplateDTO
        {
            Id = blockedTemplate, OngoingBlockedTagNames = new[] { blockingTag }
        }));
        AddConfig(grantTemplate, new ContinuousTagTemplateMO(new ContinuousTagTemplateDTO
        {
            Id = grantTemplate, ApplicationTagNames = new[] { blockingTag }
        }));

        var buff = AddRetainedBuff(1, 2, buffId).Continuous;
        buff.Refresh(buff.SourceActorId, buffLifetime, 1, 1, buff.TagRequirements);
        buff.IntervalRemainingSeconds = buffPeriod;
        buff.SyncManagedState();
        var backlogId = AddInterval(79004, 1, intervalMs: 10);
        FindInterval(backlogId).IntervalRemainingRaw = DeterministicMathBridge.ToFixed(-0.60f).RawValue;
        var manualId = AddInterval(79005, 3, blockedTemplate, intervalMs: 10);
        var tagPausedId = AddInterval(79006, 3, blockedTemplate, intervalMs: 10);
        Assert.True(continuous.TryPause(FindInterval(manualId)));
        var grantId = AddInterval(79007, 3, grantTemplate);
        tagRules.ReconcileOwner(3);
        AssertPauseState();

        var worlds = (WorldManager)typeof(ConsoleBattleBootstrapper)
            .GetField("_runtimeWorldManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_bootstrapper)!;
        var actors = Get<MobaActorRegistry>();
        var gate = Get<MobaLogicWorldRunGateService>();
        var calculator = new MobaAuthoritativeStateHashCalculator();
        var registry = MobaRollbackRegistryBuilder.Create(_world);
        Assert.True(registry.TryGet(MobaSkillLifecycleRollbackProvider.DefaultKey, out var lifecycle));
        using var remote = new FrameJitterBuffer<PlayerInputCommand[]>(0);
        var hostOptions = new HostRuntimeOptions();
        var host = new HostRuntime(worlds, hostOptions);
        var firstFrame = _frame.Value + 1;
        var lastFrame = firstFrame + 3;
        var injectedMismatchFrame = firstFrame + 1;
        uint BattleHash() => calculator.Compute(gate.InGame, actors);
        var prediction = new ClientPredictionDriverModule(_ => remote, _ => null!,
            resolveIdealFrameLimit: _ => lastFrame, minPredictionWindow: 4, enableRollback: true,
            buildRollbackRegistry: MobaRollbackRegistryBuilder.CreateForPrediction,
            buildComputeHash: _ => frame => new WorldStateHash(BattleHash() ^ (frame.Value == injectedMismatchFrame ? 1u : 0u)));

        prediction.Install(host, hostOptions);
        try
        {
            hostOptions.WorldCreated.Invoke(_world);
            Assert.True(prediction.TryRebase(_world.Id, _frame));
            var baseline = new Dictionary<int, (byte[] Payload, uint Hash, int[] Calls)>();
            var expiryFrame = firstFrame + 2;
            for (var frame = firstFrame; frame <= lastFrame; frame++)
            {
                calls.Clear();
                Advance(frame, confirmed: false);
                baseline.Add(frame, (lifecycle.Export(new FrameIndex(frame)), BattleHash(), calls.ToArray()));
                AssertPauseState();
                if (frame < expiryFrame)
                    Assert.True(FindInterval(backlogId).IntervalRemainingRaw < 0);
                if (frame == expiryFrame - 1)
                {
                    Assert.True(buff.IsActive);
                    Assert.Equal(new[] { buffTick }.Concat(Enumerable.Repeat(intervalTick, 32)), calls);
                    var frameDeltaRaw = DeterministicMathBridge.ToFixed(delta).RawValue;
                    Assert.InRange(buff.IntervalRemainingRaw, 1, frameDeltaRaw);
                    Assert.InRange(buff.RemainingRaw!.Value, 1, frameDeltaRaw);
                }
                if (frame == expiryFrame)
                {
                    Assert.True(buff.IsTerminated);
                    Assert.InRange(calls.Count, 1, 31);
                    Assert.All(calls, id => Assert.Equal(intervalTick, id));
                }
            }
            Assert.Equal(Enumerable.Repeat(intervalTick, 32), baseline[firstFrame].Calls);

            injectedMismatchFrame = firstFrame + 2;
            Correct(firstFrame, 1, supplyAuthority: true);
            injectedMismatchFrame = -1;
            Correct(firstFrame + 1, 2, supplyAuthority: false);

            intervals.EndOwnerProcesses(grantId);
            tagRules.ReconcileOwner(3);
            Assert.True(FindInterval(tagPausedId).IsActive);
            Assert.True(FindInterval(manualId).IsPaused);

            void Correct(int checkpoint, int count, bool supplyAuthority)
            {
                calls.Clear();
                prediction.OnAuthoritativeStateHash(_world.Id, new FrameIndex(checkpoint + 1),
                    new WorldStateHash(baseline[checkpoint + 1].Hash));
                Assert.Equal(count, prediction.TotalRollbackCount);
                Assert.Equal(0, prediction.TotalRollbackRestoreFailed);
                Assert.Equal(checkpoint, prediction.LastRollbackFrame.Value);
                Assert.True(prediction.IsReplaying);
                Assert.Empty(calls);
                Compare(checkpoint);

                for (var frame = checkpoint + 1; frame <= lastFrame; frame++)
                {
                    calls.Clear();
                    if (supplyAuthority) remote.Add(frame, Array.Empty<PlayerInputCommand>());
                    Advance(frame, confirmed: true);
                    Compare(frame);
                    Assert.Equal(baseline[frame].Calls, calls.ToArray());
                }
                host.Tick(delta);
                Assert.False(prediction.IsReplaying);
                Assert.Equal(count, prediction.TotalRollbackCount);
            }

            void Compare(int frame)
            {
                Assert.Equal(baseline[frame].Payload, lifecycle.Export(new FrameIndex(frame)));
                Assert.Equal(baseline[frame].Hash, BattleHash());
                AssertPauseState();
            }

            void Advance(int frame, bool confirmed)
            {
                host.Tick(delta);
                Assert.True(prediction.TryGetFrames(_world.Id, out var confirmedFrame, out var predictedFrame));
                Assert.Equal(frame, predictedFrame.Value);
                if (confirmed) Assert.Equal(frame, confirmedFrame.Value);
            }
        }
        finally
        {
            prediction.Uninstall(host, hostOptions);
        }

        void RegisterProbe(int id)
        {
            var plan = new TriggerPlan<object>(phase: 0, priority: 0, triggerId: id);
            Get<TriggerPlanJsonDatabase>().AddRecord(new TriggerPlanJsonDatabase.Record(id, string.Empty, 0,
                TriggerPlanScope.Global, in plan, new ReplayProbeExecutable(id, calls)));
        }

        long AddInterval(int id, int target, int template = 0, int intervalMs = 0)
        {
            AddConfig(id, new ContinuousProcessMO(new ContinuousProcessDTO
            {
                Id = id, ContinuousTagTemplateId = template, IntervalMs = intervalMs,
                IntervalTriggerIds = intervalMs > 0 ? new[] { intervalTick } : Array.Empty<int>()
            }));
            var root = contexts.Create(new MobaExecutionContextCreateRequest(MobaExecutionKind.PassiveActivation,
                id, target, target, frame: _frame.Value));
            var source = MobaContextSourceView.FromOrigin(new MobaGameplayOrigin(target, target,
                MobaExecutionKind.PassiveActivation, id, root.ContextId, root.ContextId, root.ContextId, root.ContextId),
                MobaContextSourceResolveKind.Origin, MobaContextSourceBoundary.Snapshot, false, "TriggerIntervalContinuous", id);
            Assert.True(intervals.UpsertProcess(id, target, target, root.ContextId, source));
            return root.ContextId;
        }

        void AddConfig<TEntry>(int id, TEntry entry) where TEntry : class =>
            Assert.IsType<IntKeyConfigTable<TEntry>>(configs.GetTable<TEntry>()).Add(id, entry);

        MobaTriggerIntervalContinuousRuntime FindInterval(long owner) => Assert.Single(
            continuous.GetAllContinuous().OfType<MobaTriggerIntervalContinuousRuntime>(), runtime => runtime.SourceContextId == owner);

        void AssertPauseState()
        {
            var manual = FindInterval(manualId);
            var byTags = FindInterval(tagPausedId);
            Assert.True(manual.IsPaused);
            Assert.True(byTags.IsPaused);
            Assert.False(continuous.IsLocallyPausedByTags(manual));
            Assert.True(continuous.IsLocallyPausedByTags(byTags));
        }
    }
}
