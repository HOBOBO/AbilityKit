using System.Diagnostics;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Protocol;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed partial class StateSyncSnapshotCostTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public void CompareOpaqueDeliveryReuse(int observers)
    {
        MeasureOpaqueDelivery(observers, reuse: false);
        MeasureOpaqueDelivery(observers, reuse: true);
        var individual = MeasureOpaqueDelivery(observers, reuse: false);
        var shared = MeasureOpaqueDelivery(observers, reuse: true);

        var publications = MeasuredFrames / PublishEvery;
        Assert.Equal(publications, individual.DeliveryCost.Count);
        Assert.Equal(publications, shared.DeliveryCost.Count);
        Assert.Equal(publications * observers, individual.DeliveryEncodes);
        Assert.Equal(publications, shared.DeliveryEncodes);
        Assert.Equal(individual.TotalDeliveredLength, shared.TotalDeliveredLength);
        ReportOpaqueDelivery(observers, "per-observer", individual);
        ReportOpaqueDelivery(observers, "shared-copy", shared);
    }

    [Fact]
    public void OwnedOpaqueSnapshotKeepsObserverQueuesIndependent()
    {
        var stream = CreateOpaqueStream();
        var (baseline, fullLength) = PrepareOpaquePublication(stream, PublishEvery);
        Assert.True(baseline.IsFullSnapshot);
        var transient = StateSyncSnapshotWireCodec.Serialize(baseline);
        var detached = transient.ToArray();
        Assert.NotSame(transient.Array, detached);
        Assert.Equal(fullLength, detached.Length);
        ReadOnlyMemory<byte> ownedFull = detached;

        const long initialTicks = 1;
        var slow = new SnapshotSendQueue<ReadOnlyMemory<byte>>(
            new SnapshotSendQueuePolicy(bytesPerSecond: 1, burstBytes: fullLength, maxQueueLength: 1,
                maxQueueAge: TimeSpan.MaxValue), initialTicks);
        var healthy = new SnapshotSendQueue<ReadOnlyMemory<byte>>(
            new SnapshotSendQueuePolicy(bytesPerSecond: 0, burstBytes: 0, maxQueueLength: 1,
                maxQueueAge: TimeSpan.MaxValue), initialTicks);

        var full = CreateOpaqueQueueItem(baseline, ownedFull, initialTicks);
        Assert.Equal(SnapshotDeliveryStatus.Accepted, slow.Enqueue(full, initialTicks).Status);
        Assert.Equal(SnapshotDeliveryStatus.Accepted, healthy.Enqueue(full, initialTicks).Status);
        Assert.True(slow.TryDequeue(initialTicks, out var failed));
        Assert.True(failed.Value.Equals(ownedFull));
        slow.MarkDeliveryFailed(failed, baselineInvalidated: true);

        var (firstDelta, firstLength) = PrepareOpaquePublication(stream, 2 * PublishEvery);
        Assert.False(firstDelta.IsFullSnapshot);
        var firstMemory = EncodeOwnedOpaquePayload(firstDelta);
        Assert.Equal(firstLength, firstMemory.Length);
        Assert.False(firstMemory.Equals(ownedFull));
        Assert.True(healthy.TryDequeue(initialTicks, out var deliveredFull));
        Assert.Equal(baseline.Frame, deliveredFull.Frame);
        Assert.True(deliveredFull.Value.Equals(ownedFull));
        healthy.MarkSent(deliveredFull, initialTicks);

        var first = CreateOpaqueQueueItem(firstDelta, firstMemory, initialTicks + 1);
        Assert.False(slow.Enqueue(first, initialTicks + 1).BaselineInvalidated);
        Assert.False(healthy.Enqueue(first, initialTicks + 1).BaselineInvalidated);
        Assert.False(slow.TryDequeue(initialTicks + 1, out _));
        Assert.True(healthy.TryDequeue(initialTicks + 1, out var deliveredFirst));
        Assert.Equal(firstDelta.Frame, deliveredFirst.Frame);
        Assert.True(deliveredFirst.Value.Equals(firstMemory));
        healthy.MarkSent(deliveredFirst, initialTicks + 1);

        var (latestDelta, latestLength) = PrepareOpaquePublication(stream, 3 * PublishEvery);
        Assert.False(latestDelta.IsFullSnapshot);
        var latestMemory = EncodeOwnedOpaquePayload(latestDelta);
        Assert.Equal(latestLength, latestMemory.Length);
        var latest = CreateOpaqueQueueItem(latestDelta, latestMemory, initialTicks + 2);
        var merged = slow.Enqueue(latest, initialTicks + 2);
        Assert.Equal(1, merged.DroppedItems);
        Assert.False(merged.BaselineInvalidated);
        Assert.Equal(SnapshotDeliveryStatus.Accepted, healthy.Enqueue(latest, initialTicks + 2).Status);
        Assert.True(healthy.TryDequeue(initialTicks + 2, out var deliveredLatest));
        Assert.Equal(latestDelta.Frame, deliveredLatest.Frame);
        Assert.True(deliveredLatest.Value.Equals(latestMemory));
        healthy.MarkSent(deliveredLatest, initialTicks + 2);

        var refilledTicks = initialTicks + TimeSpan.TicksPerSecond * fullLength;
        Assert.True(slow.TryDequeue(refilledTicks, out var drained));
        Assert.Equal(latestDelta.Frame, drained.Frame);
        Assert.True(drained.Value.Equals(latestMemory));
        slow.MarkSent(drained, refilledTicks);

        var slowMetrics = slow.CreateMetrics(refilledTicks);
        var healthyMetrics = healthy.CreateMetrics(refilledTicks);
        Assert.Equal(0, slowMetrics.QueueLength);
        Assert.Equal(0, healthyMetrics.QueueLength);
        Assert.Equal(1, slowMetrics.ResyncCount);
        Assert.Equal(0, healthyMetrics.ResyncCount);
        Assert.Equal((long)firstLength, slowMetrics.MergedBytes);
        Assert.Equal(0, healthyMetrics.MergedBytes);
        Assert.Equal((long)latestLength, slowMetrics.SentBytes);
        Assert.Equal((long)fullLength + firstLength + latestLength, healthyMetrics.SentBytes);
    }

    private static OpaqueDeliveryResult MeasureOpaqueDelivery(int observers, bool reuse)
    {
        var stream = CreateOpaqueStream();
        var result = new OpaqueDeliveryResult(MeasuredFrames / PublishEvery);
        var deliveries = new ReadOnlyMemory<byte>[observers];

        for (var frame = PublishEvery; frame <= MeasuredFrames; frame += PublishEvery)
        {
            var (publication, expectedLength) = PrepareOpaquePublication(stream, frame);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            deliveries[0] = EncodeOwnedOpaquePayload(publication);
            result.DeliveryEncodes++;
            for (var index = 1; index < deliveries.Length; index++)
            {
                if (reuse)
                {
                    deliveries[index] = deliveries[0];
                }
                else
                {
                    deliveries[index] = EncodeOwnedOpaquePayload(publication);
                    result.DeliveryEncodes++;
                }
            }
            result.DeliveryCost.Record(start, before);

            for (var index = 0; index < deliveries.Length; index++)
            {
                Assert.Equal(expectedLength, deliveries[index].Length);
                if (index > 0) Assert.Equal(reuse, deliveries[index].Equals(deliveries[0]));
                result.TotalDeliveredLength += deliveries[index].Length;
            }
        }

        return result;
    }

    private void ReportOpaqueDelivery(int observers, string mode, OpaqueDeliveryResult result) => output.WriteLine(
        $"observers={observers}, {mode}: preflights={result.DeliveryCost.Count}, delivery encodes+copies={result.DeliveryEncodes}, delivery preparation {result.DeliveryCost}, total simulated delivery length={result.TotalDeliveredLength}");

    private static FanoutStream CreateOpaqueStream() => new(Enumerable.Range(1, FanoutActors).ToArray());

    private static (StateSyncPush, int) PrepareOpaquePublication(FanoutStream stream, int frame)
    {
        stream.Projection.Frame = frame;
        var projection = DefaultOrleansBattleProtocolMapper.Instance.CreateStateSyncPushFromProjection(World, frame,
            stream.Projection, isFullSnapshot: true, stream.Buffer);
        if (!stream.Encoder.TryEncode(projection, legacyScheduledFull: false, out var push, out var wireLength, out _))
            throw new InvalidOperationException("Fixture exceeded the snapshot budget.");
        return (push, wireLength);
    }

    private static ReadOnlyMemory<byte> EncodeOwnedOpaquePayload(StateSyncPush push) =>
        StateSyncSnapshotWireCodec.Serialize(push).ToArray();

    private static SnapshotSendQueueItem<ReadOnlyMemory<byte>> CreateOpaqueQueueItem(StateSyncPush push,
        ReadOnlyMemory<byte> payload, long nowTicks) => new(payload, push.Frame, payload.Length,
        push.IsFullSnapshot ? SnapshotDeliveryPriority.FullBaseline : SnapshotDeliveryPriority.Delta,
        replaceable: !push.IsFullSnapshot, producedAtTicks: nowTicks);

    private sealed class OpaqueDeliveryResult(int publications)
    {
        internal readonly Samples DeliveryCost = new(publications);
        internal int DeliveryEncodes;
        internal long TotalDeliveredLength;
    }
}
