using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Protocol.Room;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace AbilityKit.Orleans.Grains.Battle;

internal enum FixedBaselineFullReason
{
    None,
    Initial,
    Periodic,
    RefreshRequested,
    DeltaCost
}

/// <summary>
/// 从单份不可变的全量投影编码可替换的增量；定向请求只排队，不修改基线。
/// </summary>
internal sealed class FixedBaselineSnapshotStream
{
    private readonly Guid _generation = Guid.NewGuid();
    private readonly int? _fullSnapshotIntervalFrames;
    private readonly int _minimumRefreshIntervalFrames;
    private Dictionary<int, ActorValue>? _baseline;
    private long _baseSnapshotId;
    private long _lastSnapshotId;
    private int _lastFullFrame;
    private bool _fullRequested;

    /// <summary>
    /// 全量周期为空时保留调用方的绝对帧调度，供附属状态推送使用。
    /// </summary>
    internal FixedBaselineSnapshotStream(int tickRate, int? fullSnapshotIntervalFrames = null)
    {
        _fullSnapshotIntervalFrames = fullSnapshotIntervalFrames;
        _minimumRefreshIntervalFrames = Math.Max(1, tickRate / 4);
    }

    internal void RequestFull() => _fullRequested = true;

    /// <summary>
    /// 对候选完整消息做预算预检；只有可发布的消息才推进发布编号和共享基线。
    /// </summary>
    internal bool TryEncode(StateSyncPush projection, bool legacyScheduledFull,
        [NotNullWhen(true)] out StateSyncPush? push, out int wireBytes, out FixedBaselineFullReason fullReason) =>
        TryEncodeCore(projection, legacyScheduledFull, prepareDelivery: false, out push, out _, out wireBytes, out fullReason);

    internal bool TryPrepareDelivery(StateSyncPush projection, bool legacyScheduledFull,
        [NotNullWhen(true)] out PreparedFixedBaselineSnapshot? prepared, out int wireBytes, out FixedBaselineFullReason fullReason) =>
        TryEncodeCore(projection, legacyScheduledFull, prepareDelivery: true, out _, out prepared, out wireBytes, out fullReason)
        && prepared != null;

    private bool TryEncodeCore(StateSyncPush projection, bool legacyScheduledFull, bool prepareDelivery,
        [NotNullWhen(true)] out StateSyncPush? push, out PreparedFixedBaselineSnapshot? prepared,
        out int wireBytes, out FixedBaselineFullReason fullReason)
    {
        prepared = null;
        fullReason = GetFullReason(projection.Frame, legacyScheduledFull);
        var upserts = new List<ActorSnapshot>();
        var removals = new List<int>();
        if (fullReason == FixedBaselineFullReason.None)
        {
            var present = new HashSet<int>();
            foreach (var actor in projection.Actors)
            {
                present.Add(actor.ActorId);
                if (!_baseline!.TryGetValue(actor.ActorId, out var previous) || previous != ActorValue.From(actor))
                    upserts.Add(actor);
            }

            foreach (var actorId in _baseline!.Keys)
                if (!present.Contains(actorId)) removals.Add(actorId);

            if (13L * upserts.Count + removals.Count >= 13L * projection.Actors.Count &&
                (upserts.Count > 0 || removals.Count > 0))
                fullReason = FixedBaselineFullReason.DeltaCost;
        }

        var mustSendFull = fullReason != FixedBaselineFullReason.None;
        var snapshotId = checked(_lastSnapshotId + 1);
        var baseSnapshotId = mustSendFull ? 0 : _baseSnapshotId;
        var encoded = FixedBaselineStateSyncProtocol.Encode(
            _generation, snapshotId, baseSnapshotId, projection.SchemaVersion, projection.Payload);

        projection.IsFullSnapshot = mustSendFull;
        if (!mustSendFull) projection.Actors = upserts;
        projection.RemovedActorIds = mustSendFull ? new List<int>() : removals;
        projection.SchemaVersion = FixedBaselineStateSyncProtocol.SchemaVersion;
        projection.Payload = encoded;
        var wire = StateSyncSnapshotWireCodec.Serialize(projection);
        wireBytes = wire.Count;
        if (wireBytes > FixedBaselineStateSyncProtocol.MaximumSnapshotBytes)
        {
            push = null;
            return false;
        }

        if (prepareDelivery)
            prepared = new PreparedFixedBaselineSnapshot(projection.Frame, mustSendFull, projection.NetworkEnvironmentId,
                ImmutableCollectionsMarshal.AsImmutableArray(wire.ToArray()))
            {
                Revision = new FixedBaselineSnapshotRevision(projection.WorldId, _generation, snapshotId, baseSnapshotId)
            };

        if (mustSendFull)
        {
            _baseline = projection.Actors.ToDictionary(actor => actor.ActorId, ActorValue.From);
            _baseSnapshotId = snapshotId;
            _lastFullFrame = projection.Frame;
            _fullRequested = false;
        }

        _lastSnapshotId = snapshotId;
        push = projection;
        return true;
    }

    private FixedBaselineFullReason GetFullReason(int frame, bool legacyScheduledFull)
    {
        if (_baseline == null) return FixedBaselineFullReason.Initial;

        var elapsedFrames = (long)frame - _lastFullFrame;
        var periodicDue = _fullSnapshotIntervalFrames is int interval
            ? elapsedFrames >= interval
            : legacyScheduledFull;
        if (periodicDue) return FixedBaselineFullReason.Periodic;
        if (_fullRequested && elapsedFrames >= _minimumRefreshIntervalFrames)
            return FixedBaselineFullReason.RefreshRequested;

        return FixedBaselineFullReason.None;
    }

    private readonly record struct ActorValue(
        float X, float Y, float Z, float Rotation, float VelocityX, float VelocityZ,
        float Hp, float HpMax, int TeamId, int Kind, int Code, int OwnerNetId)
    {
        internal static ActorValue From(ActorSnapshot actor) => new(
            actor.X, actor.Y, actor.Z, actor.Rotation, actor.VelocityX, actor.VelocityZ,
            actor.Hp, actor.HpMax, actor.TeamId, actor.Kind, actor.Code, actor.OwnerNetId);
    }
}
