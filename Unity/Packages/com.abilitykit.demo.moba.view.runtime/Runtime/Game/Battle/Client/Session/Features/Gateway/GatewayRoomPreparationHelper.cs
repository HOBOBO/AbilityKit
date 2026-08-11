using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Agent;

namespace AbilityKit.Game.Flow
{
    internal static class GatewayRoomPreparationHelper
    {
        public static bool ShouldPrepareGatewayRoom(BattleStartPlan plan)
        {
            var gateway = plan.Gateway;
            if (plan.HostMode != BattleHostMode.GatewayRemote) return false;
            if (!gateway.UseGatewayTransport) return false;
            if (!gateway.AutoCreateRoom && !gateway.AutoJoinRoom) return false;
            return true;
        }

        public static string ResolveJoinRoomId(BattleStartPlan plan)
        {
            var world = plan.World;
            var gateway = plan.Gateway;
            var joinRoomId = gateway.JoinRoomId;
            if (string.IsNullOrWhiteSpace(joinRoomId))
            {
                joinRoomId = gateway.NumericRoomId != 0 ? gateway.NumericRoomId.ToString() : world.WorldId;
            }

            if (string.IsNullOrWhiteSpace(joinRoomId))
            {
                throw new InvalidOperationException("GatewayAutoJoinRoom requires JoinRoomId or WorldId.");
            }

            return joinRoomId;
        }

        public static string ResolveCreatedRoomWorldId(in GatewayCreateRoomResult result)
        {
            if (result.NumericRoomId == 0)
            {
                throw new InvalidOperationException($"Gateway CreateRoom returned invalid NumericRoomId. roomId='{result.RoomId}'");
            }

            return result.NumericRoomId.ToString();
        }

        public static string ResolveCreatedRoomJoinRoomId(in GatewayCreateRoomResult result, ulong numericRoomId)
        {
            if (!string.IsNullOrWhiteSpace(result.RoomId)) return result.RoomId;
            return numericRoomId.ToString();
        }

        public static string ResolveJoinedRoomWorldId(in GatewayJoinRoomResult result, string joinRoomId)
        {
            if (result.NumericRoomId == 0)
            {
                throw new InvalidOperationException($"Gateway JoinRoom returned invalid NumericRoomId. roomId='{joinRoomId}'");
            }

            return result.NumericRoomId.ToString();
        }

        public static bool TryRecordWorldStartAnchor(
            IDictionary<WorldId, GatewayWorldStartAnchor> anchors,
            WorldId worldId,
            in GatewayWorldStartAnchor anchor)
        {
            if (anchors == null) throw new ArgumentNullException(nameof(anchors));
            if (anchor.ServerTickFrequency == 0) return false;

            anchors[worldId] = anchor;
            return true;
        }
    }
}
