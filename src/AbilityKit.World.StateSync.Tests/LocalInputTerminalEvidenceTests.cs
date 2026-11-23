using AbilityKit.Ability.StateSync.Prediction.Reconciliation;
using Xunit;

namespace AbilityKit.World.StateSync.Tests;

public sealed class LocalInputTerminalEvidenceTests
{
    [Fact]
    public void Reordered_execution_cannot_trim_pending_inputs_until_a_committed_prefix_exists()
    {
        var scope = new ReconciliationScope(1, 2, new Guid(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), 3, 1, 4, 1);
        var ledger = new InMemoryTerminalFixture(scope);
        var reconciler = new LocalMovementReconciler<int, int>(scope, KeepState);
        Assert.True(reconciler.TryRecordInput(scope, 1, 2, 10));
        Assert.True(reconciler.TryRecordInput(scope, 2, 2, 20));
        Assert.True(reconciler.TryRecordInput(scope, 3, 3, 30));
        Assert.True(reconciler.SealTick(scope, 2));
        Assert.True(reconciler.SealTick(scope, 3));

        ledger.Stage(2, Outcome.Queued, tick: 2);
        ledger.Stage(3, Outcome.Queued, tick: 2);
        var queued = ledger.Capture(revision: 1, completedTick: 2);
        Assert.Equal(0L, queued.Proof!.Value.ThroughSequence);
        Assert.Equal(ReconciliationStatus.PendingInputBeforeAnchor,
            reconciler.Reconcile(queued.Sample, queued.Proof, 3).Status);

        ledger.Stage(2, Outcome.Rescheduled, tick: 3);
        ledger.Stage(3, Outcome.Applied, tick: 3);
        var unfinished = ledger.Capture(revision: 2, completedTick: 2);
        Assert.Equal(0L, unfinished.Proof!.Value.ThroughSequence);
        Assert.Equal(0, unfinished.Sample.State);
        var outOfOrder = ledger.Capture(revision: 3, completedTick: 3);
        Assert.Equal(1, outOfOrder.Sample.State);
        Assert.Null(outOfOrder.Proof);
        Assert.Equal(ReconciliationStatus.InvalidTerminalProof,
            reconciler.Reconcile(outOfOrder.Sample, outOfOrder.Proof, 3).Status);
        Assert.False(reconciler.TryGetPrediction(out _));
        Assert.True(reconciler.TryGetAuthoritative(out var authoritative));
        Assert.Equal(1, authoritative);
        AssertPending(reconciler, 1, 2, 3);

        ledger.Stage(1, Outcome.Rejected, tick: 4);
        Assert.Null(ledger.Capture(revision: 4, completedTick: 3).Proof);
        Assert.True(reconciler.SealTick(scope, 4));
        Assert.True(reconciler.SealTick(scope, 5));
        var rejected = ledger.Capture(revision: 5, completedTick: 4);
        Assert.Null(rejected.Proof);
        Assert.Equal(ReconciliationStatus.InvalidTerminalProof,
            reconciler.Reconcile(rejected.Sample, rejected.Proof, 5).Status);
        AssertPending(reconciler, 1, 2, 3);

        ledger.Stage(2, Outcome.Applied, tick: 5);
        Assert.Null(ledger.Capture(revision: 6, completedTick: 4).Proof);
        Assert.True(reconciler.SealTick(scope, 6));
        var complete = ledger.Capture(revision: 7, completedTick: 5);
        Assert.Equal(3L, complete.Proof!.Value.ThroughSequence);
        Assert.Equal(ReconciliationStatus.Predicted,
            reconciler.Reconcile(complete.Sample, complete.Proof, 6).Status);
        Assert.Equal(3L, reconciler.TerminalSequence);
        Assert.True(reconciler.TryGetPrediction(out var prediction));
        Assert.Equal(2, prediction);
        AssertPending(reconciler);
    }

    private static bool KeepState(int tick, ReadOnlySpan<int> inputs, ref int state) => inputs.IsEmpty;

    private static void AssertPending(LocalMovementReconciler<int, int> reconciler, params long[] expected) =>
        Assert.Equal(expected, Enumerable.Range(0, reconciler.PendingCount).Select(reconciler.GetPendingSequence));

    private enum Outcome { Queued, Rescheduled, Applied, Rejected }

    private sealed class InMemoryTerminalFixture(ReconciliationScope scope)
    {
        private readonly List<(long Sequence, Outcome Outcome, int Tick)> _entries = new();

        internal void Stage(long sequence, Outcome outcome, int tick) => _entries.Add((sequence, outcome, tick));

        internal (CommittedMovement<int> Sample, InputTerminalProof? Proof) Capture(long revision, int completedTick)
        {
            var terminal = _entries.Where(entry => entry.Tick <= completedTick &&
                    entry.Outcome is Outcome.Applied or Outcome.Rejected)
                .ToDictionary(entry => entry.Sequence, entry => entry.Outcome);
            long prefix = 0;
            while (terminal.ContainsKey(prefix + 1)) prefix++;

            var sample = new CommittedMovement<int>(scope, revision, completedTick,
                terminal.Values.Count(outcome => outcome == Outcome.Applied));
            var includesNonPrefixEffect = terminal.Any(entry => entry.Key > prefix && entry.Value == Outcome.Applied);
            return (sample, includesNonPrefixEffect ? null : new InputTerminalProof(scope, revision, completedTick, prefix));
        }
    }
}
