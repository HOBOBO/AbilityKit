using System.Reflection;
using AbilityKit.Ability.Host.Extensions.Server.BattleHost;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Protocol.Room;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class FixedBaselineSnapshotStreamTests
{
    [Fact]
    public void SkippedDeltasDoNotAdvanceComparisonBase()
    {
        var stream = new FixedBaselineSnapshotStream(tickRate: 30, fullSnapshotIntervalFrames: 30);
        var baseline = Encode(stream, Projection(10, (1, 0f), (2, 20f), (3, 30f), (4, 40f)));
        var first = Read(baseline);
        Assert.True(baseline.IsFullSnapshot);
        Assert.Equal(0, first.BaseId);
        Assert.Equal(new byte[] { 1, 10 }, first.Payload);

        var omitted = Encode(stream, Projection(11, (1, 1f), (2, 20f), (3, 30f), (4, 40f), (5, 0f)));
        Assert.Equal(first.Id, Read(omitted).BaseId);
        var next = Encode(stream, Projection(12, (1, 1f), (2, 20f), (3, 30f)));
        Assert.False(next.IsFullSnapshot);
        Assert.Equal(first.Id, Read(next).BaseId);
        Assert.Equal(1, Assert.Single(next.Actors).ActorId);
        Assert.Equal(1f, next.Actors[0].X);
        Assert.Equal(4, Assert.Single(next.RemovedActorIds));
        Assert.Equal(new byte[] { 1, 12 }, Read(next).Payload);

        var returned = Encode(stream, Projection(13, (1, 0f), (2, 20f), (3, 30f), (4, 40f)));
        Assert.Empty(returned.Actors);
        Assert.Empty(returned.RemovedActorIds);
        Assert.Equal(first.Id, Read(returned).BaseId);
    }

    [Fact]
    public void RefreshRequestsCoalesceUntilSharedFullCanRotate()
    {
        var stream = new FixedBaselineSnapshotStream(tickRate: 30, fullSnapshotIntervalFrames: 30);
        var initial = Read(Encode(stream, Projection(10, (1, 0f))));
        stream.RequestFull();
        stream.RequestFull();
        var waiting = Encode(stream, Projection(16, (1, 0f)));
        Assert.False(waiting.IsFullSnapshot);
        Assert.Equal(initial.Id, Read(waiting).BaseId);

        var rotation = Encode(stream, Projection(17, (1, 0f)));
        Assert.True(rotation.IsFullSnapshot);
        var rotated = Read(rotation);
        Assert.Equal(initial.Generation, rotated.Generation);
        Assert.True(rotated.Id > initial.Id);
        Assert.Equal(rotated.Id, Read(Encode(stream, Projection(18, (1, 0f)))).BaseId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OversizedFullDoesNotAdvanceStreamAndPreservesPendingRefresh(bool requested)
    {
        var stream = new FixedBaselineSnapshotStream(tickRate: 30);
        var observer = new StateSyncObserverGrain(NullLogger<StateSyncObserverGrain>.Instance,
            Options.Create(new StateSyncObserverOptions { BytesPerSecond = 0 }));
        typeof(StateSyncObserverGrain).GetField("_usesFixedBaseline", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(observer, true);

        var initial = Encode(stream, Projection(10, (1, 0f), (2, 20f)));
        AssertQueuedOrAccepted(await observer.OnSnapshotPushedAsync(initial));

        if (requested) stream.RequestFull();
        var candidate = OversizedProjection(17);
        var maximumBytes = FixedBaselineStateSyncProtocol.MaximumSnapshotBytes;
        Assert.False(stream.TryEncode(candidate, legacyScheduledFull: !requested,
            out var rejected, out var wireBytes, out var reason));
        Assert.Null(rejected);
        Assert.Equal(requested ? FixedBaselineFullReason.RefreshRequested : FixedBaselineFullReason.Periodic, reason);
        Assert.True(wireBytes > maximumBytes);
        Assert.True(candidate.IsFullSnapshot);
        Assert.Equal(maximumBytes, candidate.Payload.Length);
        Assert.Equal(StateSyncDeliveryStatus.Failed, (await observer.OnSnapshotPushedAsync(candidate)).Status);

        var next = Encode(stream, Projection(18, (1, 2f), (2, 20f)));
        var initialId = Read(initial).Id;
        Assert.Equal(requested, next.IsFullSnapshot);
        AssertQueuedOrAccepted(await observer.OnSnapshotPushedAsync(next));
        Assert.Equal(initialId + 1, Read(next).Id);
        Assert.Equal(requested ? 0 : initialId, Read(next).BaseId);
    }

    [Fact]
    public void OneObserverMissingFullDoesNotInvalidateHealthyObserverBaseline()
    {
        var policy = new SnapshotSendQueuePolicy(bytesPerSecond: 0, burstBytes: 0,
            maxQueueLength: 4, maxQueueAge: TimeSpan.FromTicks(20));
        var recovering = new SnapshotSendQueue<StateSyncPush>(policy, nowTicks: 1);
        var healthy = new SnapshotSendQueue<StateSyncPush>(policy, nowTicks: 1);
        var stream = new FixedBaselineSnapshotStream(tickRate: 30, fullSnapshotIntervalFrames: 30);
        var initial = Encode(stream, Projection(10, (1, 0f), (2, 20f)));
        Enqueue(recovering, initial);
        Enqueue(healthy, initial);
        Assert.True(recovering.TryDequeue(10, out var failed));
        Assert.True(healthy.TryDequeue(10, out var delivered));
        recovering.MarkDeliveryFailed(in failed, baselineInvalidated: true);
        healthy.MarkSent(in delivered, nowTicks: 10);
        Assert.Equal(1, recovering.CreateMetrics(10).ResyncCount);
        Assert.Equal(0, healthy.CreateMetrics(10).ResyncCount);
        stream.RequestFull();

        var beforeRefresh = Encode(stream, Projection(16, (1, 1f), (2, 20f)));
        Assert.False(beforeRefresh.IsFullSnapshot);
        Enqueue(healthy, beforeRefresh);
        Assert.True(healthy.TryDequeue(16, out var usableDelta));
        Assert.Equal(Read(delivered.Value).Id, Read(usableDelta.Value).BaseId);

        var rotated = Encode(stream, Projection(17, (1, 2f), (2, 20f)));
        Assert.True(rotated.IsFullSnapshot);
        Enqueue(recovering, rotated);
        Enqueue(healthy, rotated);
        Assert.True(recovering.TryDequeue(17, out var recovered));
        Assert.True(healthy.TryDequeue(17, out var nextFull));
        Assert.Equal(Read(recovered.Value).Id, Read(nextFull.Value).Id);
        Assert.Equal(0, healthy.CreateMetrics(17).ResyncCount);
    }

    [Theory]
    [InlineData(1, 31, 61)]
    [InlineData(4, 36, 68)]
    public void PeriodicFullUsesFirstPublishPointAfterBaselineAgeThreshold(int publishInterval, int firstRotation,
        int secondRotation)
    {
        var publishPolicy = new BattleSnapshotSyncPolicy(publishInterval, fullSnapshotInterval: 30);
        var stream = new FixedBaselineSnapshotStream(tickRate: 30, publishPolicy.FullSnapshotInterval);
        var fullFrames = new List<int>();
        var fullReasons = new List<FixedBaselineFullReason>();
        for (var frame = 1; frame <= secondRotation; frame++)
        {
            if (!publishPolicy.ShouldPublish(frame, observerCount: 1, worldTicked: true)) continue;

            var push = Encode(stream, Projection(frame, (1, 0f)), out var reason,
                legacyScheduledFull: publishPolicy.ShouldCreateFullSnapshot(frame));
            if (push.IsFullSnapshot)
            {
                fullFrames.Add(frame);
                fullReasons.Add(reason);
            }
        }

        Assert.Equal(new[] { publishInterval, firstRotation, secondRotation }, fullFrames);
        Assert.Equal(new[] { FixedBaselineFullReason.Initial, FixedBaselineFullReason.Periodic,
            FixedBaselineFullReason.Periodic }, fullReasons);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(8, 2)]
    [InlineData(30, 7)]
    public void RequestedFullPreservesMinimumRefreshIntervalInFrames(int tickRate, int minimumFrames)
    {
        const int initialFrame = 10;
        var stream = new FixedBaselineSnapshotStream(tickRate, fullSnapshotIntervalFrames: 30);
        Encode(stream, Projection(initialFrame, (1, 0f)));
        stream.RequestFull();
        stream.RequestFull();
        Assert.False(Encode(stream, Projection(initialFrame + minimumFrames - 1, (1, 0f))).IsFullSnapshot);
        Assert.True(Encode(stream, Projection(initialFrame + minimumFrames, (1, 0f)), out var reason).IsFullSnapshot);
        Assert.Equal(FixedBaselineFullReason.RefreshRequested, reason);
        Assert.False(Encode(stream, Projection(initialFrame + minimumFrames + 1, (1, 0f))).IsFullSnapshot);
    }

    [Fact]
    public void RejectedPeriodicFullRemainsDueUntilPublishableFullCommits()
    {
        var stream = new FixedBaselineSnapshotStream(tickRate: 30, fullSnapshotIntervalFrames: 30);
        var initialId = Read(Encode(stream, Projection(4, (1, 0f), (2, 20f)))).Id;

        Assert.False(stream.TryEncode(OversizedProjection(36), legacyScheduledFull: false,
            out var rejected, out _, out var rejectionReason));
        Assert.Null(rejected);
        Assert.Equal(FixedBaselineFullReason.Periodic, rejectionReason);

        var retry = Encode(stream, Projection(40, (1, 1f), (2, 20f)), out var retryReason);
        Assert.Equal(initialId + 1, Read(retry).Id);
        Assert.Equal(FixedBaselineFullReason.Periodic, retryReason);
        Assert.False(Encode(stream, Projection(68, (1, 1f), (2, 20f))).IsFullSnapshot);
        Assert.True(Encode(stream, Projection(72, (1, 1f), (2, 20f))).IsFullSnapshot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EarlyFullRestartsPeriodicAge(bool requested)
    {
        var stream = new FixedBaselineSnapshotStream(tickRate: 30, fullSnapshotIntervalFrames: 30);
        Encode(stream, Projection(1, (1, 0f)));
        if (requested) stream.RequestFull();
        var early = Encode(stream, Projection(8, (1, requested ? 0f : 1f)), out var reason);
        Assert.True(early.IsFullSnapshot);
        Assert.Equal(requested ? FixedBaselineFullReason.RefreshRequested : FixedBaselineFullReason.DeltaCost, reason);
        Assert.False(Encode(stream, Projection(31, (1, requested ? 0f : 1f))).IsFullSnapshot);
        Assert.True(Encode(stream, Projection(38, (1, requested ? 0f : 1f))).IsFullSnapshot);
    }

    private static void Enqueue(SnapshotSendQueue<StateSyncPush> queue, StateSyncPush push)
    {
        var priority = push.IsFullSnapshot ? SnapshotDeliveryPriority.FullBaseline : SnapshotDeliveryPriority.Delta;
        var item = new SnapshotSendQueueItem<StateSyncPush>(push, push.Frame,
            StateSyncSnapshotWireCodec.Serialize(push).Count, priority,
            replaceable: !push.IsFullSnapshot, producedAtTicks: push.Frame);
        var result = queue.Enqueue(in item, nowTicks: push.Frame);
        Assert.Contains(result.Status, new[] { SnapshotDeliveryStatus.Queued, SnapshotDeliveryStatus.Accepted });
    }

    private static StateSyncPush Encode(FixedBaselineSnapshotStream stream, StateSyncPush projection) =>
        Encode(stream, projection, out _);

    private static StateSyncPush Encode(FixedBaselineSnapshotStream stream, StateSyncPush projection,
        out FixedBaselineFullReason fullReason, bool legacyScheduledFull = false)
    {
        Assert.True(stream.TryEncode(projection, legacyScheduledFull, out var push, out _, out fullReason));
        return Assert.IsType<StateSyncPush>(push);
    }

    private static StateSyncPush OversizedProjection(int frame)
    {
        var projection = Projection(frame, (1, 1f), (2, 20f));
        projection.Payload = new byte[FixedBaselineStateSyncProtocol.MaximumSnapshotBytes -
            FixedBaselineStateSyncProtocol.Encode(Guid.NewGuid(), 1, 0, 1, null).Length];
        return projection;
    }

    private static void AssertQueuedOrAccepted(StateSyncDeliveryResult result) =>
        Assert.Contains(result.Status, new[] { StateSyncDeliveryStatus.Queued, StateSyncDeliveryStatus.Accepted });

    private static StateSyncPush Projection(int frame, params (int ActorId, float X)[] actors) => new()
    {
        WorldId = 7,
        Frame = frame,
        Actors = actors.Select(actor => new ActorSnapshot { ActorId = actor.ActorId, X = actor.X }).ToList(),
        SchemaVersion = 1,
        Payload = new[] { (byte)1, (byte)frame }
    };

    private static (Guid Generation, long Id, long BaseId, byte[] Payload) Read(StateSyncPush push)
    {
        Assert.Equal(FixedBaselineStateSyncProtocol.SchemaVersion, push.SchemaVersion);
        Assert.True(FixedBaselineStateSyncProtocol.TryDecode(push.Payload, out var generation,
            out var snapshotId, out var baseId, out var innerVersion, out var payload));
        Assert.Equal(1, innerVersion);
        return (generation, snapshotId, baseId, payload);
    }
}
