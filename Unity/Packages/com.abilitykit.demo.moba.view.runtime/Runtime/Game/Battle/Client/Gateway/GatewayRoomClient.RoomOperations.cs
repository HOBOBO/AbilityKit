using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Room;

namespace AbilityKit.Game.Battle.Agent
{
    public sealed partial class GatewayRoomClient
    {
        public async Task<GatewayCreateRoomResult> CreateRoomAsync(
            string sessionToken,
            string region,
            string serverId,
            string roomType,
            string title,
            bool isPublic,
            int maxPlayers,
            IReadOnlyDictionary<string, string> tags,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(region)) throw new ArgumentException("region is required.", nameof(region));
            if (string.IsNullOrWhiteSpace(serverId)) throw new ArgumentException("serverId is required.", nameof(serverId));
            if (string.IsNullOrWhiteSpace(roomType)) roomType = "battle";
            if (title == null) title = string.Empty;

            var result = await _roomSessionClient.CreateRoomAsync(
                new RoomGatewayCreateRequest(
                    sessionToken,
                    region,
                    serverId,
                    roomType,
                    title,
                    isPublic,
                    maxPlayers,
                    tags),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return new GatewayCreateRoomResult(result.RoomId, result.NumericRoomId);
        }

        public async Task<GatewayJoinRoomResult> JoinRoomAsync(
            string sessionToken,
            string region,
            string serverId,
            string roomId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(region)) throw new ArgumentException("region is required.", nameof(region));
            if (string.IsNullOrWhiteSpace(serverId)) throw new ArgumentException("serverId is required.", nameof(serverId));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var result = await _roomSessionClient.JoinRoomAsync(
                new RoomGatewayJoinRequest(sessionToken, region, serverId, roomId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            var anchor = ToGatewayAnchor(in result.WorldStartAnchor);
            return new GatewayJoinRoomResult(
                result.Success,
                result.RoomId,
                result.NumericRoomId,
                string.Empty,
                in anchor,
                result.Message,
                result.BattleId,
                result.CanStart,
                result.ServerNowTicks,
                result.WorldId,
                result.CurrentPlayerId);
        }

        public async Task<GatewayRoomSnapshotResult> SetReadyAsync(
            string sessionToken,
            string roomId,
            bool ready,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var result = await _roomSessionClient.SetReadyAsync(
                new RoomGatewayReadyRequest(sessionToken, roomId, ready),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return new GatewayRoomSnapshotResult(
                result.Success,
                result.Applied,
                result.ErrorCode,
                result.Message,
                result.RoomId,
                result.NumericRoomId);
        }

        public async Task<GatewayRoomSnapshotResult> PickHeroAsync(
            string sessionToken,
            string roomId,
            int heroId,
            int teamId,
            int spawnPointId,
            int level,
            int attributeTemplateId,
            int basicAttackSkillId,
            IReadOnlyList<int> skillIds,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var result = await _roomSessionClient.PickHeroAsync(
                new RoomGatewayPickHeroRequest(
                    sessionToken,
                    roomId,
                    heroId,
                    teamId,
                    spawnPointId,
                    level,
                    attributeTemplateId,
                    basicAttackSkillId,
                    skillIds),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return new GatewayRoomSnapshotResult(
                result.Success,
                result.Applied,
                result.ErrorCode,
                result.Message,
                result.RoomId,
                result.NumericRoomId);
        }

        public async Task<GatewayStartBattleResult> StartBattleAsync(
            string sessionToken,
            string roomId,
            int gameplayId,
            int ruleSetId,
            int configVersion,
            int protocolVersion,
            string worldType,
            string clientId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var result = await _roomSessionClient.StartBattleAsync(
                new RoomGatewayStartBattleRequest(
                    sessionToken,
                    roomId,
                    gameplayId,
                    ruleSetId,
                    configVersion,
                    protocolVersion,
                    worldType,
                    clientId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return new GatewayStartBattleResult(result.BattleId, result.WorldId, result.Started);
        }
    }
}
