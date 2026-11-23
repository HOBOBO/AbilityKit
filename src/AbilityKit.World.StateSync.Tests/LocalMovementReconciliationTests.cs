using AbilityKit.Ability.StateSync.Prediction.Reconciliation;
using Xunit;

namespace AbilityKit.World.StateSync.Tests;

public sealed class LocalMovementReconciliationTests
{
    private static readonly Guid Stream = new("1222de7b-96a4-4c1d-8bd0-fcc9f03fc95c");
    private static readonly ReconciliationScope Scope = ScopeFor();

    [Fact]
    public void AcceptedPrefixTrimsAppliedAndRejectedInputsAndReplaysSealedTicksInOrder()
    {
        var visited = new List<(int Tick, int Count)>();
        var reconciler = Create((int tick, ReadOnlySpan<int> inputs, ref int state) =>
        {
            visited.Add((tick, inputs.Length));
            state++;
            foreach (var input in inputs) state = state * 10 + input;
            return true;
        });

        Assert.True(reconciler.TryRecordInput(Scope, 1, 10, 7));
        Assert.True(reconciler.TryRecordInput(Scope, 2, 10, 8));
        Assert.True(reconciler.TryRecordInput(Scope, 3, 11, 2));
        Assert.True(reconciler.TryRecordInput(Scope, 4, 11, 5));
        Assert.False(reconciler.TryRecordInput(Scope, 4, 11, 9));
        Assert.True(reconciler.TryRecordInput(Scope, 5, 13, 3));
        Assert.True(reconciler.SealTick(Scope, 11));
        Assert.True(reconciler.SealTick(Scope, 12));
        Assert.True(reconciler.SealTick(Scope, 13));

        var result = reconciler.Reconcile(Sample(1, 10, 1), Proof(1, 10, 2), 13);

        Assert.Equal(ReconciliationStatus.Predicted, result.Status);
        Assert.Equal(3, result.AttemptedTicks);
        Assert.Equal(new[] { (11, 2), (12, 0), (13, 1) }, visited);
        Assert.True(reconciler.TryGetPrediction(out var position));
        Assert.Equal(2273, position);
        Assert.Equal(new long[] { 3, 4, 5 }, Pending(reconciler));
        Assert.Equal(2L, reconciler.TerminalSequence);
        AssertAuthoritative(reconciler, 1);
    }

    [Fact]
    public void MissingOrMismatchedProofDoesNotTreatEnqueuedInputAsTerminal()
    {
        var reconciler = Create();
        Assert.True(reconciler.TryRecordInput(Scope, 1, 10, 3));
        Assert.True(reconciler.SealTick(Scope, 11));

        Assert.Equal(ReconciliationStatus.InvalidTerminalProof, reconciler.Reconcile(Sample(1, 10, 4), null, 11).Status);
        Assert.Equal(ReconciliationStatus.InvalidTerminalProof, reconciler.Reconcile(Sample(2, 10, 5), Proof(1, 10, 1), 11).Status);
        Assert.Equal(ReconciliationStatus.InvalidTerminalProof, reconciler.Reconcile(Sample(3, 10, 6), Proof(3, 9, 1), 11).Status);
        Assert.Equal(ReconciliationStatus.InvalidTerminalProof, reconciler.Reconcile(Sample(4, 10, 7),
            new InputTerminalProof(ScopeFor(epoch: 2), 4, 10, 1), 11).Status);
        Assert.Equal(0L, reconciler.TerminalSequence);
        Assert.Equal(new long[] { 1 }, Pending(reconciler));
        Assert.False(reconciler.TryGetPrediction(out _));
        AssertAuthoritative(reconciler, 7);

        var pending = reconciler.Reconcile(Sample(5, 10, 8), Proof(5, 10, 0), 11);
        Assert.Equal(ReconciliationStatus.PendingInputBeforeAnchor, pending.Status);
        Assert.Equal(0, pending.AttemptedTicks);
        Assert.Equal(new long[] { 1 }, Pending(reconciler));

        Assert.Equal(ReconciliationStatus.Predicted, reconciler.Reconcile(Sample(6, 10, 9), Proof(6, 10, 1), 11).Status);
        Assert.Equal(0, reconciler.PendingCount);
        Assert.False(reconciler.WaitingForFullBaseline);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    public void OldScopeAndStaleRevisionDoNotReplaceCurrentValuesOrTrimHistory(long lifetime, long epoch)
    {
        var reconciler = Create();
        var newScope = ScopeFor(lifetime, epoch);
        Assert.True(reconciler.TryRecordInput(Scope, 1, 2, 10));
        reconciler.SwitchScope(newScope);
        Assert.Equal(0, reconciler.PendingCount);
        Assert.True(reconciler.TryRecordInput(newScope, 1, 2, 3));
        Assert.True(reconciler.SealTick(newScope, 2));

        var current = new CommittedMovement<int>(newScope, 1, 1, 6);
        Assert.Equal(ReconciliationStatus.Predicted, reconciler.Reconcile(current, new InputTerminalProof(newScope, 1, 1, 0), 2).Status);
        Assert.Equal(ReconciliationStatus.Ignored, reconciler.Reconcile(Sample(99, 1, 99), Proof(99, 1, 9), 2).Status);
        Assert.Equal(ReconciliationStatus.Ignored, reconciler.Reconcile(current, new InputTerminalProof(newScope, 1, 1, 1), 2).Status);
        Assert.Equal(ReconciliationStatus.Ignored, reconciler.Reconcile(new CommittedMovement<int>(newScope, 2, 0, 99),
            new InputTerminalProof(newScope, 2, 0, 1), 2).Status);
        reconciler.InvalidateBaseline(Scope);

        Assert.True(reconciler.TryGetPrediction(out var prediction));
        Assert.Equal(10, prediction);
        AssertAuthoritative(reconciler, 6);
        Assert.Equal(new long[] { 1 }, Pending(reconciler));
    }

    [Fact]
    public void WatermarkRegressionCannotPrunePendingInputsOrReuseOldPrediction()
    {
        var reconciler = Create();
        Assert.True(reconciler.TryRecordInput(Scope, 1, 5, 3));
        Assert.True(reconciler.TryRecordInput(Scope, 2, 7, 4));
        Assert.True(reconciler.SealTick(Scope, 6));
        Assert.Equal(ReconciliationStatus.Predicted, reconciler.Reconcile(Sample(1, 5, 3), Proof(1, 5, 1), 6).Status);

        var result = reconciler.Reconcile(Sample(2, 5, 6), Proof(2, 5, 0), 6);
        Assert.Equal(ReconciliationStatus.InvalidTerminalProof, result.Status);
        Assert.Equal(1L, reconciler.TerminalSequence);
        Assert.Equal(new long[] { 2 }, Pending(reconciler));
        Assert.False(reconciler.TryGetPrediction(out _));
        AssertAuthoritative(reconciler, 6);
    }

    [Fact]
    public void MissingTickAndReplayBudgetAreRejectedBeforeAnyStep()
    {
        var reconciler = Create(replayTickLimit: 2);
        Assert.True(reconciler.SealTick(Scope, 2));
        Assert.True(reconciler.SealTick(Scope, 4));
        Assert.False(reconciler.SealTick(Scope, 2));

        var missing = reconciler.Reconcile(Sample(1, 2, 7), Proof(1, 2, 0), 4);
        Assert.Equal(ReconciliationStatus.MissingTickCoverage, missing.Status);
        Assert.Equal(0, missing.AttemptedTicks);
        var overBudget = reconciler.Reconcile(Sample(2, 2, 8), Proof(2, 2, 0), 5);
        Assert.Equal(ReconciliationStatus.ReplayLimitExceeded, overBudget.Status);
        Assert.Equal(0, overBudget.AttemptedTicks);

        Assert.True(reconciler.SealTick(Scope, 3));
        var recovered = reconciler.Reconcile(Sample(3, 2, 9), Proof(3, 2, 0), 4);
        Assert.Equal(ReconciliationStatus.Predicted, recovered.Status);
        Assert.Equal(2, recovered.AttemptedTicks);
        Assert.False(reconciler.WaitingForFullBaseline);
    }

    [Fact]
    public void BoundedOverflowNeedsBothTerminalSequenceAndTickCoverageBeforeReusingPrediction()
    {
        var reconciler = Create(pendingInputLimit: 1);
        Assert.True(reconciler.TryRecordInput(Scope, 1, 2, 3));
        Assert.False(reconciler.TryRecordInput(Scope, 2, 4, 7));
        Assert.False(reconciler.TryRecordInput(Scope, 3, 3, 8));
        Assert.Equal(new long[] { 1 }, Pending(reconciler));

        Assert.Equal(ReconciliationStatus.UncoveredInput, reconciler.Reconcile(Sample(1, 4, 10), Proof(1, 4, 2), 5).Status);
        Assert.True(reconciler.SealTick(Scope, 5));
        Assert.Equal(ReconciliationStatus.Predicted, reconciler.Reconcile(Sample(2, 4, 20), Proof(2, 4, 3), 5).Status);
        Assert.True(reconciler.TryGetPrediction(out var prediction));
        Assert.Equal(21, prediction);

        var earlierAnchor = Create(pendingInputLimit: 1);
        Assert.True(earlierAnchor.TryRecordInput(Scope, 1, 2, 3));
        Assert.False(earlierAnchor.TryRecordInput(Scope, 2, 4, 7));
        Assert.Equal(ReconciliationStatus.UncoveredInput,
            earlierAnchor.Reconcile(Sample(1, 3, 10), Proof(1, 3, 2), 4).Status);
        Assert.False(earlierAnchor.WaitingForFullBaseline);
    }

    [Fact]
    public void LateInputCannotBeMistakenForASealedEmptyTick()
    {
        var reconciler = Create();
        Assert.True(reconciler.SealTick(Scope, 3));
        Assert.Equal(ReconciliationStatus.Predicted, reconciler.Reconcile(Sample(1, 2, 5), Proof(1, 2, 0), 3).Status);
        Assert.False(reconciler.TryRecordInput(Scope, 1, 3, 10));
        Assert.False(reconciler.TryGetPrediction(out _));
        Assert.Equal(ReconciliationStatus.UncoveredInput, reconciler.Reconcile(Sample(2, 2, 6), Proof(2, 2, 0), 3).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StepFailureDiscardsPartialCandidateButKeepsAuthoritativeCommitAndTerminalTrim(bool throws)
    {
        var error = new InvalidOperationException("step failed");
        var reconciler = Create((int tick, ReadOnlySpan<int> inputs, ref int state) =>
        {
            state += 100;
            if (tick == 6 && throws) throw error;
            return tick != 6;
        });
        Assert.True(reconciler.TryRecordInput(Scope, 1, 4, 3));
        Assert.True(reconciler.TryRecordInput(Scope, 2, 5, 4));
        Assert.True(reconciler.SealTick(Scope, 5));
        Assert.True(reconciler.SealTick(Scope, 6));
        Assert.True(reconciler.SealTick(Scope, 7));

        var result = reconciler.Reconcile(Sample(1, 4, 8), Proof(1, 4, 1), 7);

        Assert.Equal(ReconciliationStatus.StepFailed, result.Status);
        Assert.Equal(2, result.AttemptedTicks);
        Assert.Same(throws ? error : null, result.StepException);
        Assert.False(reconciler.TryGetPrediction(out _));
        Assert.False(reconciler.WaitingForFullBaseline);
        AssertAuthoritative(reconciler, 8);
        Assert.Equal(new long[] { 2 }, Pending(reconciler));
    }

    [Fact]
    public void InvalidBaselineClearsPredictionAndOnlyNewCommittedFullReopensIt()
    {
        var reconciler = Create();
        Assert.True(reconciler.TryRecordInput(Scope, 1, 8, 10));
        Assert.True(reconciler.SealTick(Scope, 8));
        Assert.Equal(ReconciliationStatus.Predicted, reconciler.Reconcile(Sample(1, 7, 2, full: true), Proof(1, 7, 0), 8).Status);

        reconciler.InvalidateBaseline(Scope);
        Assert.False(reconciler.TryGetPrediction(out _));
        Assert.False(reconciler.TryGetAuthoritative(out _));
        Assert.Equal(ReconciliationStatus.WaitingForFullBaseline, reconciler.Reconcile(Sample(2, 8, 12), Proof(2, 8, 1), 8).Status);
        Assert.Equal(ReconciliationStatus.Ignored, reconciler.Reconcile(Sample(1, 7, 2, full: true), Proof(1, 7, 1), 8).Status);
        Assert.True(reconciler.WaitingForFullBaseline);
        Assert.Equal(1L, reconciler.AcceptedRevision);
        Assert.Equal(new long[] { 1 }, Pending(reconciler));

        Assert.Equal(ReconciliationStatus.InvalidTerminalProof, reconciler.Reconcile(Sample(2, 8, 12, full: true), null, 8).Status);
        Assert.False(reconciler.WaitingForFullBaseline);
        AssertAuthoritative(reconciler, 12);
        Assert.Equal(new long[] { 1 }, Pending(reconciler));
    }

    [Fact]
    public void DefaultsBoundHistoryAndSingleReplayIndependently()
    {
        var reconciler = new LocalMovementReconciler<int, int>(Scope, Step);
        for (var sequence = 1; sequence <= 64; sequence++)
            Assert.True(reconciler.TryRecordInput(Scope, sequence, 1, 0));
        Assert.False(reconciler.TryRecordInput(Scope, 65, 1, 0));
        Assert.Equal(64, reconciler.PendingCount);

        for (var tick = 2; tick <= 33; tick++)
            Assert.True(reconciler.SealTick(Scope, tick));
        Assert.Equal(ReconciliationStatus.ReplayLimitExceeded,
            reconciler.Reconcile(Sample(1, 1, 0), Proof(1, 1, 65), 34).Status);
        var bounded = reconciler.Reconcile(Sample(2, 1, 0), Proof(2, 1, 65), 33);
        Assert.Equal(ReconciliationStatus.Predicted, bounded.Status);
        Assert.Equal(32, bounded.AttemptedTicks);
    }

    private static ReconciliationScope ScopeFor(long lifetime = 1, long epoch = 1) => new(1, 2, Stream, 3, lifetime, 4, epoch);
    private static CommittedMovement<int> Sample(long revision, int tick, int position, bool full = false) => new(Scope, revision, tick, position, full);
    private static InputTerminalProof Proof(long revision, int tick, long sequence) => new(Scope, revision, tick, sequence);

    private static LocalMovementReconciler<int, int> Create(TryMovementStep<int, int>? step = null,
        int replayTickLimit = 32, int pendingInputLimit = 64) => new(Scope, step ?? Step, replayTickLimit, pendingInputLimit);

    private static bool Step(int tick, ReadOnlySpan<int> inputs, ref int state)
    {
        state++;
        foreach (var input in inputs) state += input;
        return true;
    }

    private static long[] Pending(LocalMovementReconciler<int, int> reconciler) =>
        Enumerable.Range(0, reconciler.PendingCount).Select(reconciler.GetPendingSequence).ToArray();

    private static void AssertAuthoritative(LocalMovementReconciler<int, int> reconciler, int expected)
    {
        Assert.True(reconciler.TryGetAuthoritative(out var state));
        Assert.Equal(expected, state);
    }
}
