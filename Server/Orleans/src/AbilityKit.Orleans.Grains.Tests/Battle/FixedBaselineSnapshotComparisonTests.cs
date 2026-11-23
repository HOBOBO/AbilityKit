using AbilityKit.Network.Battle.Projection;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Protocol;
using AbilityKit.Protocol.Room;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class FixedBaselineSnapshotComparisonTests(ITestOutputHelper output)
{
    private const int TickRate = 30;
    private const int FrameCount = 60;
    private const int DroppedFrame = 5;
    private const ulong World = 7;

    [Fact]
    public void SingleOmittedChange_ConvergesOnNextFixedDeltaOrNextLegacyFull()
    {
        var mapper = DefaultOrleansBattleProtocolMapper.Instance;
        var producer = new SampleProjection();
        var legacyCache = new Dictionary<int, ActorProjectionData>();
        var stream = new FixedBaselineSnapshotStream(TickRate, fullSnapshotIntervalFrames: 30);
        var fullOnly = new SampleReceiver(useFixedBaseline: false);
        var fixedBaseline = new SampleReceiver(useFixedBaseline: true);
        var legacy = new SampleReceiver(useFixedBaseline: false);

        for (var frame = 1; frame <= FrameCount; frame++)
        {
            producer.FirstActorX = frame < DroppedFrame ? 0f : 1f;
            var legacyFull = frame == 1 || frame % 30 == 0;
            var full = mapper.CreateStateSyncPushFromProjection(World, frame, producer, isFullSnapshot: true);
            var legacyPush = mapper.CreateStateSyncPushFromProjection(World, frame, producer, legacyFull,
                lastFrameData: legacyCache);
            fullOnly.Observe(full);
            legacy.Observe(legacyPush);
            Assert.True(stream.TryEncode(full, legacyFull, out var fixedPush, out _, out _));
            fixedBaseline.Observe(Assert.IsType<StateSyncPush>(fixedPush));
        }

        Assert.Equal(DroppedFrame + 1, fullOnly.RecoveredFrame);
        Assert.Equal(DroppedFrame + 1, fixedBaseline.RecoveredFrame);
        Assert.Equal(30, legacy.RecoveredFrame);
        Assert.True(fixedBaseline.ProducedBytes < fullOnly.ProducedBytes);
        Assert.Equal(0, fullOnly.ResyncRequests);
        Assert.Equal(0, fixedBaseline.ResyncRequests);
        Assert.Equal(0, legacy.ResyncRequests);
        Report("full-only", fullOnly);
        Report("fixed-baseline", fixedBaseline);
        Report("legacy", legacy);
    }

    private void Report(string mode, SampleReceiver sample) => output.WriteLine(
        $"{mode}: produced={sample.ProducedBytes * TickRate / FrameCount} B/s, " +
        $"resync-signals={sample.ResyncRequests}, convergence={sample.RecoveredFrame - DroppedFrame} ticks");

    private sealed class SampleReceiver(bool useFixedBaseline)
    {
        private readonly AuthoritativeSnapshotAdmission _admission = new();
        private long _baselineId;
        private float _baselineX;
        private float _actorX;

        internal long ProducedBytes { get; private set; }
        internal int RecoveredFrame { get; private set; }
        internal int ResyncRequests { get; private set; }

        internal void Observe(StateSyncPush push)
        {
            var body = StateSyncSnapshotWireCodec.Serialize(push);
            ProducedBytes += body.Count;
            if (push.Frame == DroppedFrame) return;

            var snapshot = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(body);
            var projectionVersion = snapshot.SchemaVersion;
            if (snapshot.Frame == 1) _admission.Reset(World);
            if (useFixedBaseline)
            {
                Assert.True(FixedBaselineStateSyncProtocol.TryDecode(snapshot.Payload, out _, out var snapshotId,
                    out var baseId, out projectionVersion, out _));
                if (!snapshot.IsFullSnapshot && baseId != _baselineId)
                {
                    ResyncRequests++;
                    return;
                }
                if (snapshot.IsFullSnapshot) _baselineId = snapshotId;
                else _actorX = _baselineX;
            }

            var admitted = _admission.Admit(snapshot.WorldId, snapshot.Frame, snapshot.IsFullSnapshot, projectionVersion,
                explicitBaselineAndOrderValidated: useFixedBaseline);
            if (admitted.ShouldRequestFullResync) ResyncRequests++;
            Assert.True(admitted.Accepted);
            if (snapshot.Actors != null)
            {
                foreach (var actor in snapshot.Actors)
                    if (actor.ActorId == 1) _actorX = actor.X;
            }
            if (useFixedBaseline && snapshot.IsFullSnapshot) _baselineX = _actorX;
            if (snapshot.Frame > DroppedFrame && RecoveredFrame == 0 && _actorX == 1f)
                RecoveredFrame = snapshot.Frame;
        }
    }

    private sealed class SampleProjection : IActorProjectionProducer
    {
        internal float FirstActorX { get; set; }

        public ActorProjectionData ExtractFull(int actorId) => new(actorId,
            actorId == 1 ? FirstActorX : 0f, 0f, 0f, 0f, 0f, 0f, 1f, 1f, 1f, 1f, 100f, 100f, 1, 0f, 0f,
            1, 1, 0, ActorProjectionFields.All);

        public void ExtractAll(List<ActorProjectionData> buffer)
        {
            for (var actorId = 1; actorId <= 32; actorId++) buffer.Add(ExtractFull(actorId));
        }

        public ActorProjectionData ExtractSpawn(int actorId) => ExtractFull(actorId);
    }
}
