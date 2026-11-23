using AbilityKit.Ability.StateSync.Prediction.Reconciliation;
using AbilityKit.BattleScenario;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Scenario;
using Xunit;

namespace AbilityKit.World.StateSync.Tests;

public sealed partial class LocalProjectionReferenceTests
{
    private const string LocalCommandPrefix = "local.statesync.";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FixedBaselineBattleScenario_ReproducesRecoveryAndProofGatedReplay(bool withRecoveryProof)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Samples", "Scenarios", "fixed-baseline-recovery.battlescenario");
        var trace = RunLocalScenario(BattleScenarioCompiler.CompileFile(path), withRecoveryProof);
        var repeated = RunLocalScenario(BattleScenarioCompiler.CompileFile(path), withRecoveryProof);
        Assert.Equal(trace.Select(point => point.State), repeated.Select(point => point.State));
        for (var index = 0; index < trace.Count; index++)
        {
            Assert.Equal(trace[index].Pending, repeated[index].Pending);
            Assert.Equal(trace[index].ReplayTicks, repeated[index].ReplayTicks);
        }

        var initial = new ScenarioObservation("initial-full", 10,
            ClientSnapshotSyncStatus.AppliedFullBaseline, SnapshotStreamValidationStatus.AcceptedFullBaseline,
            ReconciliationStatus.AuthoritativeOnly, Sequence: 1, AnchorTick: 10, BaselineTick: 10,
            PublishedRevision: 1, ReconciledRevision: 1, TerminalSequence: 0, StreamWaiting: false,
            PredictionWaiting: false, Imports: 1, PublishedPosition: 110, Authority: 110, Prediction: null);
        var current = new ScenarioObservation("current-delta", 17,
            ClientSnapshotSyncStatus.AppliedDelta, SnapshotStreamValidationStatus.AcceptedDelta,
            ReconciliationStatus.Predicted, Sequence: 3, AnchorTick: 13, BaselineTick: 10,
            PublishedRevision: 3, ReconciledRevision: 3, TerminalSequence: 1, StreamWaiting: false,
            PredictionWaiting: false, Imports: 2, PublishedPosition: 113, Authority: 113, Prediction: 120);
        var late = current with
        {
            Command = "late-delta", Admission = ClientSnapshotSyncStatus.IgnoredStale,
            Validation = SnapshotStreamValidationStatus.IgnoredStale, Reconciliation = null,
        };
        var mismatch = current with
        {
            Command = "wrong-baseline", DeliveryTick = 18, Admission = ClientSnapshotSyncStatus.NeedsFullBaseline,
            Validation = SnapshotStreamValidationStatus.BaselineMismatch, Reconciliation = null,
            StreamWaiting = true, PredictionWaiting = true, Authority = null, Prediction = null,
        };
        var recovered = new ScenarioObservation("recovery-full", 20,
            ClientSnapshotSyncStatus.AppliedFullBaseline, SnapshotStreamValidationStatus.AcceptedFullBaseline,
            withRecoveryProof ? ReconciliationStatus.Predicted : ReconciliationStatus.InvalidTerminalProof,
            Sequence: 6, AnchorTick: 16, BaselineTick: 16, PublishedRevision: 6, ReconciledRevision: 6,
            TerminalSequence: withRecoveryProof ? 2 : 1, StreamWaiting: false, PredictionWaiting: false,
            Imports: 3, PublishedPosition: 130, Authority: 130, Prediction: withRecoveryProof ? 135 : null);

        Assert.Collection(trace,
            point => Check(point, initial, [], []),
            point => Check(point, current, [2], [14, 15, 16, 17]),
            point => Check(point, late, [2], []),
            point => Check(point, mismatch, [2], []),
            point => Check(point, mismatch with { Command = "delta-during-recovery", Admission = null, Validation = null }, [2], []),
            point => Check(point, recovered, withRecoveryProof ? [3] : [2, 3], withRecoveryProof ? [17, 18, 19, 20] : []));

        static void Check(ScenarioCheckpoint point, ScenarioObservation expected, long[] pending, int[] replayTicks)
        {
            Assert.Equal(expected, point.State);
            Assert.Equal(pending, point.Pending);
            Assert.Equal(replayTicks, point.ReplayTicks);
        }
    }

    private static List<ScenarioCheckpoint> RunLocalScenario(TestScenario scenario, bool withRecoveryProof)
    {
        TestScenarioValidator.ThrowIfInvalid(scenario);
        var execution = scenario.ResolveExecution();
        if (execution.EndCondition.Kind != TestEndConditionKinds.TimelineComplete || execution.SettleDurationMs != 0 ||
            scenario.Timeline.Count != 0 || scenario.Setup.Count != 0 || scenario.Actors.Count != 0 ||
            scenario.Obstacles.Count != 0 || scenario.NetworkWatches.Count != 0 || scenario.Expectations != null)
            throw new InvalidOperationException("本地状态同步夹具只执行命令时间线，无静态世界或收尾阶段。");

        var scope = new ReconciliationScope(1, 17, Guid.Parse("e1846f67-d729-4131-804b-fcb496820011"), 5, 1, 7, 1);
        var host = new LocalProjectionHost<MovementProjection, int>(
            (baseline, offset) => new MovementProjection(baseline.Position + offset),
            projection => new MovementProjection(projection.Position + 10));
        var replayTicks = new List<int>();
        var reconciler = new LocalMovementReconciler<int, int>(scope, ReplayStep);
        var actions = new Dictionary<string, LocalScenarioAction>(StringComparer.Ordinal)
        {
            ["initial-full"] = new SnapshotDelivery(SnapshotAt(1, 10, 10, 100, full: true), new(scope, 1, 10, 0)),
            ["input-first"] = new LocalInput(1, 3),
            ["input-second"] = new LocalInput(2, 7),
            ["current-delta"] = new SnapshotDelivery(SnapshotAt(3, 13, 10, 3), new(scope, 3, 13, 1)),
            ["late-delta"] = new SnapshotDelivery(SnapshotAt(2, 12, 10, 3), new(scope, 2, 12, 1)),
            ["wrong-baseline"] = new SnapshotDelivery(SnapshotAt(4, 14, 12, 3), new(scope, 4, 14, 1)),
            ["delta-during-recovery"] = new SnapshotDelivery(SnapshotAt(5, 15, 10, 3), new(scope, 5, 15, 1)),
            ["input-third"] = new LocalInput(3, 5),
            ["recovery-full"] = new SnapshotDelivery(SnapshotAt(6, 16, 16, 120, full: true),
                withRecoveryProof ? new InputTerminalProof(scope, 6, 16, 2) : null),
        };

        var checkpoints = new List<ScenarioCheckpoint>();
        var nextUnsealedTick = 0;
        var ordered = scenario.Commands.Select((command, index) => (command, index))
            .OrderBy(item => item.command.AtMs).ThenBy(item => item.index);
        foreach (var (command, _) in ordered)
        {
            var tickUnits = (long)command.AtMs * execution.TickRate;
            if (tickUnits % 1000 != 0)
                throw new InvalidOperationException($"命令 '{command.Name}' 没有对齐固定 Tick：{command.AtMs}ms。");
            var tick = checked((int)(tickUnits / 1000));
            if (!command.Name.StartsWith(LocalCommandPrefix, StringComparison.Ordinal) ||
                !actions.TryGetValue(command.Name[LocalCommandPrefix.Length..], out var action) ||
                command.Parameters.Count != 0 || command.ActorAlias != null || command.TargetAlias != null)
                throw new InvalidOperationException($"不支持的本地状态同步命令：'{command.Name}'。");

            var name = command.Name[LocalCommandPrefix.Length..];
            SealThrough(tick - 1);
            switch (action)
            {
                case LocalInput input:
                    Assert.True(reconciler.TryRecordInput(scope, input.Sequence, tick, input.Value), $"{name}@{tick}");
                    break;
                case SnapshotDelivery delivery:
                    SealThrough(tick);
                    checkpoints.Add(Deliver(name, tick, delivery));
                    break;
                default:
                    throw new InvalidOperationException($"未实现的本地状态同步动作：'{name}'。");
            }
        }

        return checkpoints;

        void SealThrough(int tick)
        {
            for (; nextUnsealedTick <= tick; nextUnsealedTick++)
                Assert.True(reconciler.SealTick(scope, nextUnsealedTick));
        }

        ScenarioCheckpoint Deliver(string name, int tick, SnapshotDelivery delivery)
        {
            replayTicks.Clear();
            ClientSnapshotSyncResult? attempt = null;
            ReconciliationStatus? reconciliation = null;
            if (!host.StreamState.NeedsFullBaselineRecovery || delivery.Snapshot.Envelope.IsFullBaseline)
            {
                var committed = host.Receive(delivery.Snapshot, out attempt);
                if (attempt?.Validation.NeedsFullBaseline == true)
                    reconciler.InvalidateBaseline(scope);
                if (committed is { } state)
                    reconciliation = reconciler.Reconcile(MovementAt(state, scope), delivery.Proof, tick).Status;
            }

            var stream = host.StreamState;
            var observation = new ScenarioObservation(name, tick, attempt?.Status, attempt?.Validation.Status, reconciliation,
                stream.LastAppliedSequence, stream.LastAppliedFrame, stream.LastBaselineFrame,
                host.LatestCommitted?.Revision, reconciler.AcceptedRevision, reconciler.TerminalSequence,
                stream.NeedsFullBaselineRecovery, reconciler.WaitingForFullBaseline, host.ImportCalls,
                host.LatestCommitted?.State.Position,
                reconciler.TryGetAuthoritative(out var authority) ? authority : null,
                reconciler.TryGetPrediction(out var prediction) ? prediction : null);
            return new ScenarioCheckpoint(observation,
                Enumerable.Range(0, reconciler.PendingCount).Select(reconciler.GetPendingSequence).ToArray(),
                replayTicks.ToArray());
        }

        bool ReplayStep(int tick, ReadOnlySpan<int> inputs, ref int state)
        {
            replayTicks.Add(tick);
            foreach (var input in inputs)
                state += input;
            return true;
        }
    }

    private static Snapshot<MovementProjection, int> SnapshotAt(long sequence, int tick, int baseline, int value, bool full = false)
    {
        var envelope = new SnapshotStreamEnvelope(17, 1, sequence, tick,
            full ? SnapshotStreamSnapshotKind.FullBaseline : SnapshotStreamSnapshotKind.Delta,
            baseline, (uint)baseline + 1, (uint)tick + 1);
        return new Snapshot<MovementProjection, int>(1, envelope, full ? new MovementProjection(value) : default, full ? 0 : value);
    }

    private abstract record LocalScenarioAction;
    private sealed record LocalInput(long Sequence, int Value) : LocalScenarioAction;
    private sealed record SnapshotDelivery(Snapshot<MovementProjection, int> Snapshot, InputTerminalProof? Proof) : LocalScenarioAction;
    private sealed record ScenarioCheckpoint(ScenarioObservation State, long[] Pending, int[] ReplayTicks);
    private sealed record ScenarioObservation(string Command, int DeliveryTick, ClientSnapshotSyncStatus? Admission,
        SnapshotStreamValidationStatus? Validation, ReconciliationStatus? Reconciliation, long Sequence, int AnchorTick,
        int BaselineTick, long? PublishedRevision, long ReconciledRevision, long TerminalSequence, bool StreamWaiting,
        bool PredictionWaiting, int Imports, int? PublishedPosition, int? Authority, int? Prediction);
}
