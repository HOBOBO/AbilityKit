using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Orleans.Grains.Battle;

/// <summary>
/// 发布预检与观察者交付共用的房间快照编码。
/// </summary>
internal static class StateSyncSnapshotWireCodec
{
    /// <summary>
    /// 返回临时编码缓冲区；调用者应立即读长度或复制，不跨后续编码保存。
    /// </summary>
    internal static ArraySegment<byte> Serialize(StateSyncPush push)
    {
        var source = push.Actors;
        List<WireStateSyncActorSnapshot>? actors = null;
        if (source is { Count: > 0 })
        {
            actors = new List<WireStateSyncActorSnapshot>(source.Count);
            foreach (var actor in source)
            {
                actors.Add(new WireStateSyncActorSnapshot
                {
                    ActorId = actor.ActorId,
                    X = actor.X,
                    Y = actor.Y,
                    Z = actor.Z,
                    Rotation = actor.Rotation,
                    VelocityX = actor.VelocityX,
                    VelocityZ = actor.VelocityZ,
                    Hp = actor.Hp,
                    HpMax = actor.HpMax,
                    TeamId = actor.TeamId,
                    Kind = actor.Kind,
                    Code = actor.Code,
                    OwnerNetId = actor.OwnerNetId
                });
            }
        }

        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = push.WorldId,
            Frame = push.Frame,
            Timestamp = push.Timestamp,
            IsFullSnapshot = push.IsFullSnapshot,
            Actors = actors,
            PayloadOpCode = push.PayloadOpCode,
            Payload = push.Payload,
            ServerTicks = push.ServerTicks,
            EventWatermark = push.EventWatermark,
            SchemaVersion = push.SchemaVersion,
            RemovedActorIds = push.RemovedActorIds is { Count: > 0 } ? new List<int>(push.RemovedActorIds) : null,
            EventEpoch = push.EventEpoch ?? string.Empty
        };
        return WireRoomGatewayBinary.Serialize(in wire);
    }
}
