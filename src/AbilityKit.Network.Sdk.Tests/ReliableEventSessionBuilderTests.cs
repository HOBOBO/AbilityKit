using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Sdk.Tests;

public sealed class ReliableEventSessionBuilderTests
{
    [Fact]
    public void BuildCreatesStandardCursorAndRestoresCheckpoint()
    {
        var delivered = new List<long>();
        var options = CreateOptions();
        options.InitialCheckpoint = new ReliableEventCheckpoint(StreamId, TimelineId, 3L);
        options.EventSink = item => delivered.Add(item.Sequence);
        options.AwaitAuthoritativeBaseline = false;

        using var session = new ReliableEventSessionBuilder<TestEvent>(options).Build();
        var batch = Batch(Event(4));
        session.Handle(in batch);

        Assert.Equal(StreamId, session.StreamId);
        Assert.Equal(TimelineId, session.TimelineId);
        Assert.Equal(4L, session.LastAcknowledgedSequence);
        Assert.Equal(new[] { 4L }, delivered);
    }

    [Fact]
    public void BuildUsesOptionsSnapshot()
    {
        var firstSinkCalls = 0;
        var secondSinkCalls = 0;
        var options = CreateOptions();
        options.EventSink = _ => firstSinkCalls++;
        options.AwaitAuthoritativeBaseline = false;
        var builder = new ReliableEventSessionBuilder<TestEvent>(options);
        options.EventSink = _ => secondSinkCalls++;
        options.DeliveryOptions.AcknowledgementStrategy =
            ReliableEventAcknowledgementStrategy.Automatic;

        using var session = builder.Build();
        var batch = Batch(Event(1));
        session.Handle(in batch);

        Assert.Equal(1, firstSinkCalls);
        Assert.Equal(0, secondSinkCalls);
    }

    [Fact]
    public void BuildAcceptsCustomCompatibleCursor()
    {
        var cursor = CreateCursor();
        var options = new ReliableEventSessionOptions<TestEvent>
        {
            Cursor = cursor,
            DeliveryOptions = new ReliableEventDeliveryOptions
            {
                AcknowledgementStrategy = ReliableEventAcknowledgementStrategy.External
            },
            EventSink = _ => { },
            TimelineInvalidated = _ => { },
            AwaitAuthoritativeBaseline = false
        };

        using var session = new ReliableEventSessionBuilder<TestEvent>(options).Build();

        Assert.Same(cursor, session.Cursor);
    }

    [Fact]
    public void BuildRejectsAutomaticAcknowledgementWithoutOperation()
    {
        var options = CreateOptions();
        options.DeliveryOptions.AcknowledgementStrategy =
            ReliableEventAcknowledgementStrategy.Automatic;

        var exception = Assert.Throws<ReliableEventSessionBuildException>(
            () => new ReliableEventSessionBuilder<TestEvent>(options).Build());

        Assert.Equal(
            ReliableEventSessionBuildFailureReason.MissingAcknowledgementOperation,
            exception.Reason);
    }

    [Fact]
    public void BuildRejectsAmbiguousCursorConfiguration()
    {
        var options = CreateOptions();
        options.Cursor = CreateCursor();

        var exception = Assert.Throws<ReliableEventSessionBuildException>(
            () => new ReliableEventSessionBuilder<TestEvent>(options).Build());

        Assert.Equal(
            ReliableEventSessionBuildFailureReason.AmbiguousCursorConfiguration,
            exception.Reason);
    }

    [Fact]
    public void BuildRejectsMismatchedInitialCheckpoint()
    {
        var options = CreateOptions();
        options.InitialCheckpoint = new ReliableEventCheckpoint(
            "another-battle",
            TimelineId,
            2L);

        var exception = Assert.Throws<ReliableEventSessionBuildException>(
            () => new ReliableEventSessionBuilder<TestEvent>(options).Build());

        Assert.Equal(
            ReliableEventSessionBuildFailureReason.InvalidInitialCheckpoint,
            exception.Reason);
    }

    [Fact]
    public void SessionExposesWatermarkPendingControlAndCheckpoint()
    {
        var options = CreateOptions();
        options.CursorOptions = new ReliableEventCursorOptions
        {
            GapPolicy = ReliableEventGapPolicy.BufferWithinCapacity,
            BindTimelineOnAdmission = true
        };
        options.AwaitAuthoritativeBaseline = false;
        using var session = new ReliableEventSessionBuilder<TestEvent>(options).Build();
        var future = new ReliableEventBatch<TestEvent>(
            StreamId,
            TimelineId,
            1L,
            2L,
            retentionGap: false,
            new[] { Event(2) });
        session.Handle(in future);

        Assert.Equal(2L, session.LastObservedWatermark);
        Assert.Equal(0L, session.LastDeliveredSequence);
        session.DiscardPending();
        var first = Batch(Event(1));
        session.Handle(in first);
        var checkpoint = session.CreateCheckpoint();

        Assert.Equal(1L, checkpoint.LastAcknowledgedSequence);
        Assert.Equal(StreamId, checkpoint.StreamId);
        Assert.Equal(TimelineId, checkpoint.TimelineId);
    }

    [Fact]
    public void BuildAppliesNegotiatedExternalAcknowledgementAndBufferedDelivery()
    {
        var saved = new List<ReliableEventCheckpoint>();
        var options = CreateOptions();
        options.NegotiatedSession = CreateNegotiatedSession(NetworkSyncProfiles.PredictRollback);
        options.ApplyNegotiatedPolicy = true;
        options.SaveCheckpoint = saved.Add;
        options.AwaitAuthoritativeBaseline = false;

        using var session = new ReliableEventSessionBuilder<TestEvent>(options).Build();
        var future = new ReliableEventBatch<TestEvent>(
            StreamId,
            TimelineId,
            1L,
            2L,
            retentionGap: false,
            new[] { Event(2) });
        session.Handle(in future);
        var contiguous = Batch(Event(1), Event(2));
        session.Handle(in contiguous);

        Assert.True(session.NegotiatedPolicy.HasValue);
        Assert.Equal(
            ReliableEventAcknowledgementStrategy.External,
            session.NegotiatedPolicy.Value.AcknowledgementStrategy);
        Assert.Equal(ReliableEventGapPolicy.BufferWithinCapacity, session.NegotiatedPolicy.Value.GapPolicy);
        Assert.Equal(2L, session.LastAcknowledgedSequence);
        Assert.Contains(saved, checkpoint => checkpoint.LastAcknowledgedSequence == 2L);
    }

    [Fact]
    public void BuildAppliesNegotiatedAutomaticAcknowledgement()
    {
        var profile = CreateAutomaticProfile();
        var acknowledgements = new List<long>();
        var options = CreateOptions();
        options.NegotiatedSession = CreateNegotiatedSession(profile);
        options.ApplyNegotiatedPolicy = true;
        options.SaveCheckpoint = _ => { };
        options.Acknowledge = (_, sequence) =>
        {
            acknowledgements.Add(sequence);
            return Task.FromResult(sequence);
        };
        options.AwaitAuthoritativeBaseline = false;

        using var session = new ReliableEventSessionBuilder<TestEvent>(options).Build();
        var batch = Batch(Event(1));
        session.Handle(in batch);
        SpinWait.SpinUntil(() => session.LastAcknowledgedSequence == 1L, 1000);

        Assert.Equal(
            ReliableEventAcknowledgementStrategy.Automatic,
            session.NegotiatedPolicy!.Value.AcknowledgementStrategy);
        Assert.Equal(new[] { 1L }, acknowledgements);
    }

    [Fact]
    public void BuildRejectsNegotiatedCheckpointWithoutSaveOperation()
    {
        var options = CreateOptions();
        options.NegotiatedSession = CreateNegotiatedSession(NetworkSyncProfiles.PredictRollback);
        options.ApplyNegotiatedPolicy = true;

        var exception = Assert.Throws<ReliableEventSessionBuildException>(
            () => new ReliableEventSessionBuilder<TestEvent>(options).Build());

        Assert.Equal(
            ReliableEventSessionBuildFailureReason.MissingCheckpointSaveOperation,
            exception.Reason);
    }

    [Fact]
    public void BuildRejectsProfileWithoutNegotiatedReliableEvents()
    {
        var options = CreateOptions();
        options.NegotiatedSession = CreateNegotiatedSession(NetworkSyncProfiles.Lockstep);
        options.ApplyNegotiatedPolicy = true;

        var exception = Assert.Throws<ReliableEventSessionBuildException>(
            () => new ReliableEventSessionBuilder<TestEvent>(options).Build());

        Assert.Equal(
            ReliableEventSessionBuildFailureReason.ReliableEventsNotNegotiated,
            exception.Reason);
    }

    [Fact]
    public void BuildRejectsNegotiatedBufferedDeliveryForOpaqueCustomCursor()
    {
        var options = new ReliableEventSessionOptions<TestEvent>
        {
            Cursor = CreateCursor(),
            NegotiatedSession = CreateNegotiatedSession(NetworkSyncProfiles.PredictRollback),
            ApplyNegotiatedPolicy = true,
            DeliveryOptions = new ReliableEventDeliveryOptions(),
            EventSink = _ => { },
            TimelineInvalidated = _ => { },
            SaveCheckpoint = _ => { },
            AwaitAuthoritativeBaseline = false
        };

        var exception = Assert.Throws<ReliableEventSessionBuildException>(
            () => new ReliableEventSessionBuilder<TestEvent>(options).Build());

        Assert.Equal(
            ReliableEventSessionBuildFailureReason.BufferedDeliveryRequiresStandardCursor,
            exception.Reason);
    }

    [Fact]
    public void BuildLoadsCheckpointFromStoreAndPersistsToStoreAndObserver()
    {
        var store = new InMemoryReliableEventCheckpointStore();
        var initial = new ReliableEventCheckpoint(StreamId, TimelineId, 3L);
        store.Save(in initial);
        var observed = new List<ReliableEventCheckpoint>();
        var options = CreateOptions();
        options.CheckpointStore = store;
        options.SaveCheckpoint = observed.Add;
        options.AwaitAuthoritativeBaseline = false;

        using var session = new ReliableEventSessionBuilder<TestEvent>(options).Build();
        var batch = Batch(Event(4));
        session.Handle(in batch);

        Assert.Equal(4L, session.LastAcknowledgedSequence);
        Assert.True(store.TryLoad(StreamId, out var saved));
        Assert.Equal(4L, saved.LastAcknowledgedSequence);
        Assert.Contains(observed, item => item.LastAcknowledgedSequence == 4L);
        Assert.True(session.RemoveStoredCheckpoint());
        Assert.False(store.TryLoad(StreamId, out _));
    }

    [Fact]
    public void ExplicitCheckpointTakesPriorityOverStoredCheckpoint()
    {
        var store = new InMemoryReliableEventCheckpointStore();
        var stored = new ReliableEventCheckpoint(StreamId, TimelineId, 8L);
        store.Save(in stored);
        var options = CreateOptions();
        options.CheckpointStore = store;
        options.InitialCheckpoint = new ReliableEventCheckpoint(StreamId, TimelineId, 3L);

        using var session = new ReliableEventSessionBuilder<TestEvent>(options).Build();

        Assert.Equal(3L, session.LastAcknowledgedSequence);
    }

    [Fact]
    public void CustomRestorableCursorLoadsCheckpointFromStore()
    {
        var store = new InMemoryReliableEventCheckpointStore();
        var initial = new ReliableEventCheckpoint(StreamId, TimelineId, 5L);
        store.Save(in initial);
        var cursor = CreateCursor();
        var options = new ReliableEventSessionOptions<TestEvent>
        {
            Cursor = cursor,
            CheckpointStore = store,
            DeliveryOptions = new ReliableEventDeliveryOptions
            {
                AcknowledgementStrategy = ReliableEventAcknowledgementStrategy.External
            },
            EventSink = _ => { },
            TimelineInvalidated = _ => { },
            AwaitAuthoritativeBaseline = false
        };

        using var session = new ReliableEventSessionBuilder<TestEvent>(options).Build();

        Assert.Equal(5L, session.LastAcknowledgedSequence);
    }

    [Fact]
    public void BuildRejectsStoredCheckpointFromAnotherStream()
    {
        var options = CreateOptions();
        options.CheckpointStore = new MismatchedCheckpointStore();

        var exception = Assert.Throws<ReliableEventSessionBuildException>(
            () => new ReliableEventSessionBuilder<TestEvent>(options).Build());

        Assert.Equal(
            ReliableEventSessionBuildFailureReason.InvalidStoredCheckpoint,
            exception.Reason);
    }

    [Fact]
    public async Task SessionCanFlushABufferedCheckpointStore()
    {
        var inner = new InMemoryReliableEventCheckpointStore();
        using var buffered = new BufferedReliableEventCheckpointStore(inner);
        var options = CreateOptions();
        options.CheckpointStore = buffered;
        options.AwaitAuthoritativeBaseline = false;

        using var session = new ReliableEventSessionBuilder<TestEvent>(options).Build();
        var batch = Batch(Event(1));
        session.Handle(in batch);
        await session.FlushCheckpointStoreAsync();

        Assert.True(inner.TryLoad(StreamId, out var checkpoint));
        Assert.Equal(1L, checkpoint.LastAcknowledgedSequence);
    }

    private static ReliableEventSessionOptions<TestEvent> CreateOptions()
    {
        return new ReliableEventSessionOptions<TestEvent>
        {
            StreamId = StreamId,
            Descriptor = Descriptor,
            DeliveryOptions = new ReliableEventDeliveryOptions
            {
                AcknowledgementStrategy = ReliableEventAcknowledgementStrategy.External
            },
            EventSink = _ => { },
            TimelineInvalidated = _ => { }
        };
    }

    private static ReliableEventCursor<TestEvent> CreateCursor()
    {
        return new ReliableEventCursor<TestEvent>(StreamId, Descriptor);
    }

    private static NetworkSyncSessionDescriptor CreateNegotiatedSession(in NetworkSyncProfile profile)
    {
        var registry = new NetworkSyncProfileControllerRegistry<string, int>(
            new Dictionary<NetworkSyncProfile, NetworkSyncProfileControllerBuilder<string, int>>
            {
                [profile] = static (in int _) => "controller"
            });
        var capabilities = NetworkSyncCapabilities.FromProfile(in profile, 1, 1);
        var options = new NetworkSyncSessionOptions
        {
            RequiredProfile = profile,
            RequiredProfileName = "Reliable.Events.Tests",
            RequiredMinimumSchemaVersion = 1,
            RequiredMaximumSchemaVersion = 1,
            AvailableCapabilities = capabilities
        };
        return new NetworkSyncSessionBuilder<string, int>(registry, options).Build(0).Descriptor;
    }

    private static NetworkSyncProfile CreateAutomaticProfile()
    {
        return new NetworkSyncProfile(
            NetworkSyncModel.AuthoritativeInterpolation,
            ClientPlaybackPolicy.AuthoritativeInterpolation,
            InputPolicy.NoClientInput,
            SnapshotPolicy.FullSnapshot | SnapshotPolicy.EventStream,
            InterestPolicy.AllEntities,
            RecoveryPolicy.RequestFullSnapshot,
            ServerValidationPolicy.AuthoritativeOnly,
            ReliableEventPolicy.OrderedDelivery |
            ReliableEventPolicy.AutomaticAcknowledgement |
            ReliableEventPolicy.PersistentCheckpoint |
            ReliableEventPolicy.AuthoritativeBaselineRecovery);
    }

    private sealed class MismatchedCheckpointStore : IReliableEventCheckpointStore
    {
        public bool TryLoad(string streamId, out ReliableEventCheckpoint checkpoint)
        {
            checkpoint = new ReliableEventCheckpoint("another-stream", TimelineId, 1L);
            return true;
        }

        public void Save(in ReliableEventCheckpoint checkpoint)
        {
        }

        public bool Remove(string streamId)
        {
            return false;
        }
    }

    private static ReliableEventBatch<TestEvent> Batch(params TestEvent[] events)
    {
        return new ReliableEventBatch<TestEvent>(
            StreamId,
            TimelineId,
            events.Length == 0 ? 0L : events[0].Sequence,
            events.Length == 0 ? 0L : events[^1].Sequence,
            retentionGap: false,
            events);
    }

    private static TestEvent Event(long sequence)
    {
        return new TestEvent(StreamId, TimelineId, sequence);
    }

    private readonly record struct TestEvent(
        string StreamId,
        string TimelineId,
        long Sequence);

    private static readonly ReliableEventDescriptor<TestEvent> Descriptor =
        new ReliableEventDescriptor<TestEvent>(
            item => item.StreamId,
            item => item.TimelineId,
            item => item.Sequence);

    private const string StreamId = "battle-1";
    private const string TimelineId = "epoch-1";
}
