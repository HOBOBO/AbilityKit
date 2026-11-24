using AbilityKit.Ability.StateSync.Prediction.Reconciliation;
using AbilityKit.Ability.StateSync.Samples;
using Xunit;
using StateSample = AbilityKit.Ability.StateSync.Samples.LocalStateCommitExample.StateSample;

namespace AbilityKit.World.StateSync.Tests;

public sealed partial class LocalCommittedStateResultTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FailedCommitStep_DoesNotPublishOrConsumeProof(bool failAdmission)
    {
        var host = new LocalCommitHost();
        var candidate = new StateSample(frame: 1, revision: 2, value: 10);
        var proof = new InputTerminalProof(host.Scope, revision: 2, tick: 1, throughSequence: 1);
        var failure = failAdmission ? Stage.CommitAdmission : Stage.CommitBaseline;

        Assert.Null(host.Deliver(candidate, proof, full: true, failingStage: failure));

        Assert.Equal(failAdmission
            ? new[] { Stage.Import, Stage.CommitAdmission }
            : new[] { Stage.Import, Stage.CommitAdmission, Stage.CommitBaseline }, host.Attempted);
        Assert.True(host.Admission.TryGetCommitted(out var cursor));
        Assert.Equal(failAdmission ? 1L : 2L, cursor.Revision);
        Assert.Equal(1L, host.Materialized?.Revision);
        Assert.Equal(10, host.Materialized?.Value);
        AssertWaitingWithoutConsumption(host);
    }

    [Fact]
    public void PartialCommit_WaitsForNewFull_AndRecoveryDoesNotRequireTerminalProof()
    {
        var host = new LocalCommitHost();
        var failed = new StateSample(frame: 1, revision: 2, value: 10);
        var failedProof = new InputTerminalProof(host.Scope, revision: 2, tick: 1, throughSequence: 1);
        Assert.Null(host.Deliver(failed, failedProof, full: true, failingStage: Stage.CommitBaseline));
        host.Attempted.Clear();

        Assert.Null(host.Deliver(failed, failedProof, full: true));
        var incremental = new StateSample(frame: 1, revision: 3, value: 11);
        var incrementalProof = new InputTerminalProof(host.Scope, revision: 3, tick: 1, throughSequence: 1);
        Assert.Null(host.Deliver(incremental, incrementalProof, full: false));
        Assert.Empty(host.Attempted);
        AssertWaitingWithoutConsumption(host);

        Assert.Equal(ReconciliationStatus.InvalidTerminalProof, host.Deliver(incremental, null, full: true)?.Status);

        Assert.Equal(3L, Assert.Single(host.Published).Revision);
        Assert.False(host.Reconciler.WaitingForFullBaseline);
        Assert.True(host.Reconciler.TryGetAuthoritative(out var authority));
        Assert.Equal(14, authority);
        Assert.False(host.Reconciler.TryGetPrediction(out _));
        Assert.Equal(0L, host.Reconciler.TerminalSequence);
        Assert.Equal(new long[] { 1, 2 }, host.PendingSequences());
        Assert.Empty(host.SteppedTicks);
    }

    [Fact]
    public void CompleteCommit_PublishesImportedValuesOncePerRevision_IncludingAtTheSameTick()
    {
        var host = new LocalCommitHost();
        var first = new StateSample(frame: 1, revision: 2, value: 10);
        var firstProof = new InputTerminalProof(host.Scope, revision: 2, tick: 1, throughSequence: 1);
        Assert.Equal(ReconciliationStatus.Predicted, host.Deliver(first, firstProof, full: false)?.Status);

        Assert.Equal(new[] { Stage.Import, Stage.CommitAdmission, Stage.CommitBaseline, Stage.Publish }, host.Attempted);
        var published = Assert.Single(host.Published);
        Assert.Equal((2L, 1, 13, false), (published.Revision, published.Tick, published.State, published.IsFullBaseline));
        Assert.Equal(1L, host.Reconciler.TerminalSequence);
        Assert.Equal(new long[] { 2 }, host.PendingSequences());
        Assert.Equal(new[] { 2, 3 }, host.SteppedTicks);

        host.Attempted.Clear();
        Assert.Null(host.Deliver(first, firstProof, full: false));
        Assert.Empty(host.Attempted);
        Assert.Single(host.Published);

        var next = new StateSample(frame: 1, revision: 3, value: 11);
        Assert.Equal(ReconciliationStatus.InvalidTerminalProof, host.Deliver(next, null, full: false)?.Status);
        Assert.Equal(new long[] { 2, 3 }, host.Published.Select(result => result.Revision));
        Assert.Equal(3L, host.Materialized?.Revision);
        Assert.Equal(1L, host.Reconciler.TerminalSequence);
        Assert.Equal(new long[] { 2 }, host.PendingSequences());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OldBaselineCompletionAfterSwitch_DoesNotPublishOrInvalidateCurrentSession(bool succeeds)
    {
        var host = new LocalCommitHost();
        var previous = host.Session;
        var oldFull = new StateSample(frame: 1, revision: 2, value: 10);
        var oldProof = new InputTerminalProof(previous.Scope, revision: 2, tick: 1, throughSequence: 1);
        Stage? failingStage = succeeds ? null : Stage.CommitBaseline;

        Assert.Null(host.Deliver(previous.Scope, oldFull, oldProof, full: true, failingStage: failingStage,
            beforeBaselineCompletes: () =>
            {
                Assert.True(previous.Admission.TryGetCommitted(out var pending));
                Assert.Equal(2L, pending.Revision);
                Assert.Equal(1L, previous.Materialized?.Revision);
                Assert.Empty(host.Published);
                host.SwitchSession(generation: 2, initialValue: 97);
            }));

        Assert.Equal(succeeds ? 2L : 1L, previous.Materialized?.Revision);
        AssertCurrentSessionUnchanged(host);
    }

    [Fact]
    public void CandidateFromOldSessionArrivingAfterSwitch_DoesNotEnterImport()
    {
        var host = new LocalCommitHost();
        var previous = host.Session;
        host.SwitchSession(generation: 2, initialValue: 97);
        host.Attempted.Clear();
        var oldFull = new StateSample(frame: 1, revision: 2, value: 10);
        var oldProof = new InputTerminalProof(previous.Scope, revision: 2, tick: 1, throughSequence: 1);

        Assert.Null(host.Deliver(previous.Scope, oldFull, oldProof, full: true));

        Assert.Empty(host.Attempted);
        Assert.True(previous.Admission.TryGetCommitted(out var oldCursor));
        Assert.Equal(1L, oldCursor.Revision);
        AssertCurrentSessionUnchanged(host);
    }

    private static void AssertCurrentSessionUnchanged(LocalCommitHost host)
    {
        var published = Assert.Single(host.Published);
        Assert.True(published.Scope.Matches(host.Scope));
        Assert.Equal((1L, 0, 100), (published.Revision, published.Tick, published.State));
        Assert.True(host.Admission.TryGetCommitted(out var cursor));
        Assert.Equal((1L, 0, 100), (cursor.Revision, cursor.Frame, cursor.Value));
        Assert.Equal((1L, 0, 100), (host.Materialized?.Revision, host.Materialized?.Frame, host.Materialized?.Value));
        Assert.Equal(1L, host.Reconciler.AcceptedRevision);
        Assert.False(host.Reconciler.WaitingForFullBaseline);
        Assert.Equal(0L, host.Reconciler.TerminalSequence);
        Assert.Equal(new long[] { 1, 2 }, host.PendingSequences());
        Assert.True(host.Reconciler.TryGetAuthoritative(out var authority));
        Assert.Equal(100, authority);
        Assert.True(host.Reconciler.TryGetPrediction(out var prediction));
        Assert.Equal(110, prediction);
        Assert.Equal(new[] { 1, 2, 3 }, host.SteppedTicks);
    }

    private static void AssertWaitingWithoutConsumption(LocalCommitHost host)
    {
        Assert.Empty(host.Published);
        Assert.Equal(1L, host.Reconciler.AcceptedRevision);
        Assert.True(host.Reconciler.WaitingForFullBaseline);
        Assert.False(host.Reconciler.TryGetAuthoritative(out _));
        Assert.False(host.Reconciler.TryGetPrediction(out _));
        Assert.Equal(0L, host.Reconciler.TerminalSequence);
        Assert.Equal(new long[] { 1, 2 }, host.PendingSequences());
        Assert.Empty(host.SteppedTicks);
    }

    private enum Stage { Import, CommitAdmission, CommitBaseline, Publish }

    private sealed class LocalCommitHost
    {
        internal readonly LocalMovementReconciler<int, int> Reconciler;
        internal readonly List<Stage> Attempted = new();
        internal readonly List<CommittedMovement<int>> Published = new();
        internal readonly List<CommitReplayStep> ReplaySteps = new();
        internal IEnumerable<int> SteppedTicks => ReplaySteps.Select(step => step.Tick);
        internal SessionState Session { get; private set; } = new(generation: 1);
        internal ReconciliationScope Scope => Session.Scope;
        internal LocalStateCommitExample Admission => Session.Admission;
        internal StateSample? Materialized => Session.Materialized;

        internal LocalCommitHost()
        {
            Reconciler = new LocalMovementReconciler<int, int>(Scope, Step);
            Seed(initialValue: 7);
            Attempted.Clear();
            Published.Clear();
            ReplaySteps.Clear();
        }

        internal void SwitchSession(long generation, int initialValue)
        {
            Session = new SessionState(generation);
            Reconciler.SwitchScope(Scope);
            Seed(initialValue);
        }

        private void Seed(int initialValue)
        {
            Assert.True(Reconciler.TryRecordInput(Scope, sequence: 1, tick: 1, input: 3));
            Assert.True(Reconciler.TryRecordInput(Scope, sequence: 2, tick: 2, input: 7));
            for (var tick = 1; tick <= 3; tick++)
                Assert.True(Reconciler.SealTick(Scope, tick));

            var initial = new StateSample(frame: 0, revision: 1, value: initialValue);
            var proof = new InputTerminalProof(Scope, revision: 1, tick: 0, throughSequence: 0);
            Assert.Equal(ReconciliationStatus.Predicted, Deliver(initial, proof, full: true)?.Status);
        }

        internal ReconciliationResult? Deliver(StateSample candidate, InputTerminalProof? proof, bool full,
            Stage? failingStage = null, int targetTick = 3) => Deliver(Scope, candidate, proof, full, failingStage, targetTick: targetTick);

        internal ReconciliationResult? Deliver(ReconciliationScope source, StateSample candidate, InputTerminalProof? proof,
            bool full, Stage? failingStage = null, Action? beforeBaselineCompletes = null, int targetTick = 3)
        {
            var session = Session;
            if (!session.Scope.Matches(source) || (Reconciler.WaitingForFullBaseline && !full))
                return null;

            var imported = false;
            if (!session.Admission.TryCommit(candidate, ImportAndCommitAdmission))
            {
                if (imported && ReferenceEquals(Session, session))
                    Reconciler.InvalidateBaseline(source);
                return null;
            }

            Assert.True(session.Admission.TryGetCommitted(out var admitted));
            Attempted.Add(Stage.CommitBaseline);
            beforeBaselineCompletes?.Invoke();
            var baselineCommitted = session.TryCommitBaseline(admitted, failingStage != Stage.CommitBaseline);
            if (!ReferenceEquals(Session, session))
                return null;
            if (!baselineCommitted)
            {
                Reconciler.InvalidateBaseline(source);
                return null;
            }

            var materialized = session.Materialized!.Value;
            var result = new CommittedMovement<int>(source, admitted.Revision, admitted.Frame, materialized.Value, full);
            Attempted.Add(Stage.Publish);
            Published.Add(result);
            return Reconciler.Reconcile(result, proof, targetTick);

            bool ImportAndCommitAdmission(int value, out int importedValue)
            {
                Attempted.Add(Stage.Import);
                importedValue = value + 3;
                if (failingStage == Stage.Import)
                    return false;
                imported = true;
                Attempted.Add(Stage.CommitAdmission);
                return failingStage != Stage.CommitAdmission;
            }
        }

        internal IEnumerable<long> PendingSequences() =>
            Enumerable.Range(0, Reconciler.PendingCount).Select(Reconciler.GetPendingSequence);

        private bool Step(int tick, ReadOnlySpan<int> inputs, ref int state)
        {
            var before = state;
            foreach (var input in inputs)
                state += input;
            ReplaySteps.Add(new CommitReplayStep(tick, before, state));
            return true;
        }
    }

    private sealed class SessionState(long generation)
    {
        internal readonly ReconciliationScope Scope = new(generation, 2, new Guid(3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), 4, 1, 5, 1);
        internal readonly LocalStateCommitExample Admission = new();
        internal StateSample? Materialized { get; private set; }

        internal bool TryCommitBaseline(StateSample admitted, bool succeeds)
        {
            if (succeeds)
                Materialized = admitted;
            return succeeds;
        }
    }
}
