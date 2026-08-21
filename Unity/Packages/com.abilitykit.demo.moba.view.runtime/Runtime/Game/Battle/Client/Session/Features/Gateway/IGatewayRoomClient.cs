using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Demo.Common.Rooms;

namespace AbilityKit.Game.Flow
{
    public interface IGatewayAuthenticationCapability
    {
        Task<string> GuestLoginAsync(
            uint guestLoginOpCode,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);
    }

    public interface IGatewayClockCapability
    {
        Task<GatewayTimeSyncResult> TimeSyncAsync(
            uint timeSyncOpCode,
            long clientSendTicks,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);
    }

    public interface IGatewayRoomCommandCapability
    {
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

        Task<GatewayRoomOperationResult> BeginLoadingAsync(
            string sessionToken,
            string roomId,
            long? expectedRevision,
            string commandId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<GatewayRoomOperationResult> ReportAssetsLoadedAsync(
            string sessionToken,
            string roomId,
            long launchGeneration,
            int manifestVersion,
            string manifestHash,
            string commandId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<GatewayRoomOperationResult> LeaveRoomAsync(
            string sessionToken,
            string roomId,
            long? expectedRevision,
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

        Task<GatewayRoomOperationResult> CancelLoadingAsync(
            string sessionToken,
            string roomId,
            long? expectedRevision,
            string commandId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);
    }

    public interface IGatewayRoomRecoveryQueryCapability
    {
        Task<GatewayGetSnapshotResult> GetSnapshotAsync(
            string sessionToken,
            string roomId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<GatewayRestoreRoomResult> RestoreRoomAsync(
            string sessionToken,
            string region,
            string serverId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);
    }

    public interface IGatewayRoomPushDecodingCapability
    {
        ClientRoomSnapshot DeserializeRoomStateChangedPush(ArraySegment<byte> payload);
        bool IsRoomStateChangedPush(uint opCode);
    }

    public interface IGatewayRoomClient :
        IGatewayAuthenticationCapability,
        IGatewayClockCapability,
        IGatewayRoomCommandCapability,
        IGatewayRoomRecoveryQueryCapability,
        IGatewayRoomPushDecodingCapability,
        IDisposable
    {
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
