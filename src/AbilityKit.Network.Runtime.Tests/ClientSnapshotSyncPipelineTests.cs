using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class ClientSnapshotSyncPipelineTests
{
    [Fact]
    public void FullBaselineAndDeltaApplyCommitAndPublishStandardHealthEvents()
    {
        var applied = new List<int>();
        var pipeline = CreatePipeline(applied);
        var baseline = Snapshot.Baseline(sequence: 10, frame: 10, stateHash: 100u, entityCount: 3);
        var delta = Snapshot.Delta(sequence: 11, frame: 11, baseline, stateHash: 101u, entityCount: 2);

        var baselineResult = pipeline.Apply(in baseline);
        var deltaResult = pipeline.Apply(in delta);

        Assert.Equal(ClientSnapshotSyncStatus.AppliedFullBaseline, baselineResult.Status);
        Assert.Equal(ClientSnapshotSyncStatus.AppliedDelta, deltaResult.Status);
        Assert.Equal(new[] { 10, 11 }, applied);
        Assert.Equal(11, pipeline.State.LastAppliedFrame);
        Assert.Equal(101u, pipeline.State.LastAppliedStateHash);
        Assert.Contains(baselineResult.HealthEvents, e =>
            e.Kind == SyncHealthEventKind.SnapshotReceived && e.Value == 3);
        Assert.Contains(baselineResult.HealthEvents, e =>
            e.Kind == SyncHealthEventKind.FullSnapshotApplied && e.Value == 10);
        Assert.Single(deltaResult.HealthEvents);
        Assert.Equal(SyncHealthEventKind.SnapshotReceived, deltaResult.HealthEvents[0].Kind);
        Assert.Equal(2, deltaResult.HealthEvents[0].Value);
    }

    [Fact]
    public void MissingBaselineRequestsRecoveryWithoutApplying()
    {
        var applied = new List<int>();
        var pipeline = CreatePipeline(applied);
        var delta = new Snapshot(1ul, 1, 5, 5, false, 1, 100u, 101u, 1, 1);

        var result = pipeline.Apply(in delta);

        Assert.Equal(ClientSnapshotSyncStatus.NeedsFullBaseline, result.Status);
        Assert.Equal(SnapshotStreamRecoveryReason.MissingBaseline, result.Validation.RecoveryReason);
        Assert.Empty(applied);
        Assert.True(pipeline.State.NeedsFullBaselineRecovery);
        Assert.Contains(result.HealthEvents, e => e.Kind == SyncHealthEventKind.FullSnapshotRequested);
    }

    [Fact]
    public void BaselineMismatchAndSequenceGapUseSameRecoveryContract()
    {
        var pipeline = CreatePipeline(new List<int>());
        var baseline = Snapshot.Baseline(sequence: 10, frame: 10, stateHash: 100u);
        pipeline.Apply(in baseline);
        var mismatch = Snapshot.Delta(sequence: 11, frame: 11, baseline, stateHash: 101u) with
        {
            BaselineHash = 999u
        };

        var mismatchResult = pipeline.Apply(in mismatch);

        Assert.Equal(ClientSnapshotSyncStatus.NeedsFullBaseline, mismatchResult.Status);
        Assert.Equal(SnapshotStreamRecoveryReason.BaselineMismatch, mismatchResult.Validation.RecoveryReason);

        pipeline.Reset();
        pipeline.Apply(in baseline);
        var gap = Snapshot.Delta(sequence: 13, frame: 13, baseline, stateHash: 103u, maximumSequenceAdvance: 2);
        var gapResult = pipeline.Apply(in gap);

        Assert.Equal(ClientSnapshotSyncStatus.NeedsFullBaseline, gapResult.Status);
        Assert.Equal(SnapshotStreamRecoveryReason.SequenceGap, gapResult.Validation.RecoveryReason);
        Assert.Equal(2, gapResult.HealthEvents.Count);
        Assert.Equal(SyncHealthEventKind.SnapshotGap, gapResult.HealthEvents[0].Kind);
        Assert.Equal(SyncHealthSeverity.Error, gapResult.HealthEvents[0].Severity);
        Assert.Equal(SyncHealthEventKind.FullSnapshotRequested, gapResult.HealthEvents[1].Kind);
    }

    [Fact]
    public void DuplicateAndUnsupportedVersionAreRejectedWithoutApplying()
    {
        var applied = new List<int>();
        var pipeline = CreatePipeline(applied);
        var baseline = Snapshot.Baseline(sequence: 10, frame: 10, stateHash: 100u);
        pipeline.Apply(in baseline);
        var unsupported = Snapshot.Baseline(sequence: 11, frame: 11, stateHash: 101u) with { Version = 2 };

        var duplicateResult = pipeline.Apply(in baseline);
        var unsupportedResult = pipeline.Apply(in unsupported);

        Assert.Equal(ClientSnapshotSyncStatus.IgnoredStale, duplicateResult.Status);
        Assert.Equal(SyncHealthEventKind.SnapshotStale, duplicateResult.HealthEvents[0].Kind);
        Assert.Equal(ClientSnapshotSyncStatus.UnsupportedVersion, unsupportedResult.Status);
        Assert.Equal(SyncHealthEventKind.SnapshotDropped, unsupportedResult.HealthEvents[0].Kind);
        Assert.Equal(new[] { 10 }, applied);
    }

    [Fact]
    public void ApplyExceptionDoesNotCommitAuthoritativeCursor()
    {
        var throwOnApply = true;
        var pipeline = CreatePipeline(
            new List<int>(),
            (in Snapshot _) =>
            {
                if (throwOnApply) throw new InvalidOperationException("projection failed");
            });
        var baseline = Snapshot.Baseline(sequence: 10, frame: 10, stateHash: 100u);

        Assert.Throws<InvalidOperationException>(() => pipeline.Apply(in baseline));
        Assert.False(pipeline.State.HasAppliedSnapshot);
        Assert.Equal(0, pipeline.State.LastAppliedFrame);

        throwOnApply = false;
        var result = pipeline.Apply(in baseline);
        Assert.Equal(ClientSnapshotSyncStatus.AppliedFullBaseline, result.Status);
        Assert.Equal(10, pipeline.State.LastAppliedFrame);
    }

    [Fact]
    public void ResetRequiresANewBaseline()
    {
        var pipeline = CreatePipeline(new List<int>());
        var baseline = Snapshot.Baseline(sequence: 10, frame: 10, stateHash: 100u);
        var delta = Snapshot.Delta(sequence: 11, frame: 11, baseline, stateHash: 101u);
        pipeline.Apply(in baseline);
        pipeline.Reset();

        var result = pipeline.Apply(in delta);

        Assert.Equal(ClientSnapshotSyncStatus.NeedsFullBaseline, result.Status);
        Assert.Equal(SnapshotStreamRecoveryReason.MissingBaseline, result.Validation.RecoveryReason);
    }

    [Fact]
    public void OptionsValidateRequiredDelegatesAndVersionRange()
    {
        var valid = CreateOptions(new List<int>());
        valid.Validate();

        var negativeVersion = CreateOptions(new List<int>());
        negativeVersion.MinimumSupportedVersion = -1;
        Assert.Throws<ArgumentOutOfRangeException>(negativeVersion.Validate);

        var reversedRange = CreateOptions(new List<int>());
        reversedRange.MaximumSupportedVersion = 0;
        Assert.Throws<ArgumentOutOfRangeException>(reversedRange.Validate);

        var missingEnvelope = CreateOptions(new List<int>());
        missingEnvelope.CreateEnvelope = null;
        Assert.Throws<ArgumentNullException>(missingEnvelope.Validate);

        var missingApply = CreateOptions(new List<int>());
        missingApply.ApplySnapshot = null;
        Assert.Throws<ArgumentNullException>(missingApply.Validate);
    }

    [Fact]
    public void PipelineSnapshotsOptionsAtConstruction()
    {
        var originalApplied = new List<int>();
        var replacementApplied = new List<int>();
        var options = CreateOptions(originalApplied);
        var pipeline = new ClientSnapshotSyncPipeline<Snapshot>(options);
        options.ApplySnapshot = (in Snapshot snapshot) => replacementApplied.Add(snapshot.Frame);
        options.MinimumSupportedVersion = 99;

        var baseline = Snapshot.Baseline(sequence: 10, frame: 10, stateHash: 100u);
        var result = pipeline.Apply(in baseline);

        Assert.True(result.Applied);
        Assert.Equal(new[] { 10 }, originalApplied);
        Assert.Empty(replacementApplied);
    }

    [Fact]
    public void RecoveryStrategyCanSelectKeyFrameAndDispatchProjectHandler()
    {
        var requests = new List<SnapshotRecoveryRequest>();
        var options = CreateOptions(new List<int>());
        options.RecoveryStrategy = static (in Snapshot _, in SnapshotStreamValidationResult _) =>
            SnapshotRecoveryRequestKind.KeyFrame;
        options.RecoveryHandler = (in Snapshot _, in SnapshotRecoveryRequest request) => requests.Add(request);
        var pipeline = new ClientSnapshotSyncPipeline<Snapshot>(options);
        var delta = new Snapshot(1ul, 1, 5, 5, false, 1, 100u, 101u, 1, 1);

        var result = pipeline.Apply(in delta);

        var request = Assert.Single(requests);
        Assert.Equal(SnapshotRecoveryRequestKind.KeyFrame, request.Kind);
        Assert.Equal(SnapshotStreamRecoveryReason.MissingBaseline, request.Reason);
        Assert.Equal(5, request.Envelope.Frame);
        Assert.Contains(result.HealthEvents, e => e.Kind == SyncHealthEventKind.KeyFrameRequested);
        Assert.DoesNotContain(result.HealthEvents, e => e.Kind == SyncHealthEventKind.FullSnapshotRequested);
    }

    [Fact]
    public void RecoveryStrategyCanSuppressExternalRequestWhileStateStillRequiresBaseline()
    {
        var handlerCalls = 0;
        var options = CreateOptions(new List<int>());
        options.RecoveryStrategy = static (in Snapshot _, in SnapshotStreamValidationResult _) =>
            SnapshotRecoveryRequestKind.None;
        options.RecoveryHandler = (in Snapshot _, in SnapshotRecoveryRequest _) => handlerCalls++;
        var pipeline = new ClientSnapshotSyncPipeline<Snapshot>(options);
        var delta = new Snapshot(1ul, 1, 5, 5, false, 1, 100u, 101u, 1, 1);

        var result = pipeline.Apply(in delta);

        Assert.Equal(ClientSnapshotSyncStatus.NeedsFullBaseline, result.Status);
        Assert.True(pipeline.State.NeedsFullBaselineRecovery);
        Assert.Equal(0, handlerCalls);
        Assert.Empty(result.HealthEvents);
    }

    [Theory]
    [InlineData(SnapshotRecoveryRequestKind.AoiSlice, SyncHealthEventKind.AoiSliceRequested)]
    [InlineData(SnapshotRecoveryRequestKind.Custom, SyncHealthEventKind.None)]
    public void RecoveryStrategySupportsAoiAndProjectSpecificActions(
        SnapshotRecoveryRequestKind requestKind,
        SyncHealthEventKind expectedStandardEvent)
    {
        SnapshotRecoveryRequestKind? dispatched = null;
        var options = CreateOptions(new List<int>());
        options.RecoveryStrategy = (in Snapshot _, in SnapshotStreamValidationResult _) => requestKind;
        options.RecoveryHandler = (in Snapshot _, in SnapshotRecoveryRequest request) => dispatched = request.Kind;
        var pipeline = new ClientSnapshotSyncPipeline<Snapshot>(options);
        var delta = new Snapshot(1ul, 1, 5, 5, false, 1, 100u, 101u, 1, 1);

        var result = pipeline.Apply(in delta);

        Assert.Equal(requestKind, dispatched);
        if (expectedStandardEvent == SyncHealthEventKind.None)
        {
            Assert.Empty(result.HealthEvents);
        }
        else
        {
            Assert.Contains(result.HealthEvents, e => e.Kind == expectedStandardEvent);
        }
    }

    [Fact]
    public void HealthEventPolicyCanDecorateFrameworkDefaults()
    {
        IReadOnlyList<SyncHealthEvent>? observedDefaults = null;
        var options = CreateOptions(new List<int>());
        options.HealthEventPolicy =
            (in ClientSnapshotSyncEventContext<Snapshot> context, IReadOnlyList<SyncHealthEvent> standardEvents) =>
            {
                observedDefaults = standardEvents;
                return new[]
                {
                    standardEvents[0],
                    SyncHealthEvent.Info(SyncHealthEventKind.ObserverSnapshotQueued, context.Envelope.Frame, context.EntityCount)
                };
            };
        var pipeline = new ClientSnapshotSyncPipeline<Snapshot>(options);
        var baseline = Snapshot.Baseline(sequence: 10, frame: 10, stateHash: 100u, entityCount: 3);

        var result = pipeline.Apply(in baseline);

        Assert.NotNull(observedDefaults);
        Assert.Equal(2, observedDefaults!.Count);
        Assert.Equal(2, result.HealthEvents.Count);
        Assert.Equal(SyncHealthEventKind.SnapshotReceived, result.HealthEvents[0].Kind);
        Assert.Equal(SyncHealthEventKind.ObserverSnapshotQueued, result.HealthEvents[1].Kind);
        Assert.Equal(3, result.HealthEvents[1].Value);
    }

    [Fact]
    public void ObserverSeesRecoveryBeforeResultAndCommittedStateAfterApply()
    {
        var observer = new RecordingObserver();
        var options = CreateOptions(new List<int>());
        options.Observer = observer;
        var pipeline = new ClientSnapshotSyncPipeline<Snapshot>(options);
        var missingBaseline = new Snapshot(1ul, 1, 5, 5, false, 1, 100u, 101u, 1, 1);
        var baseline = Snapshot.Baseline(sequence: 10, frame: 10, stateHash: 100u);

        pipeline.Apply(in missingBaseline);
        pipeline.Apply(in baseline);
        pipeline.Reset();

        Assert.Equal(
            new[] { "recovery:5", "result:NeedsFullBaseline:0", "result:AppliedFullBaseline:10", "reset:10" },
            observer.Events);
    }

    [Fact]
    public void ApplyFailureDoesNotNotifyResultObserverOrCommit()
    {
        var observer = new RecordingObserver();
        var options = CreateOptions(new List<int>());
        options.ApplySnapshot = static (in Snapshot _) => throw new InvalidOperationException("projection failed");
        options.Observer = observer;
        var pipeline = new ClientSnapshotSyncPipeline<Snapshot>(options);
        var baseline = Snapshot.Baseline(sequence: 10, frame: 10, stateHash: 100u);

        Assert.Throws<InvalidOperationException>(() => pipeline.Apply(in baseline));

        Assert.Empty(observer.Events);
        Assert.False(pipeline.State.HasAppliedSnapshot);
    }

    [Fact]
    public void CustomHealthEventsAreCopiedBeforeResultIsPublished()
    {
        var customEvents = new List<SyncHealthEvent>
        {
            SyncHealthEvent.Info(SyncHealthEventKind.ObserverSnapshotQueued, 10, 1)
        };
        var options = CreateOptions(new List<int>());
        options.HealthEventPolicy =
            (in ClientSnapshotSyncEventContext<Snapshot> _, IReadOnlyList<SyncHealthEvent> _) => customEvents;
        var pipeline = new ClientSnapshotSyncPipeline<Snapshot>(options);
        var baseline = Snapshot.Baseline(sequence: 10, frame: 10, stateHash: 100u);

        var result = pipeline.Apply(in baseline);
        customEvents.Clear();

        Assert.Single(result.HealthEvents);
        Assert.Equal(SyncHealthEventKind.ObserverSnapshotQueued, result.HealthEvents[0].Kind);
    }

    [Fact]
    public void CompositeObserverSnapshotsAndNotifiesEachObserver()
    {
        var first = new RecordingObserver();
        var second = new RecordingObserver();
        var source = new IClientSnapshotSyncObserver<Snapshot>[] { first, second };
        var composite = new CompositeClientSnapshotSyncObserver<Snapshot>(source);
        source[0] = new RecordingObserver();
        var options = CreateOptions(new List<int>());
        options.Observer = composite;
        var pipeline = new ClientSnapshotSyncPipeline<Snapshot>(options);
        var baseline = Snapshot.Baseline(sequence: 10, frame: 10, stateHash: 100u);

        pipeline.Apply(in baseline);

        Assert.Equal(2, composite.Count);
        Assert.Single(first.Events);
        Assert.Single(second.Events);
    }

    [Fact]
    public void HealthEventPolicyFailureDoesNotApplyOrCommitAcceptedSnapshot()
    {
        var applied = new List<int>();
        var options = CreateOptions(applied);
        options.HealthEventPolicy =
            static (in ClientSnapshotSyncEventContext<Snapshot> _, IReadOnlyList<SyncHealthEvent> _) =>
                throw new InvalidOperationException("metrics failed");
        var pipeline = new ClientSnapshotSyncPipeline<Snapshot>(options);
        var baseline = Snapshot.Baseline(sequence: 10, frame: 10, stateHash: 100u);

        Assert.Throws<InvalidOperationException>(() => pipeline.Apply(in baseline));

        Assert.Empty(applied);
        Assert.False(pipeline.State.HasAppliedSnapshot);
    }

    [Fact]
    public void HealthEventPolicyFailureDoesNotDispatchRecoveryHandler()
    {
        var handlerCalls = 0;
        var options = CreateOptions(new List<int>());
        options.RecoveryHandler = (in Snapshot _, in SnapshotRecoveryRequest _) => handlerCalls++;
        options.HealthEventPolicy =
            static (in ClientSnapshotSyncEventContext<Snapshot> _, IReadOnlyList<SyncHealthEvent> _) =>
                throw new InvalidOperationException("metrics failed");
        var pipeline = new ClientSnapshotSyncPipeline<Snapshot>(options);
        var delta = new Snapshot(1ul, 1, 5, 5, false, 1, 100u, 101u, 1, 1);

        Assert.Throws<InvalidOperationException>(() => pipeline.Apply(in delta));

        Assert.Equal(0, handlerCalls);
        Assert.True(pipeline.State.NeedsFullBaselineRecovery);
    }

    [Fact]
    public void UnsupportedRecoveryKindFailsBeforeHandlerAndResultObserver()
    {
        var handlerCalls = 0;
        var observer = new RecordingObserver();
        var options = CreateOptions(new List<int>());
        options.RecoveryStrategy = static (in Snapshot _, in SnapshotStreamValidationResult _) =>
            (SnapshotRecoveryRequestKind)999;
        options.RecoveryHandler = (in Snapshot _, in SnapshotRecoveryRequest _) => handlerCalls++;
        options.Observer = observer;
        var pipeline = new ClientSnapshotSyncPipeline<Snapshot>(options);
        var delta = new Snapshot(1ul, 1, 5, 5, false, 1, 100u, 101u, 1, 1);

        Assert.Throws<InvalidOperationException>(() => pipeline.Apply(in delta));

        Assert.Equal(0, handlerCalls);
        Assert.Empty(observer.Events);
        Assert.True(pipeline.State.NeedsFullBaselineRecovery);
    }

    [Fact]
    public void ObserverFailureIsReportedWithoutChangingCommittedResult()
    {
        var reported = new List<(ClientSnapshotSyncObserverStage Stage, string Message)>();
        var options = CreateOptions(new List<int>());
        options.Observer = new ThrowingObserver();
        options.ObserverErrorHandler = (stage, exception) => reported.Add((stage, exception.Message));
        var pipeline = new ClientSnapshotSyncPipeline<Snapshot>(options);
        var baseline = Snapshot.Baseline(sequence: 10, frame: 10, stateHash: 100u);

        var result = pipeline.Apply(in baseline);
        pipeline.Reset();

        Assert.True(result.Applied);
        Assert.False(pipeline.State.HasAppliedSnapshot);
        Assert.Equal(
            new[]
            {
                (ClientSnapshotSyncObserverStage.Result, "observer result failed"),
                (ClientSnapshotSyncObserverStage.Reset, "observer reset failed")
            },
            reported);
    }

    private static ClientSnapshotSyncPipeline<Snapshot> CreatePipeline(
        List<int> applied,
        SnapshotApplyHandler<Snapshot>? apply = null)
    {
        return new ClientSnapshotSyncPipeline<Snapshot>(
            minimumSupportedVersion: 1,
            maximumSupportedVersion: 1,
            createEnvelope: static (in Snapshot snapshot) => new SnapshotStreamEnvelope(
                snapshot.WorldId,
                snapshot.Version,
                snapshot.Sequence,
                snapshot.Frame,
                snapshot.IsFullBaseline ? SnapshotStreamSnapshotKind.FullBaseline : SnapshotStreamSnapshotKind.Delta,
                snapshot.BaselineFrame,
                snapshot.BaselineHash,
                snapshot.StateHash),
            applySnapshot: apply ?? ((in Snapshot snapshot) => applied.Add(snapshot.Frame)),
            maximumSequenceAdvance: static (in Snapshot snapshot) => snapshot.MaximumSequenceAdvance,
            entityCount: static (in Snapshot snapshot) => snapshot.EntityCount);
    }

    private static ClientSnapshotSyncOptions<Snapshot> CreateOptions(List<int> applied)
    {
        return new ClientSnapshotSyncOptions<Snapshot>(
            minimumSupportedVersion: 1,
            maximumSupportedVersion: 1,
            createEnvelope: static (in Snapshot snapshot) => new SnapshotStreamEnvelope(
                snapshot.WorldId,
                snapshot.Version,
                snapshot.Sequence,
                snapshot.Frame,
                snapshot.IsFullBaseline ? SnapshotStreamSnapshotKind.FullBaseline : SnapshotStreamSnapshotKind.Delta,
                snapshot.BaselineFrame,
                snapshot.BaselineHash,
                snapshot.StateHash),
            applySnapshot: (in Snapshot snapshot) => applied.Add(snapshot.Frame))
        {
            MaximumSequenceAdvance = static (in Snapshot snapshot) => snapshot.MaximumSequenceAdvance,
            EntityCount = static (in Snapshot snapshot) => snapshot.EntityCount
        };
    }

    private sealed class RecordingObserver : ClientSnapshotSyncObserver<Snapshot>
    {
        public List<string> Events { get; } = new();

        public override void OnResult(
            in Snapshot snapshot,
            in ClientSnapshotSyncResult result,
            in ClientSnapshotSyncState state)
        {
            Events.Add($"result:{result.Status}:{state.LastAppliedFrame}");
        }

        public override void OnRecoveryRequested(
            in Snapshot snapshot,
            in SnapshotRecoveryRequest request,
            in ClientSnapshotSyncState state)
        {
            Events.Add($"recovery:{request.Envelope.Frame}");
        }

        public override void OnReset(in ClientSnapshotSyncState previousState)
        {
            Events.Add($"reset:{previousState.LastAppliedFrame}");
        }
    }

    private sealed class ThrowingObserver : ClientSnapshotSyncObserver<Snapshot>
    {
        public override void OnResult(
            in Snapshot snapshot,
            in ClientSnapshotSyncResult result,
            in ClientSnapshotSyncState state)
        {
            throw new InvalidOperationException("observer result failed");
        }

        public override void OnReset(in ClientSnapshotSyncState previousState)
        {
            throw new InvalidOperationException("observer reset failed");
        }
    }

    private readonly record struct Snapshot(
        ulong WorldId,
        int Version,
        long Sequence,
        int Frame,
        bool IsFullBaseline,
        int BaselineFrame,
        uint BaselineHash,
        uint StateHash,
        int MaximumSequenceAdvance,
        int EntityCount)
    {
        public static Snapshot Baseline(long sequence, int frame, uint stateHash, int entityCount = 1)
        {
            return new Snapshot(1ul, 1, sequence, frame, true, frame, stateHash, stateHash, 2, entityCount);
        }

        public static Snapshot Delta(
            long sequence,
            int frame,
            Snapshot baseline,
            uint stateHash,
            int entityCount = 1,
            int maximumSequenceAdvance = 2)
        {
            return new Snapshot(
                baseline.WorldId,
                baseline.Version,
                sequence,
                frame,
                false,
                baseline.BaselineFrame,
                baseline.BaselineHash,
                stateHash,
                maximumSequenceAdvance,
                entityCount);
        }
    }
}
