using System.Reflection;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Management;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Network.Runtime;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed partial class MobaSkillLifecycleRollbackPreparationTests
{
    [Fact]
    public void In_flight_cast_replays_via_client_driver_and_formal_battle_registry()
    {
        const float delta = 1f / 30f;
        var actors = Get<MobaActorRegistry>();
        var gate = Get<MobaLogicWorldRunGateService>();
        var skills = Get<SkillCastCoordinator>();
        var projectiles = Get<IProjectileService>();
        var executions = Get<MobaTriggerExecutionRuntimeService>();
        var scheduledCalls = new List<int>();
        var triggerContext = default(ExecCtx<object>);
        executions.Schedule(new ReplayProbeExecutable(1, scheduledCalls), null, in triggerContext,
            EScheduleMode.Timed, 50f, 1, true);
        executions.Schedule(new ReplayProbeExecutable(2, scheduledCalls), null, in triggerContext,
            EScheduleMode.Timed, 50f, 1, true);
        executions.Schedule(new ReplayProbeExecutable(3, scheduledCalls), null, in triggerContext,
            EScheduleMode.Timed, 90f, 1, true);
        var caster = actors.Entries.First(pair => pair.Value.hasModelId && pair.Value.modelId.Value == 1001).Key;
        var cast = skills.TryCastSkill(caster, 10010301, 3);
        Assert.True(cast.Success, cast.FailReason);

        var worlds = (WorldManager)typeof(ConsoleBattleBootstrapper)
            .GetField("_runtimeWorldManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_bootstrapper)!;
        var calculator = new MobaAuthoritativeStateHashCalculator();
        var remote = new FrameJitterBuffer<PlayerInputCommand[]>(0);
        var hostOptions = new HostRuntimeOptions();
        var host = new HostRuntime(worlds, hostOptions);
        var checkpoint = _frame.Value + 1;
        var mismatch = new FrameIndex(checkpoint + 1);
        var injectClientMismatch = true;
        var prediction = new ClientPredictionDriverModule(_ => remote, _ => null!,
            resolveIdealFrameLimit: _ => _frame.Value + 4, minPredictionWindow: 4, enableRollback: true,
            buildRollbackRegistry: MobaRollbackRegistryBuilder.Create,
            buildComputeHash: _ => frame => new WorldStateHash(calculator.Compute(gate.InGame, actors) ^
                (injectClientMismatch && frame.Value == mismatch.Value ? 1u : 0u)));
        prediction.Install(host, hostOptions);
        try
        {
            hostOptions.WorldCreated.Invoke(_world);
            Assert.True(prediction.TryRebase(_world.Id, _frame));
            var baseline = new Dictionary<int, (byte[] Casts, uint BattleHash, int ProjectileCount, byte[] TriggerProgress, int[] Calls)>();
            var lifecycle = new MobaSkillPipelineRollbackProvider(skills);
            (byte[] Casts, uint BattleHash, int ProjectileCount, byte[] TriggerProgress, int[] Calls) Read(FrameIndex frame) =>
                (lifecycle.ExportState(frame), calculator.Compute(gate.InGame, actors), projectiles.ActiveCount,
                    executions.ExportState(frame), scheduledCalls.ToArray());

            for (var frame = checkpoint; frame <= checkpoint + 3; frame++)
            {
                host.Tick(delta);
                Assert.True(prediction.TryGetFrames(_world.Id, out _, out var predicted));
                Assert.Equal(frame, predicted.Value);
                baseline.Add(frame, Read(predicted));
            }
            Assert.True(skills.TryGetRunningByInstanceId(caster, cast.RuntimeHandle.RootContextId, out _));
            Assert.NotEqual(baseline[checkpoint].Casts, baseline[checkpoint + 1].Casts);
            Assert.Equal(4, prediction.TotalPredictedFrames);
            Assert.Empty(baseline[checkpoint].Calls);
            Assert.Equal(new[] { 1, 2 }, baseline[checkpoint + 1].Calls);
            Assert.Equal(new[] { 1, 2, 3 }, scheduledCalls);

            scheduledCalls.Clear();
            injectClientMismatch = false;
            prediction.OnAuthoritativeStateHash(_world.Id, mismatch, new WorldStateHash(baseline[mismatch.Value].BattleHash));
            Assert.Equal(1, prediction.TotalReconcileMismatch);
            Assert.Equal(1, prediction.TotalRollbackCount);
            Assert.Equal(0, prediction.TotalRollbackRestoreFailed);
            Assert.Equal(checkpoint, prediction.LastRollbackFrame.Value);
            Assert.True(prediction.IsReplaying);
            Compare(checkpoint);

            for (var frame = checkpoint + 1; frame <= checkpoint + 3; frame++)
            {
                remote.Add(frame, Array.Empty<PlayerInputCommand>());
                host.Tick(delta);
                Assert.True(prediction.TryGetFrames(_world.Id, out var confirmed, out var predicted));
                Assert.Equal(frame, confirmed.Value);
                Assert.Equal(frame, predicted.Value);
                Compare(frame);
            }
            host.Tick(delta);
            Assert.False(prediction.IsReplaying);
            Assert.Equal(1, prediction.TotalRollbackCount);

            void Compare(int frame)
            {
                var expected = baseline[frame];
                var actual = Read(new FrameIndex(frame));
                Assert.Equal(expected.Casts, actual.Casts);
                Assert.Equal(expected.BattleHash, actual.BattleHash);
                Assert.Equal(expected.ProjectileCount, actual.ProjectileCount);
                Assert.Equal(expected.TriggerProgress, actual.TriggerProgress);
                Assert.Equal(expected.Calls, actual.Calls);
            }
        }
        finally
        {
            prediction.Uninstall(host, hostOptions);
            remote.Dispose();
        }
    }

    private sealed class ReplayProbeExecutable(int id, List<int> calls) : ITriggerPlanExecutable
    {
        public string Name => "replay-probe";
        public ETriggerPlanExecutableKind Kind => ETriggerPlanExecutableKind.Succeed;
        public float Weight => 1f;

        public TriggerPlanExecutionResult Execute<TCtx>(object args, in ExecCtx<TCtx> context) where TCtx : class
        {
            calls.Add(id);
            return TriggerPlanExecutionResult.Success();
        }
    }

    [Fact]
    public void Pipeline_progress_keeps_version_one_wire_vector()
    {
        var vector = Convert.FromHexString("0100000011000000036F6C640000000001010001000000057068617365066C6567616379010200000003000000FFFFFFFF01000000200000000000000001000000000000000000C03F00000000");
        var state = PipelineRollbackStateCodec.Deserialize(vector);
        var progress = state.ToProgress();
        Assert.Equal(17, progress.ConfigId);
        Assert.True(progress.HasCurrentPhase);
        Assert.True(progress.IsPaused);
        Assert.False(progress.IsCancelled);
        var node = Assert.Single(progress.Phases);
        Assert.True(node.IsComplete);
        Assert.Equal(new[] { 3, -1 }, node.Integers);
        Assert.Equal(32, Assert.Single(node.Longs));
        Assert.Equal(0.125, Assert.Single(node.Numbers));
        Assert.Equal(vector, PipelineRollbackStateCodec.Serialize(state));
    }
}
