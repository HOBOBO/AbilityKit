using AbilityKit.Ability.StateSync.Prediction.Reconciliation;
using AbilityKit.Ability.StateSync.Samples;
using Xunit;
using StateSample = AbilityKit.Ability.StateSync.Samples.LocalStateCommitExample.StateSample;

namespace AbilityKit.World.StateSync.Tests;

public sealed class LocalStateCommitReconciliationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedImport_DoesNotReachReconciliation_AndRetryUsesOnlyCommittedValue(bool throws)
    {
        var host = new LocalHost();
        host.StartWithPrediction();
        var candidate = new StateSample(frame: 1, revision: 2, value: 11);
        var proof = new InputTerminalProof(host.Scope, revision: 2, tick: 1, throughSequence: 1);
        var importFailure = new InvalidOperationException("模拟导入异常");

        if (throws)
            Assert.Same(importFailure, Assert.Throws<InvalidOperationException>(
                () => host.Deliver(candidate, proof, FailImport)));
        else
            Assert.Null(host.Deliver(candidate, proof, FailImport));

        Assert.Equal(new long[] { 1 }, host.ForwardedRevisions);
        Assert.True(host.Commit.TryGetCommitted(out var retained));
        Assert.Equal((0, 1L, 10), (retained.Frame, retained.Revision, retained.Value));
        Assert.Equal(1L, host.Reconciler.AcceptedRevision);
        Assert.Equal(0L, host.Reconciler.TerminalSequence);
        Assert.Equal(new long[] { 1, 2 }, host.PendingSequences());
        Assert.True(host.Reconciler.TryGetAuthoritative(out var oldAuthority));
        Assert.Equal(10, oldAuthority);
        Assert.True(host.Reconciler.TryGetPrediction(out var oldPrediction));
        Assert.Equal(20, oldPrediction);
        Assert.Empty(host.Steps);

        var retried = host.Deliver(candidate, proof, ImportWithAdjustment);
        Assert.Equal(ReconciliationStatus.Predicted, retried?.Status);
        Assert.Equal(new long[] { 1, 2 }, host.ForwardedRevisions);
        Assert.Equal(2L, host.Reconciler.AcceptedRevision);
        Assert.Equal(1L, host.Reconciler.TerminalSequence);
        Assert.Equal(new long[] { 2 }, host.PendingSequences());
        Assert.Equal(new[] { (2, 1), (3, 0) }, host.Steps);
        Assert.True(host.Reconciler.TryGetAuthoritative(out var authority));
        Assert.Equal(14, authority);
        Assert.True(host.Reconciler.TryGetPrediction(out var prediction));
        Assert.Equal(21, prediction);

        bool FailImport(int value, out int imported)
        {
            ImportWithAdjustment(value, out imported);
            if (throws)
                throw importFailure;
            return false;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuccessfulCommit_WithoutMatchingProofUpdatesAuthorityButDoesNotTrimOrReplay(bool supplyStaleProof)
    {
        var host = new LocalHost();
        host.StartWithPrediction();
        var candidate = new StateSample(frame: 1, revision: 2, value: 13);
        InputTerminalProof? proof = supplyStaleProof
            ? new InputTerminalProof(host.Scope, revision: 1, tick: 1, throughSequence: 1)
            : null;

        var result = host.Deliver(candidate, proof, ImportValue);

        Assert.Equal(ReconciliationStatus.InvalidTerminalProof, result?.Status);
        Assert.Equal(2L, host.Reconciler.AcceptedRevision);
        Assert.True(host.Reconciler.TryGetAuthoritative(out var authority));
        Assert.Equal(13, authority);
        Assert.False(host.Reconciler.TryGetPrediction(out _));
        Assert.Equal(0L, host.Reconciler.TerminalSequence);
        Assert.Equal(new long[] { 1, 2 }, host.PendingSequences());
        Assert.Empty(host.Steps);
    }

    [Fact]
    public void InvalidBaseline_OnlySuccessfullyImportedNewFullCommitOpensRecoveryGate()
    {
        var host = new LocalHost();
        host.StartWithPrediction();
        host.Reconciler.InvalidateBaseline(host.Scope);
        AssertWaitingForFull(host);

        var incremental = new StateSample(frame: 1, revision: 2, value: 13);
        var incrementalProof = new InputTerminalProof(host.Scope, revision: 2, tick: 1, throughSequence: 1);
        Assert.Equal(ReconciliationStatus.WaitingForFullBaseline,
            host.Deliver(incremental, incrementalProof, ImportValue)?.Status);
        AssertWaitingForFull(host);

        var oldFull = new StateSample(frame: 0, revision: 1, value: 10);
        Assert.Null(host.Deliver(oldFull, null, UnexpectedImport, isFullBaseline: true));
        AssertWaitingForFull(host);

        var full = new StateSample(frame: 1, revision: 3, value: 11);
        var fullProof = new InputTerminalProof(host.Scope, revision: 3, tick: 1, throughSequence: 1);
        Assert.Null(host.Deliver(full, fullProof, RejectImport, isFullBaseline: true));
        AssertWaitingForFull(host);
        Assert.Equal(new long[] { 1, 2 }, host.ForwardedRevisions);
        Assert.True(host.Commit.TryGetCommitted(out var retained));
        Assert.Equal(2L, retained.Revision);

        Assert.Equal(ReconciliationStatus.Predicted,
            host.Deliver(full, fullProof, ImportWithAdjustment, isFullBaseline: true)?.Status);
        Assert.False(host.Reconciler.WaitingForFullBaseline);
        Assert.Equal(new long[] { 1, 2, 3 }, host.ForwardedRevisions);
        Assert.Equal(3L, host.Reconciler.AcceptedRevision);
        Assert.True(host.Reconciler.TryGetAuthoritative(out var authority));
        Assert.Equal(14, authority);
        Assert.Equal(1L, host.Reconciler.TerminalSequence);
        Assert.Equal(new long[] { 2 }, host.PendingSequences());
        Assert.Equal(new[] { (2, 1), (3, 0) }, host.Steps);
        Assert.True(host.Reconciler.TryGetPrediction(out var prediction));
        Assert.Equal(21, prediction);
    }

    [Fact]
    public void FullCommitWithoutProof_RestoresAuthorityButPredictionWaitsForMatchingProof()
    {
        var host = new LocalHost();
        host.StartWithPrediction();
        host.Reconciler.InvalidateBaseline(host.Scope);
        var full = new StateSample(frame: 1, revision: 2, value: 13);

        Assert.Equal(ReconciliationStatus.InvalidTerminalProof,
            host.Deliver(full, null, ImportValue, isFullBaseline: true)?.Status);
        Assert.False(host.Reconciler.WaitingForFullBaseline);
        Assert.Equal(2L, host.Reconciler.AcceptedRevision);
        Assert.True(host.Reconciler.TryGetAuthoritative(out var authority));
        Assert.Equal(13, authority);
        Assert.False(host.Reconciler.TryGetPrediction(out _));
        Assert.Equal(0L, host.Reconciler.TerminalSequence);
        Assert.Equal(new long[] { 1, 2 }, host.PendingSequences());
        Assert.Empty(host.Steps);

        var incremental = new StateSample(frame: 1, revision: 3, value: 13);
        var proof = new InputTerminalProof(host.Scope, revision: 3, tick: 1, throughSequence: 1);
        Assert.Equal(ReconciliationStatus.Predicted, host.Deliver(incremental, proof, ImportValue)?.Status);
        Assert.Equal(3L, host.Reconciler.AcceptedRevision);
        Assert.Equal(1L, host.Reconciler.TerminalSequence);
        Assert.Equal(new long[] { 2 }, host.PendingSequences());
        Assert.Equal(new[] { (2, 1), (3, 0) }, host.Steps);
        Assert.True(host.Reconciler.TryGetPrediction(out var prediction));
        Assert.Equal(20, prediction);
    }

    private static void AssertWaitingForFull(LocalHost host)
    {
        Assert.True(host.Reconciler.WaitingForFullBaseline);
        Assert.Equal(1L, host.Reconciler.AcceptedRevision);
        Assert.False(host.Reconciler.TryGetAuthoritative(out _));
        Assert.False(host.Reconciler.TryGetPrediction(out _));
        Assert.Equal(0L, host.Reconciler.TerminalSequence);
        Assert.Equal(new long[] { 1, 2 }, host.PendingSequences());
        Assert.Empty(host.Steps);
    }

    private static bool ImportValue(int value, out int imported)
    {
        imported = value;
        return true;
    }

    private static bool ImportWithAdjustment(int value, out int imported) => ImportValue(value + 3, out imported);

    private static bool RejectImport(int value, out int imported)
    {
        imported = value;
        return false;
    }

    private static bool UnexpectedImport(int value, out int imported) =>
        throw new InvalidOperationException("旧全量候选不应进入导入。");

    private sealed class LocalHost
    {
        internal readonly ReconciliationScope Scope = new(1, 2, new Guid(3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), 4, 1, 5, 1);
        internal readonly LocalStateCommitExample Commit = new();
        internal readonly LocalMovementReconciler<int, int> Reconciler;
        internal readonly List<long> ForwardedRevisions = new();
        internal readonly List<(int Tick, int InputCount)> Steps = new();

        internal LocalHost() => Reconciler = new LocalMovementReconciler<int, int>(Scope, Step);

        internal void StartWithPrediction()
        {
            Assert.True(Reconciler.TryRecordInput(Scope, sequence: 1, tick: 1, input: 3));
            Assert.True(Reconciler.TryRecordInput(Scope, sequence: 2, tick: 2, input: 7));
            Assert.True(Reconciler.SealTick(Scope, 1));
            Assert.True(Reconciler.SealTick(Scope, 2));
            Assert.True(Reconciler.SealTick(Scope, 3));

            var initial = new StateSample(frame: 0, revision: 1, value: 10);
            var proof = new InputTerminalProof(Scope, revision: 1, tick: 0, throughSequence: 0);
            Assert.Equal(ReconciliationStatus.Predicted,
                Deliver(initial, proof, ImportValue, isFullBaseline: true)?.Status);
            Assert.Equal(new[] { (1, 1), (2, 1), (3, 0) }, Steps);
            Steps.Clear();
        }

        internal ReconciliationResult? Deliver(StateSample candidate, InputTerminalProof? proof,
            LocalStateCommitExample.TryImportValue import, bool isFullBaseline = false)
        {
            if (!Commit.TryCommit(candidate, import))
                return null;

            Assert.True(Commit.TryGetCommitted(out var committed));
            ForwardedRevisions.Add(committed.Revision);
            return Reconciler.Reconcile(new CommittedMovement<int>(Scope, committed.Revision, committed.Frame,
                committed.Value, isFullBaseline), proof, targetTick: 3);
        }

        internal IEnumerable<long> PendingSequences() =>
            Enumerable.Range(0, Reconciler.PendingCount).Select(Reconciler.GetPendingSequence);

        private bool Step(int tick, ReadOnlySpan<int> inputs, ref int state)
        {
            Steps.Add((tick, inputs.Length));
            foreach (var input in inputs)
                state += input;
            return true;
        }
    }
}
