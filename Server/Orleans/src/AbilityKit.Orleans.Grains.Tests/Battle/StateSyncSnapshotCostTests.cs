using System.Diagnostics;
using AbilityKit.Ability.Host.Extensions.Server.BattleHost;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Battle.Projection;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Protocol;
using AbilityKit.Protocol.Room;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed partial class StateSyncSnapshotCostTests(ITestOutputHelper output)
{
    private const ulong World = 7;
    private const int TickRate = 30;
    private const int PublishEvery = 4;
    private const int FullEvery = 30;
    private const int ChangeFrame = 36;
    private const int LastOmittedFrame = 44;
    private const int MeasuredFrames = 600;

    [Theory]
    [InlineData(32, 1, false)]
    [InlineData(32, 1, true)]
    [InlineData(128, 64, false)]
    [InlineData(128, 64, true)]
    public void CompareCostAndRecovery(int actors, int changedActors, bool omitBurst)
    {
        foreach (var mode in Enum.GetValues<Mode>())
        {
            Run(mode, actors, changedActors, omitBurst, frames: MeasuredFrames);
            var result = Run(mode, actors, changedActors, omitBurst, frames: MeasuredFrames);

            var expectedRecovery = !omitBurst ? ChangeFrame : mode switch
            {
                Mode.FullOnly => 48,
                Mode.FixedBaseline => 52,
                Mode.Legacy => 60,
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };
            Assert.Equal(expectedRecovery, result.RecoveredFrame);
            Assert.Equal(omitBurst && mode == Mode.FixedBaseline ? 1 : 0, result.ResyncSignals);
            Assert.Equal(MeasuredFrames / PublishEvery, result.Build.Count);
            Assert.Equal(result.Build.Count - (omitBurst ? 3 : 0), result.Rebuild.Count);

            output.WriteLine(FormattableString.Invariant(
                $"{actors}/{changedActors}, burst={omitBurst}, {mode}: {result.WireBytes * TickRate / MeasuredFrames} B/s, resync={result.ResyncSignals}, convergence={result.RecoveredFrame - ChangeFrame} ticks, build+encode {result.Build}, decode+rebuild {result.Rebuild}"));
        }
    }

    private static Result Run(Mode mode, int actors, int changedActors, bool omitBurst, int frames)
    {
        var policy = new BattleSnapshotSyncPolicy(PublishEvery, FullEvery);
        var mapper = DefaultOrleansBattleProtocolMapper.Instance;
        var producer = new SampleProjection(actors, changedActors);
        var projectionBuffer = new List<ActorProjectionData>(actors);
        var legacyCache = mode == Mode.Legacy ? new Dictionary<int, ActorProjectionData>(actors) : null;
        var fixedStream = mode == Mode.FixedBaseline ? new FixedBaselineSnapshotStream(TickRate, FullEvery) : null;
        var admission = new MobaSnapshotAdmission();
        var state = new MobaAuthoritativeSnapshotState();
        admission.Reset(World);
        var result = new Result(frames / PublishEvery);

        for (var frame = 1; frame <= frames; frame++)
        {
            if (!policy.ShouldPublish(frame, observerCount: 1, worldTicked: true)) continue;

            producer.AfterChange = frame >= ChangeFrame;
            var legacyFull = frame == PublishEvery || policy.ShouldCreateFullSnapshot(frame);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            var push = mapper.CreateStateSyncPushFromProjection(World, frame, producer,
                isFullSnapshot: mode != Mode.Legacy || legacyFull, projectionBuffer, legacyCache);
            if (fixedStream != null && !fixedStream.TryEncode(push, legacyFull, out push, out _, out _))
                throw new InvalidOperationException("Fixture exceeded the snapshot budget.");
            var bytes = StateSyncSnapshotWireCodec.Serialize(push);
            result.Build.Record(start, before);
            result.WireBytes += bytes.Count;

            if (omitBurst && frame is >= ChangeFrame and <= LastOmittedFrame) continue;

            before = GC.GetAllocatedBytesForCurrentThread();
            start = Stopwatch.GetTimestamp();
            var wire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(bytes);
            var snapshot = GatewayStateSyncSnapshotMapper.Map(in wire);
            var materialized = Rebuild(snapshot, state, admission, out var requestFull);
            result.Rebuild.Record(start, before);

            if (requestFull)
            {
                result.ResyncSignals++;
                fixedStream?.RequestFull();
            }

            if (materialized is not { } view) continue;
            Assert.Equal(actors, view.Actors.Length);
            var matches = true;
            for (var index = 0; index < actors; index++)
            {
                var actor = view.Actors[index];
                Assert.Equal(index + 1, actor.ActorId);
                matches &= actor.X == producer.ExtractFull(actor.ActorId).PosX;
            }
            if (frame < ChangeFrame || result.RecoveredFrame != 0) Assert.True(matches);
            else if (matches) result.RecoveredFrame = frame;
        }

        return result;
    }

    private static GatewayStateSyncSnapshot? Rebuild(in GatewayStateSyncSnapshot snapshot,
        MobaAuthoritativeSnapshotState state, MobaSnapshotAdmission admission, out bool requestFull)
    {
        requestFull = false;
        if (!snapshot.UsesFixedBaseline)
        {
            var decision = admission.Admit(snapshot.WorldId, snapshot.Frame, snapshot.IsFullSnapshot, snapshot.SchemaVersion);
            requestFull = decision.ShouldRequestFullResync;
            return decision.Accepted ? state.Apply(in snapshot) : null;
        }

        var status = state.PrepareFixed(in snapshot, out var materialized);
        if (status != MobaSnapshotAdmissionStatus.Accepted)
        {
            requestFull = status == MobaSnapshotAdmissionStatus.BaselineRequired;
            if (requestFull) admission.RequireFullBaseline();
            return null;
        }

        var check = admission.CheckFixed(in snapshot);
        requestFull = check.ShouldRequestFullResync;
        if (!check.Accepted) return null;
        if (!admission.CommitFixed(in snapshot).Accepted)
            throw new InvalidOperationException("Admission changed during synchronous reconstruction.");
        state.CommitFixed(in snapshot, in materialized);
        return materialized;
    }

    private enum Mode { FullOnly, FixedBaseline, Legacy }

    private sealed class Result(int capacity)
    {
        internal readonly Samples Build = new(capacity);
        internal readonly Samples Rebuild = new(capacity);
        internal long WireBytes;
        internal int RecoveredFrame;
        internal int ResyncSignals;
    }

    private sealed class Samples(int capacity)
    {
        private readonly long[] _duration = new long[capacity];
        private long _allocated;

        internal int Count { get; private set; }
        internal long TotalAllocated => _allocated;

        internal void Record(long start, long before)
        {
            _duration[Count++] = Stopwatch.GetTimestamp() - start;
            _allocated += GC.GetAllocatedBytesForCurrentThread() - before;
        }

        public override string ToString()
        {
            Array.Sort(_duration, 0, Count);
            var p95 = _duration[(int)Math.Ceiling(Count * 0.95) - 1] * 1_000_000d / Stopwatch.Frequency;
            return FormattableString.Invariant($"p95={p95:F1}us, alloc={_allocated / Count} B/op (n={Count})");
        }
    }

    private sealed class SampleProjection(int actors, int changedActors) : IActorProjectionProducer
    {
        public bool AfterChange { get; set; }

        public ActorProjectionData ExtractFull(int actorId) => new(actorId,
            AfterChange && actorId <= changedActors ? 1f : 0f, 0f, 0f, 0f, 0f, 0f, 1f, 1f, 1f, 1f, 100f, 100f,
            1, 0f, 0f, 1, 1, 0, ActorProjectionFields.All);

        public void ExtractAll(List<ActorProjectionData> buffer)
        {
            for (var actorId = 1; actorId <= actors; actorId++) buffer.Add(ExtractFull(actorId));
        }

        public ActorProjectionData ExtractSpawn(int actorId) => ExtractFull(actorId);
    }
}
