using System.Diagnostics;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Battle.Projection;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Protocol;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed partial class StateSyncSnapshotCostTests
{
    private const int FanoutActors = 256;
    private const int ObserverCount = 8;
    private const int ViewGroupCount = 4;

    [Theory]
    [InlineData(64)]
    [InlineData(192)]
    public void CompareSustainedChangeFanout(int visibleActors)
    {
        long sharedDeliveredBytes = 0;
        long groupedDeliveredBytes = 0;
        foreach (var mode in Enum.GetValues<FanoutMode>())
        {
            RunFanout(mode, visibleActors);
            var result = RunFanout(mode, visibleActors);
            var streams = StreamCount(mode);
            var publications = MeasuredFrames / PublishEvery;
            Assert.Equal(streams * publications, result.ProjectionAndPreflight.Count);
            Assert.Equal(ObserverCount * publications, result.DeliveryEncode.Count);
            Assert.Equal(result.DeliveryEncode.Count, result.DecodeAndMap.Count);
            Assert.Equal(result.DeliveryEncode.Count, result.Materialize.Count);
            Assert.Equal((mode == FanoutMode.Shared ? FanoutActors : visibleActors) * result.Materialize.Count,
                result.MaterializedActors);

            if (mode == FanoutMode.Shared) sharedDeliveredBytes = result.DeliveredBytes;
            if (mode == FanoutMode.Grouped)
            {
                groupedDeliveredBytes = result.DeliveredBytes;
                Assert.True(groupedDeliveredBytes < sharedDeliveredBytes);
            }
            if (mode == FanoutMode.PerObserver) Assert.Equal(groupedDeliveredBytes, result.DeliveredBytes);

            var serverAlloc = result.ProjectionAndPreflight.TotalAllocated + result.DeliveryEncode.TotalAllocated;
            output.WriteLine(
                $"visible={visibleActors}, {mode}: streams={streams}, actors/observer={result.MaterializedActors / result.Materialize.Count}, " +
                $"delivered={result.DeliveredBytes * TickRate / MeasuredFrames} B/s, server alloc={serverAlloc / publications} B/publication");
            output.WriteLine($"  projection+preflight: {result.ProjectionAndPreflight}; delivery encode+copy: {result.DeliveryEncode}");
            output.WriteLine($"  decode+map: {result.DecodeAndMap}; materialize+commit: {result.Materialize}");
        }
    }

    private static FanoutResult RunFanout(FanoutMode mode, int visibleActors)
    {
        var streamCount = StreamCount(mode);
        var publications = MeasuredFrames / PublishEvery;
        var result = new FanoutResult(streamCount * publications, ObserverCount * publications);
        var streams = Enumerable.Range(0, streamCount)
            .Select(index => new FanoutStream(CreateView(mode, index, visibleActors)))
            .ToArray();
        var observers = Enumerable.Range(0, ObserverCount).Select(_ => new FanoutObserver()).ToArray();
        var mapper = DefaultOrleansBattleProtocolMapper.Instance;

        for (var frame = PublishEvery; frame <= MeasuredFrames; frame += PublishEvery)
        {
            for (var index = 0; index < streams.Length; index++)
            {
                var stream = streams[index];
                stream.Projection.Frame = frame;
                var before = GC.GetAllocatedBytesForCurrentThread();
                var start = Stopwatch.GetTimestamp();
                var projection = mapper.CreateStateSyncPushFromProjection(World, frame, stream.Projection,
                    isFullSnapshot: true, stream.Buffer);
                if (!stream.Encoder.TryEncode(projection, legacyScheduledFull: false, out var push, out var wireBytes, out _))
                    throw new InvalidOperationException("Fixture exceeded the snapshot budget.");
                result.ProjectionAndPreflight.Record(start, before);

                for (var observer = index; observer < ObserverCount; observer += streamCount)
                    Deliver(push, wireBytes, stream.Projection, observers[observer], result);
            }
        }

        return result;
    }

    private static void Deliver(StateSyncPush push, int wireBytes, FanoutProjection expected,
        FanoutObserver observer, FanoutResult result)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        var delivery = StateSyncSnapshotWireCodec.Serialize(push).ToArray();
        result.DeliveryEncode.Record(start, before);
        Assert.Equal(wireBytes, delivery.Length);
        result.DeliveredBytes += delivery.Length;

        before = GC.GetAllocatedBytesForCurrentThread();
        start = Stopwatch.GetTimestamp();
        var wire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(new ArraySegment<byte>(delivery));
        var snapshot = GatewayStateSyncSnapshotMapper.Map(in wire);
        result.DecodeAndMap.Record(start, before);

        before = GC.GetAllocatedBytesForCurrentThread();
        start = Stopwatch.GetTimestamp();
        var rebuilt = Rebuild(snapshot, observer.State, observer.Admission, out var requestFull);
        result.Materialize.Record(start, before);

        Assert.False(requestFull);
        if (rebuilt is not { } view)
            throw new InvalidOperationException($"Snapshot at frame {expected.Frame} was not materialized.");
        Assert.Equal(expected.Frame, view.Frame);
        Assert.Equal(expected.ActorIds.Length, view.Actors.Length);
        for (var index = 0; index < view.Actors.Length; index++)
        {
            var actor = view.Actors[index];
            var expectedId = expected.ActorIds[index];
            if (actor.ActorId != expectedId || actor.X != FanoutProjection.XAt(expectedId, expected.Frame))
                throw new InvalidOperationException($"Unexpected materialized actor {actor.ActorId} at frame {expected.Frame}; expected {expectedId} and X={FanoutProjection.XAt(expectedId, expected.Frame)}.");
        }
        result.MaterializedActors += view.Actors.Length;
    }

    private static int[] CreateView(FanoutMode mode, int stream, int visibleActors)
    {
        if (mode == FanoutMode.Shared) return Enumerable.Range(1, FanoutActors).ToArray();

        var start = stream % ViewGroupCount * (FanoutActors / ViewGroupCount);
        return Enumerable.Range(0, visibleActors)
            .Select(offset => (start + offset) % FanoutActors + 1)
            .OrderBy(actorId => actorId)
            .ToArray();
    }

    private static int StreamCount(FanoutMode mode) => mode switch
    {
        FanoutMode.Shared => 1,
        FanoutMode.Grouped => ViewGroupCount,
        FanoutMode.PerObserver => ObserverCount,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    private enum FanoutMode { Shared, Grouped, PerObserver }

    private sealed class FanoutStream(int[] actorIds)
    {
        internal readonly FixedBaselineSnapshotStream Encoder = new(TickRate, FullEvery);
        internal readonly FanoutProjection Projection = new(actorIds);
        internal readonly List<ActorProjectionData> Buffer = new(actorIds.Length);
    }

    private sealed class FanoutObserver
    {
        internal readonly MobaAuthoritativeSnapshotState State = new();
        internal readonly MobaSnapshotAdmission Admission = new();

        internal FanoutObserver() => Admission.Reset(World);
    }

    private sealed class FanoutResult(int buildSamples, int deliveries)
    {
        internal readonly Samples ProjectionAndPreflight = new(buildSamples);
        internal readonly Samples DeliveryEncode = new(deliveries);
        internal readonly Samples DecodeAndMap = new(deliveries);
        internal readonly Samples Materialize = new(deliveries);
        internal long DeliveredBytes;
        internal int MaterializedActors;
    }

    private sealed class FanoutProjection(int[] actorIds) : IActorProjectionProducer
    {
        internal int[] ActorIds { get; } = actorIds;
        internal int Frame { get; set; }

        internal static float XAt(int actorId, int frame) => actorId + (actorId % 4 == 0 ? frame : 0);

        public ActorProjectionData ExtractFull(int actorId) => new(actorId,
            XAt(actorId, Frame), 0f, 0f, 0f, 0f, 0f, 1f, 1f, 1f, 1f, 100f, 100f,
            1, 0f, 0f, 1, 1, 0, ActorProjectionFields.All);

        public void ExtractAll(List<ActorProjectionData> buffer)
        {
            foreach (var actorId in ActorIds) buffer.Add(ExtractFull(actorId));
        }

        public ActorProjectionData ExtractSpawn(int actorId) => ExtractFull(actorId);
    }
}
