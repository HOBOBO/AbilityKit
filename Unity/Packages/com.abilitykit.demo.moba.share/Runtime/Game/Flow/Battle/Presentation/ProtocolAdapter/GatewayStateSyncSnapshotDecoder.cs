using System;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Moba.Share
{
    public static class GatewayStateSyncSnapshotDecoder
    {
        public static GatewayStateSyncSnapshot Decode(ArraySegment<byte> payload)
        {
            var wire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(payload);
            return GatewayStateSyncSnapshotMapper.Map(in wire);
        }
    }
}
