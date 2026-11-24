using AbilityKit.Ability.StateSync.Prediction.Reconciliation;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Scenario;
using Xunit;

namespace AbilityKit.World.StateSync.Tests;

public sealed partial class LocalProjectionReferenceTests
{
    [Fact]
    public void LocalBattleScenario_RejectsUnboundTailBeforeExecutingEarlierInput()
    {
        var scenario = new TestScenario
        {
            CaseId = "unbound-tail",
            Execution = new TestExecutionSpec { TickRate = 100, MaxDurationMs = 20, SettleDurationMs = 0 },
            Commands = new[]
            {
                new TestCommand { Name = "local.statesync.input", AtMs = 10 },
                new TestCommand { Name = "local.statesync.unknown", AtMs = 20 },
            },
        };
        var actions = new Dictionary<string, LocalScenarioAction> { ["input"] = new LocalInput(0, 3) };

        var error = Assert.Throws<InvalidOperationException>(() => RunLocalScenario(scenario, actions));

        Assert.Contains("commands[1] 'local.statesync.unknown' @ 20ms", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FixedBaselineBattleScenario_ReproducesRecoveryAndProofGatedReplay(bool withRecoveryProof)
    {
        var scope = ScopeAt(1);
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
        var trace = RunLocalScenarioTwice("fixed-baseline-recovery.battlescenario", actions, out var scenario);

        var initial = new ScenarioObservation("initial-full", 10,
            ClientSnapshotSyncStatus.AppliedFullBaseline, SnapshotStreamValidationStatus.AcceptedFullBaseline,
            ReconciliationStatus.AuthoritativeOnly, Sequence: 1, AppliedTick: 10, BaselineTick: 10,
            PublishedRevision: 1, ReconciledRevision: 1, TerminalSequence: 0, StreamWaiting: false,
            PredictionWaiting: false, Imports: 1, PublishedPosition: 110, Authority: 110, Prediction: null);
        var current = new ScenarioObservation("current-delta", 17,
            ClientSnapshotSyncStatus.AppliedDelta, SnapshotStreamValidationStatus.AcceptedDelta,
            ReconciliationStatus.Predicted, Sequence: 3, AppliedTick: 13, BaselineTick: 10,
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
            Sequence: 6, AppliedTick: 16, BaselineTick: 16, PublishedRevision: 6, ReconciledRevision: 6,
            TerminalSequence: withRecoveryProof ? 2 : 1, StreamWaiting: false, PredictionWaiting: false,
            Imports: 3, PublishedPosition: 130, Authority: 130, Prediction: withRecoveryProof ? 135 : null);

        Assert.Collection(trace,
            point => Check(point, initial, [], []),
            point => Check(point, current, [2], [14, 15, 16, 17]),
            point => Check(point, late, [2], []),
            point => Check(point, mismatch, [2], []),
            point => Check(point, mismatch with { Command = "delta-during-recovery", Admission = null, Validation = null }, [2], []),
            point => Check(point, recovered, withRecoveryProof ? [3] : [2, 3], withRecoveryProof ? [17, 18, 19, 20] : []));

        Assert.Equal<MovementReplayStep>(
            [new(14, 113, 113), new(15, 113, 113), new(16, 113, 120), new(17, 120, 120)], trace[1].ReplaySteps);
        Assert.Equal<MovementReplayStep>(withRecoveryProof
            ? [new(17, 130, 130), new(18, 130, 130), new(19, 130, 135), new(20, 135, 135)]
            : [], trace[^1].ReplaySteps);
        VerifyAndExportReplay(scenario, trace, withRecoveryProof);
    }

    private static void Check(ScenarioCheckpoint point, ScenarioObservation expected, long[] pending, int[] replayTicks)
    {
        Assert.Equal(expected, point.State);
        Assert.Equal(pending, point.Pending);
        Assert.Equal(replayTicks, point.ReplayTicks);
    }

    private static List<ScenarioCheckpoint> RunLocalScenarioTwice(
        string asset, IReadOnlyDictionary<string, LocalScenarioAction> actions) =>
        RunLocalScenarioTwice(asset, actions, out _);

    private static List<ScenarioCheckpoint> RunLocalScenarioTwice(
        string asset, IReadOnlyDictionary<string, LocalScenarioAction> actions, out TestScenario scenario)
    {
        scenario = LocalStateScenarioTimeline.Load(asset);
        var trace = RunLocalScenario(scenario, actions);
        var repeated = RunLocalScenario(LocalStateScenarioTimeline.Load(asset), actions);
        Assert.Equal(trace.Count, repeated.Count);
        for (var index = 0; index < trace.Count; index++)
        {
            Check(repeated[index], trace[index].State, trace[index].Pending, trace[index].ReplayTicks);
            Assert.Equal(trace[index].ReplaySteps, repeated[index].ReplaySteps);
        }
        return trace;
    }

    private static List<ScenarioCheckpoint> RunLocalScenario(
        TestScenario scenario, IReadOnlyDictionary<string, LocalScenarioAction> actions)
    {
        var plan = LocalStateScenarioTimeline.Compile<LocalScenarioAction>(scenario, Bind);
        var generation = 1;
        var scope = ScopeAt(generation);
        var host = new LocalProjectionHost<MovementProjection, int>(
            (baseline, offset) => new MovementProjection(baseline.Position + offset),
            projection => new MovementProjection(projection.Position + 10));
        var replaySteps = new List<MovementReplayStep>();
        var reconciler = new LocalMovementReconciler<int, int>(scope, ReplayStep);
        var deferred = new Dictionary<string, (CommittedMovement<int> State, InputTerminalProof? Proof)>(StringComparer.Ordinal);
        var checkpoints = new List<ScenarioCheckpoint>();
        var nextUnsealedTick = 0;
        foreach (var step in plan)
        {
            var name = LocalStateScenarioTimeline.LocalName(step.Name);
            var tick = step.Tick;
            replaySteps.Clear();
            SealThrough(tick - 1);
            switch (step.Action)
            {
                case LocalInput input:
                    Assert.True(reconciler.TryRecordInput(scope, input.Sequence, tick, input.Value), $"{name}@{tick}");
                    break;
                case SnapshotDelivery delivery:
                    SealThrough(tick);
                    checkpoints.Add(Deliver(name, tick, delivery));
                    break;
                case SwitchGeneration change:
                    generation = change.Generation;
                    scope = ScopeAt(generation);
                    host.SwitchGeneration(generation);
                    reconciler.SwitchScope(scope);
                    nextUnsealedTick = tick;
                    checkpoints.Add(Observe(name, tick));
                    break;
                case CompleteDeferredCommit completion:
                    SealThrough(tick);
                    if (!deferred.Remove(completion.Source, out var previous))
                        throw new InvalidOperationException($"没有待交付的状态提交：'{completion.Source}'。");
                    var result = reconciler.Reconcile(previous.State, previous.Proof, tick);
                    checkpoints.Add(Observe(name, tick, reconciliation: result.Status));
                    break;
                case LeaveTickUnsealed:
                    Assert.Equal(tick, nextUnsealedTick);
                    nextUnsealedTick++;
                    break;
                default:
                    throw new InvalidOperationException($"未实现的本地状态同步动作：'{name}'。");
            }
        }

        Assert.Empty(deferred);
        return checkpoints;

        bool Bind(string name, out LocalScenarioAction action) => actions.TryGetValue(name, out action!);

        void SealThrough(int tick)
        {
            for (; nextUnsealedTick <= tick; nextUnsealedTick++)
                Assert.True(reconciler.SealTick(scope, nextUnsealedTick));
        }

        ScenarioCheckpoint Deliver(string name, int tick, SnapshotDelivery delivery)
        {
            ClientSnapshotSyncResult? attempt = null;
            ReconciliationStatus? reconciliation = null;
            if (!host.StreamState.NeedsFullBaselineRecovery || delivery.Snapshot.Envelope.IsFullBaseline)
            {
                var committed = host.Receive(delivery.Snapshot, out attempt);
                if (attempt?.Validation.NeedsFullBaseline == true)
                    reconciler.InvalidateBaseline(scope);
                if (committed is { } state)
                {
                    var movement = MovementAt(state, scope);
                    if (delivery.DeferReconciliation)
                        deferred.Add(name, (movement, delivery.Proof));
                    else
                        reconciliation = reconciler.Reconcile(movement, delivery.Proof, tick).Status;
                }
            }
            return Observe(name, tick, attempt, reconciliation);
        }

        ScenarioCheckpoint Observe(string name, int tick, ClientSnapshotSyncResult? attempt = null, ReconciliationStatus? reconciliation = null)
        {
            var stream = host.StreamState;
            var observation = new ScenarioObservation(name, tick, attempt?.Status, attempt?.Validation.Status, reconciliation,
                stream.LastAppliedSequence, stream.LastAppliedFrame, stream.LastBaselineFrame,
                host.LatestCommitted?.Revision, reconciler.AcceptedRevision, reconciler.TerminalSequence,
                stream.NeedsFullBaselineRecovery, reconciler.WaitingForFullBaseline, host.ImportCalls,
                host.LatestCommitted?.State.Position,
                reconciler.TryGetAuthoritative(out var authority) ? authority : null,
                reconciler.TryGetPrediction(out var prediction) ? prediction : null, generation);
            return new ScenarioCheckpoint(observation,
                Enumerable.Range(0, reconciler.PendingCount).Select(reconciler.GetPendingSequence).ToArray(),
                replaySteps.ToArray());
        }

        bool ReplayStep(int tick, ReadOnlySpan<int> inputs, ref int state)
        {
            var before = state;
            foreach (var input in inputs)
                state += input;
            replaySteps.Add(new MovementReplayStep(tick, before, state));
            return true;
        }
    }

    private static ReconciliationScope ScopeAt(int generation) =>
        new(generation, 17, Guid.Parse("e1846f67-d729-4131-804b-fcb496820011"), 5, 1, 7, 1);

    private static Snapshot<MovementProjection, int> SnapshotAt(
        long sequence, int tick, int baseline, int value, bool full = false, int generation = 1)
    {
        var envelope = new SnapshotStreamEnvelope(17, 1, sequence, tick,
            full ? SnapshotStreamSnapshotKind.FullBaseline : SnapshotStreamSnapshotKind.Delta,
            baseline, (uint)baseline + 1, (uint)tick + 1);
        return new Snapshot<MovementProjection, int>(generation, envelope, full ? new MovementProjection(value) : default, full ? 0 : value);
    }

    private abstract record LocalScenarioAction;
    private sealed record LocalInput(long Sequence, int Value) : LocalScenarioAction;
    private sealed record SnapshotDelivery(Snapshot<MovementProjection, int> Snapshot, InputTerminalProof? Proof,
        bool DeferReconciliation = false) : LocalScenarioAction;
    private sealed record SwitchGeneration(int Generation) : LocalScenarioAction;
    private sealed record CompleteDeferredCommit(string Source) : LocalScenarioAction;
    private sealed record LeaveTickUnsealed : LocalScenarioAction;
    private readonly record struct MovementReplayStep(int Tick, int Before, int After);
    private sealed record ScenarioCheckpoint(ScenarioObservation State, long[] Pending, MovementReplayStep[] ReplaySteps)
    {
        internal int[] ReplayTicks => ReplaySteps.Select(step => step.Tick).ToArray();
    }
    private sealed record ScenarioObservation(string Command, int DeliveryTick, ClientSnapshotSyncStatus? Admission,
        SnapshotStreamValidationStatus? Validation, ReconciliationStatus? Reconciliation, long Sequence, int AppliedTick,
        int BaselineTick, long? PublishedRevision, long ReconciledRevision, long TerminalSequence, bool StreamWaiting,
        bool PredictionWaiting, int Imports, int? PublishedPosition, int? Authority, int? Prediction, int Generation = 1);
}
