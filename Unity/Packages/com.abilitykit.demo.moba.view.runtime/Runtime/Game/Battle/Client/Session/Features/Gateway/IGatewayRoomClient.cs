using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Battle.Agent;

namespace AbilityKit.Game.Flow
{
    public interface IGatewayRoomClient
    {
        Task<GatewayTimeSyncResult> TimeSyncAsync(uint timeSyncOpCode, long clientSendTicks, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
        Task<string> GuestLoginAsync(uint guestLoginOpCode, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<GatewayCreateRoomResult> CreateRoomAsync(
            string sessionToken,
            string region,
            string serverId,
            string roomType,
            string title,
            bool isPublic,
            int maxPlayers,
            IReadOnlyDictionary<string, string> tags,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<GatewayJoinRoomResult> JoinRoomAsync(
            string sessionToken,
            string region,
            string serverId,
            string roomId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<GatewayRoomSnapshotResult> SetReadyAsync(
            string sessionToken,
            string roomId,
            bool ready,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<GatewayRoomSnapshotResult> PickHeroAsync(
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
            CancellationToken cancellationToken = default);

        // ===== 阶段 5：资源加载屏障 / 状态查询 / 恢复 =====

        /// <summary>
        /// Owner 发起资源加载阶段（Lobby -> Loading）。
        /// </summary>
        Task<GatewayRoomOperationResult> BeginLoadingAsync(
            string sessionToken,
            string roomId,
            long? expectedRevision,
            string commandId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 成员上报资源加载完成。
        /// </summary>
        Task<GatewayRoomOperationResult> ReportAssetsLoadedAsync(
            string sessionToken,
            string roomId,
            long launchGeneration,
            int manifestVersion,
            string manifestHash,
            string commandId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<GatewayRoomOperationResult> ReportLoadingProgressAsync(
            string sessionToken,
            string roomId,
            long launchGeneration,
            int manifestVersion,
            string manifestHash,
            int progress,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Owner 取消加载阶段，回到 Lobby。
        /// </summary>
        Task<GatewayRoomOperationResult> CancelLoadingAsync(
            string sessionToken,
            string roomId,
            long? expectedRevision,
            string commandId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 查询 Room 当前快照。
        /// </summary>
        Task<GatewayGetSnapshotResult> GetSnapshotAsync(
            string sessionToken,
            string roomId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 恢复 Room 会话（断线重连）。
        /// </summary>
        Task<GatewayRestoreRoomResult> RestoreRoomAsync(
            string sessionToken,
            string region,
            string serverId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 反序列化 Room 状态变更推送为客户端快照。
        /// </summary>
        ClientRoomSnapshot DeserializeRoomStateChangedPush(ArraySegment<byte> payload);

        /// <summary>
        /// 判断 opcode 是否为 Room 状态变更推送。
        /// </summary>
        bool IsRoomStateChangedPush(uint opCode);
    }

    /// <summary>
    /// Room 操作统一结果（BeginLoading / ReportAssetsLoaded / CancelLoading 共用）。
    /// </summary>
    public readonly struct GatewayRoomOperationResult
    {
        public readonly bool Success;
        public readonly bool Applied;
        public readonly int ErrorCode;
        public readonly string Message;
        public readonly long RoomRevision;
        public readonly ClientRoomSnapshot Snapshot;

        public GatewayRoomOperationResult(bool success, bool applied, int errorCode, string message, long roomRevision, ClientRoomSnapshot snapshot)
        {
            Success = success;
            Applied = applied;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
            RoomRevision = roomRevision;
            Snapshot = snapshot;
        }
    }

    /// <summary>
    /// Room 快照查询结果（GetSnapshot）。
    /// </summary>
    public readonly struct GatewayGetSnapshotResult
    {
        public readonly bool Success;
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly ClientRoomSnapshot Snapshot;
        public readonly string Message;

        public GatewayGetSnapshotResult(bool success, string roomId, ulong numericRoomId, ClientRoomSnapshot snapshot, string message)
        {
            Success = success;
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            Snapshot = snapshot;
            Message = message ?? string.Empty;
        }
    }

    /// <summary>
    /// Room 恢复结果（RestoreRoom）。
    /// </summary>
    public readonly struct GatewayRestoreRoomResult
    {
        public readonly bool Success;
        public readonly bool HasActiveRoom;
        public readonly bool IsInBattle;
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly ClientRoomSnapshot Snapshot;
        public readonly GatewayWorldStartAnchor WorldStartAnchor;
        public readonly string Message;
        public readonly RoomGatewayJoinKind JoinKind;
        public readonly long ServerNowTicks;
        public readonly uint CurrentPlayerId;

        public GatewayRestoreRoomResult(
            bool success,
            bool hasActiveRoom,
            bool isInBattle,
            string roomId,
            ulong numericRoomId,
            ClientRoomSnapshot snapshot,
            in GatewayWorldStartAnchor worldStartAnchor,
            string message,
            RoomGatewayJoinKind joinKind,
            long serverNowTicks,
            uint currentPlayerId)
        {
            Success = success;
            HasActiveRoom = hasActiveRoom;
            IsInBattle = isInBattle;
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            Snapshot = snapshot;
            WorldStartAnchor = worldStartAnchor;
            Message = message ?? string.Empty;
            JoinKind = joinKind;
            ServerNowTicks = serverNowTicks;
            CurrentPlayerId = currentPlayerId;
        }
    }

    /// <summary>
    /// Room 加入类型（与 wire WireRoomJoinKind 对齐）。
    /// </summary>
    public enum RoomGatewayJoinKind
    {
        TeamLobby = 0,
        Reconnect = 1,
        LateJoin = 2
    }
}
