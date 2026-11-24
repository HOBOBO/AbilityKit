using AbilityKit.Ability.StateSync.Prediction.Reconciliation;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.World.StateSync.Tests;

public sealed partial class LocalProjectionReferenceTests
{
    [Fact]
    public void GenerationSwitchBattleScenario_RejectsDelayedOldCommitAndSnapshot()
    {
        var oldScope = ScopeAt(1);
        var newScope = ScopeAt(2);
        var trace = RunLocalScenarioTwice("generation-switch.battlescenario", new Dictionary<string, LocalScenarioAction>
        {
            ["old-input-first"] = new LocalInput(1, 3),
            ["old-full"] = new SnapshotDelivery(SnapshotAt(1, 10, 10, 100, full: true), new(oldScope, 1, 10, 1)),
            ["old-input-second"] = new LocalInput(2, 5),
            ["old-delta"] = new SnapshotDelivery(SnapshotAt(8, 12, 10, 8), new(oldScope, 8, 12, 2), DeferReconciliation: true),
            ["switch-generation"] = new SwitchGeneration(2),
            ["new-input"] = new LocalInput(1, 7),
            ["new-full"] = new SnapshotDelivery(SnapshotAt(1, 12, 12, 200, full: true, generation: 2), new(newScope, 1, 12, 0)),
            ["late-old-commit"] = new CompleteDeferredCommit("old-delta"),
            ["late-old-snapshot"] = new SnapshotDelivery(SnapshotAt(9, 13, 99, 90), new(oldScope, 9, 13, 2)),
            ["new-delta"] = new SnapshotDelivery(SnapshotAt(2, 13, 12, 4, generation: 2), new(newScope, 2, 13, 1)),
        }, out var scenario);

        var oldFull = new ScenarioObservation("old-full", 10,
            ClientSnapshotSyncStatus.AppliedFullBaseline, SnapshotStreamValidationStatus.AcceptedFullBaseline,
            ReconciliationStatus.AuthoritativeOnly, Sequence: 1, AppliedTick: 10, BaselineTick: 10,
            PublishedRevision: 1, ReconciledRevision: 1, TerminalSequence: 1, StreamWaiting: false,
            PredictionWaiting: false, Imports: 1, PublishedPosition: 110, Authority: 110, Prediction: null);
        var reset = new ScenarioObservation("switch-generation", 13, null, null, null,
            Sequence: 0, AppliedTick: 0, BaselineTick: 0, PublishedRevision: null, ReconciledRevision: 0,
            TerminalSequence: 0, StreamWaiting: false, PredictionWaiting: false, Imports: 2,
            PublishedPosition: null, Authority: null, Prediction: null, Generation: 2);
        var newFull = new ScenarioObservation("new-full", 14,
            ClientSnapshotSyncStatus.AppliedFullBaseline, SnapshotStreamValidationStatus.AcceptedFullBaseline,
            ReconciliationStatus.Predicted, Sequence: 1, AppliedTick: 12, BaselineTick: 12,
            PublishedRevision: 1, ReconciledRevision: 1, TerminalSequence: 0, StreamWaiting: false,
            PredictionWaiting: false, Imports: 3, PublishedPosition: 210, Authority: 210, Prediction: 217, Generation: 2);

        Assert.Collection(trace,
            point => Check(point, oldFull, [], []),
            point => Check(point, oldFull with
            {
                Command = "old-delta", DeliveryTick = 12, Admission = ClientSnapshotSyncStatus.AppliedDelta,
                Validation = SnapshotStreamValidationStatus.AcceptedDelta, Reconciliation = null,
                Sequence = 8, AppliedTick = 12, PublishedRevision = 8, Imports = 2, PublishedPosition = 118,
            }, [2], []),
            point => Check(point, reset, [], []),
            point => Check(point, newFull, [1], [13, 14]),
            point => Check(point, newFull with
            {
                Command = "late-old-commit", DeliveryTick = 15, Admission = null, Validation = null,
                Reconciliation = ReconciliationStatus.Ignored,
            }, [1], []),
            point => Check(point, newFull with
            {
                Command = "late-old-snapshot", DeliveryTick = 15, Admission = null, Validation = null, Reconciliation = null,
            }, [1], []),
            point => Check(point, newFull with
            {
                Command = "new-delta", DeliveryTick = 16, Admission = ClientSnapshotSyncStatus.AppliedDelta,
                Validation = SnapshotStreamValidationStatus.AcceptedDelta, Sequence = 2, AppliedTick = 13,
                PublishedRevision = 2, ReconciledRevision = 2, TerminalSequence = 1,
                Imports = 4, PublishedPosition = 214, Authority = 214, Prediction = 214,
            }, [], [14, 15, 16]));
        LocalReplaySample.VerifyAndExport("generation-switch", CreateProjectionReplay(scenario, trace, "default"));
    }

    [Fact]
    public void ReplayHistoryGapBattleScenario_CommitsAuthorityWithoutPartialReplay()
    {
        var scope = ScopeAt(1);
        var trace = RunLocalScenarioTwice("replay-history-gap.battlescenario", new Dictionary<string, LocalScenarioAction>
        {
            ["initial-full"] = new SnapshotDelivery(SnapshotAt(1, 10, 10, 100, full: true), new(scope, 1, 10, 0)),
            ["input-first"] = new LocalInput(1, 3),
            ["before-gap"] = new SnapshotDelivery(SnapshotAt(2, 11, 10, 3), new(scope, 2, 11, 1)),
            ["input-second"] = new LocalInput(2, 3),
            ["history-gap"] = new LeaveTickUnsealed(),
            ["input-third"] = new LocalInput(3, 7),
            ["crosses-gap"] = new SnapshotDelivery(SnapshotAt(3, 14, 10, 6), new(scope, 3, 14, 2)),
            ["past-gap"] = new SnapshotDelivery(SnapshotAt(4, 16, 10, 8), new(scope, 4, 16, 2)),
        }, out var scenario);

        var initial = new ScenarioObservation("initial-full", 10,
            ClientSnapshotSyncStatus.AppliedFullBaseline, SnapshotStreamValidationStatus.AcceptedFullBaseline,
            ReconciliationStatus.AuthoritativeOnly, Sequence: 1, AppliedTick: 10, BaselineTick: 10,
            PublishedRevision: 1, ReconciledRevision: 1, TerminalSequence: 0, StreamWaiting: false,
            PredictionWaiting: false, Imports: 1, PublishedPosition: 110, Authority: 110, Prediction: null);
        var beforeGap = new ScenarioObservation("before-gap", 13,
            ClientSnapshotSyncStatus.AppliedDelta, SnapshotStreamValidationStatus.AcceptedDelta,
            ReconciliationStatus.Predicted, Sequence: 2, AppliedTick: 11, BaselineTick: 10,
            PublishedRevision: 2, ReconciledRevision: 2, TerminalSequence: 1, StreamWaiting: false,
            PredictionWaiting: false, Imports: 2, PublishedPosition: 113, Authority: 113, Prediction: 113);
        var crossesGap = beforeGap with
        {
            Command = "crosses-gap", DeliveryTick = 18, Reconciliation = ReconciliationStatus.MissingTickCoverage,
            Sequence = 3, AppliedTick = 14, PublishedRevision = 3, ReconciledRevision = 3,
            TerminalSequence = 2, Imports = 3, PublishedPosition = 116, Authority = 116, Prediction = null,
        };

        Assert.Collection(trace,
            point => Check(point, initial, [], []),
            point => Check(point, beforeGap, [], [12, 13]),
            point => Check(point, crossesGap, [3], []),
            point => Check(point, crossesGap with
            {
                Command = "past-gap", DeliveryTick = 19, Reconciliation = ReconciliationStatus.Predicted,
                Sequence = 4, AppliedTick = 16, PublishedRevision = 4, ReconciledRevision = 4,
                Imports = 4, PublishedPosition = 118, Authority = 118, Prediction = 125,
            }, [3], [17, 18, 19]));
        LocalReplaySample.VerifyAndExport("replay-history-gap", CreateProjectionReplay(scenario, trace, "default"));
    }
}
