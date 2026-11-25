using AbilityKit.Ability.StateSync.Prediction.Reconciliation;
using AbilityKit.BattleScenario.Editor;
using AbilityKit.Scenario;
using Xunit;
using StateSample = AbilityKit.Ability.StateSync.Samples.LocalStateCommitExample.StateSample;

namespace AbilityKit.World.StateSync.Tests;

public sealed partial class LocalCommittedStateResultTests
{
    private static readonly CommitObservation InitialObservation = new("initial", 3, null,
        new CommitValue(1, 0, 10), new CommitValue(1, 0, 10), Published: null,
        ReconciledRevision: 1, TerminalSequence: 0, WaitingForFull: false, Authority: 10, Prediction: 20);

    [Fact]
    public void CommitFailureBattleScenario_ImportFailureKeepsLastCommitAndAllowsRetry()
    {
        var candidate = new StateSample(frame: 1, revision: 2, value: 11);
        var trace = RunCommitScenarioTwice("import-failure.battlescenario", new Dictionary<string, CommitDelivery>
        {
            ["fail-import"] = new(candidate, Full: false, TerminalSequence: 1, FailingStage: Stage.Import),
            ["retry-import"] = new(candidate, Full: false, TerminalSequence: 1),
        }, out var scenario);
        var committed = new CommitValue(2, 1, 14);

        Assert.Collection(trace,
            point => CheckCommit(point, InitialObservation, [], [1, 2], []),
            point => CheckCommit(point, InitialObservation with { Command = "fail-import" }, [Stage.Import], [1, 2], []),
            point => CheckCommit(point, InitialObservation with
            {
                Command = "retry-import", DeliveryTick = 4, Reconciliation = ReconciliationStatus.Predicted,
                Admission = committed, Materialized = committed, Published = new PublishValue(committed, Full: false),
                ReconciledRevision = 2, TerminalSequence = 1, Authority = 14, Prediction = 21,
            }, [Stage.Import, Stage.CommitAdmission, Stage.CommitBaseline, Stage.Publish], [2], [2, 3, 4]));
        Assert.Equal<CommitReplayStep>([new(2, 14, 21), new(3, 21, 21), new(4, 21, 21)], trace[^1].ReplaySteps);
        var replay = CreateCommitReplay(scenario, trace);
        var frames = replay.BuildTimeline().Frames;
        var comparison = Assert.Single(frames, frame => frame.Phase == LocalStateSyncReplay.FramePhase.Compare);
        Assert.Equal(20, comparison.PreviousPrediction);
        var anchor = Assert.Single(frames, frame => frame.Phase == LocalStateSyncReplay.FramePhase.Anchor);
        Assert.Equal(14, anchor.Position);
        LocalReplaySample.VerifyAndExport("import-failure", replay);
    }

    [Fact]
    public void CommitFailureBattleScenario_PartialCommitDoesNotPublishAndRequiresNewFull()
    {
        var failed = new StateSample(frame: 1, revision: 2, value: 10);
        var trace = RunCommitScenarioTwice("partial-commit-failure.battlescenario", new Dictionary<string, CommitDelivery>
        {
            ["fail-baseline-commit"] = new(failed, Full: true, TerminalSequence: 1, FailingStage: Stage.CommitBaseline),
            ["retry-partial-full"] = new(failed, Full: true, TerminalSequence: 1),
            ["delta-while-waiting"] = new(new StateSample(frame: 2, revision: 3, value: 12), Full: false, TerminalSequence: 2),
            ["new-full-without-proof"] = new(new StateSample(frame: 1, revision: 4, value: 11), Full: true, TerminalSequence: null),
            ["new-delta-with-proof"] = new(new StateSample(frame: 1, revision: 5, value: 12), Full: false, TerminalSequence: 1),
        }, out var scenario);
        var partial = InitialObservation with
        {
            Command = "fail-baseline-commit", Admission = new CommitValue(2, 1, 13),
            WaitingForFull = true, Authority = null, Prediction = null,
        };
        var full = new CommitValue(4, 1, 14);
        var recovered = partial with
        {
            Command = "new-full-without-proof", DeliveryTick = 5, Reconciliation = ReconciliationStatus.InvalidTerminalProof,
            Admission = full, Materialized = full, Published = new PublishValue(full, Full: true),
            ReconciledRevision = 4, WaitingForFull = false, Authority = 14,
        };
        var delta = new CommitValue(5, 1, 15);

        Assert.Collection(trace,
            point => CheckCommit(point, InitialObservation, [], [1, 2], []),
            point => CheckCommit(point, partial, [Stage.Import, Stage.CommitAdmission, Stage.CommitBaseline], [1, 2], []),
            point => CheckCommit(point, partial with { Command = "retry-partial-full", DeliveryTick = 4 }, [], [1, 2], []),
            point => CheckCommit(point, partial with { Command = "delta-while-waiting", DeliveryTick = 4 }, [], [1, 2], []),
            point => CheckCommit(point, recovered, [Stage.Import, Stage.CommitAdmission, Stage.CommitBaseline, Stage.Publish], [1, 2], []),
            point => CheckCommit(point, recovered with
            {
                Command = "new-delta-with-proof", DeliveryTick = 6, Reconciliation = ReconciliationStatus.Predicted,
                Admission = delta, Materialized = delta, Published = new PublishValue(delta, Full: false),
                ReconciledRevision = 5, TerminalSequence = 1, Authority = 15, Prediction = 22,
            }, [Stage.Import, Stage.CommitAdmission, Stage.CommitBaseline, Stage.Publish], [2], [2, 3, 4, 5, 6]));
        Assert.Equal<CommitReplayStep>([new(2, 15, 22), new(3, 22, 22), new(4, 22, 22), new(5, 22, 22), new(6, 22, 22)], trace[^1].ReplaySteps);
        LocalReplaySample.VerifyAndExport("partial-commit-failure", CreateCommitReplay(scenario, trace));
    }

    private static LocalStateSyncReplay CreateCommitReplay(TestScenario scenario, List<CommitCheckpoint> trace) => new()
    {
        CaseId = scenario.CaseId,
        Variant = "default",
        TickRate = scenario.ResolveExecution().TickRate,
        Checkpoints = trace.Select(point => new LocalStateSyncReplay.Checkpoint
        {
            Command = point.State.Command,
            DeliveryTick = point.State.DeliveryTick,
            Generation = 1,
            Reconciliation = point.State.Reconciliation?.ToString(),
            PublishedRevision = point.State.Published?.State.Revision,
            PublishedPosition = point.State.Published?.State.Value,
            ReconciledRevision = point.State.ReconciledRevision,
            TerminalSequence = point.State.TerminalSequence,
            PredictionWaiting = point.State.WaitingForFull,
            Authority = point.State.Authority,
            Prediction = point.State.Prediction,
            Pending = point.Pending,
            ReplayTicks = point.ReplayTicks,
            Steps = point.ReplaySteps.Select(step => new LocalStateSyncReplay.ReplayStep
            {
                Tick = step.Tick,
                Before = step.Before,
                After = step.After,
            }).ToArray(),
            Commit = new LocalStateSyncReplay.CommitObservation
            {
                AdmittedRevision = point.State.Admission?.Revision,
                AdmittedTick = point.State.Admission?.Tick,
                MaterializedRevision = point.State.Materialized?.Revision,
                MaterializedPosition = point.State.Materialized?.Value,
                Stages = point.Stages.Select(stage => stage.ToString()).ToArray(),
            },
        }).ToArray(),
    };

    private static List<CommitCheckpoint> RunCommitScenarioTwice(string asset, IReadOnlyDictionary<string, CommitDelivery> deliveries,
        out TestScenario scenario)
    {
        scenario = LocalStateScenarioTimeline.Load(asset);
        var trace = RunCommitScenario(scenario, deliveries);
        var repeated = RunCommitScenario(LocalStateScenarioTimeline.Load(asset), deliveries);
        Assert.Equal(trace.Count, repeated.Count);
        for (var index = 0; index < trace.Count; index++)
        {
            CheckCommit(repeated[index], trace[index].State, trace[index].Stages, trace[index].Pending, trace[index].ReplayTicks);
            Assert.Equal(trace[index].ReplaySteps, repeated[index].ReplaySteps);
        }
        return trace;
    }

    private static List<CommitCheckpoint> RunCommitScenario(TestScenario scenario, IReadOnlyDictionary<string, CommitDelivery> deliveries)
    {
        var plan = LocalStateScenarioTimeline.Compile<CommitDelivery?>(scenario, Bind);
        var host = new LocalCommitHost();
        var trace = new List<CommitCheckpoint>();
        var sealedThrough = 3;
        foreach (var step in plan)
        {
            var name = LocalStateScenarioTimeline.LocalName(step.Name);
            var tick = step.Tick;
            Assert.True(tick >= sealedThrough);
            while (sealedThrough < tick)
                Assert.True(host.Reconciler.SealTick(host.Scope, ++sealedThrough));

            host.Attempted.Clear();
            host.Published.Clear();
            host.ReplaySteps.Clear();
            ReconciliationStatus? status = null;
            if (step.Action is { } delivery)
            {
                var candidate = delivery.Candidate;
                InputTerminalProof? proof = delivery.TerminalSequence is { } sequence
                    ? new InputTerminalProof(host.Scope, candidate.Revision, candidate.Frame, sequence)
                    : null;
                status = host.Deliver(candidate, proof, delivery.Full, delivery.FailingStage, targetTick: tick)?.Status;
            }

            PublishValue? published = null;
            if (host.Published.Count != 0)
            {
                var result = Assert.Single(host.Published);
                published = new PublishValue(new CommitValue(result.Revision, result.Tick, result.State), result.IsFullBaseline);
            }
            var state = new CommitObservation(name, tick, status,
                host.Admission.TryGetCommitted(out var admitted) ? CommitValue.From(admitted) : null,
                host.Materialized is { } materialized ? CommitValue.From(materialized) : null, published,
                host.Reconciler.AcceptedRevision, host.Reconciler.TerminalSequence, host.Reconciler.WaitingForFullBaseline,
                host.Reconciler.TryGetAuthoritative(out var authority) ? authority : null,
                host.Reconciler.TryGetPrediction(out var prediction) ? prediction : null);
            trace.Add(new CommitCheckpoint(state, host.Attempted.ToArray(), host.PendingSequences().ToArray(), host.ReplaySteps.ToArray()));
        }
        return trace;

        bool Bind(string name, out CommitDelivery? action)
        {
            if (deliveries.TryGetValue(name, out var delivery))
            {
                action = delivery;
                return true;
            }
            action = null;
            return name == "initial";
        }
    }

    private static void CheckCommit(CommitCheckpoint point, CommitObservation expected, Stage[] stages, long[] pending, int[] replayTicks)
    {
        Assert.Equal(expected, point.State);
        Assert.Equal(stages, point.Stages);
        Assert.Equal(pending, point.Pending);
        Assert.Equal(replayTicks, point.ReplayTicks);
    }

    private readonly record struct CommitDelivery(StateSample Candidate, bool Full, long? TerminalSequence, Stage? FailingStage = null);
    private readonly record struct CommitValue(long Revision, int Tick, int Value)
    {
        internal static CommitValue From(StateSample sample) => new(sample.Revision, sample.Frame, sample.Value);
    }
    private readonly record struct PublishValue(CommitValue State, bool Full);
    private sealed record CommitObservation(string Command, int DeliveryTick, ReconciliationStatus? Reconciliation,
        CommitValue? Admission, CommitValue? Materialized, PublishValue? Published, long ReconciledRevision,
        long TerminalSequence, bool WaitingForFull, int? Authority, int? Prediction);
    private readonly record struct CommitReplayStep(int Tick, int Before, int After);
    private sealed record CommitCheckpoint(CommitObservation State, Stage[] Stages, long[] Pending, CommitReplayStep[] ReplaySteps)
    {
        internal int[] ReplayTicks => ReplaySteps.Select(step => step.Tick).ToArray();
    }
}
