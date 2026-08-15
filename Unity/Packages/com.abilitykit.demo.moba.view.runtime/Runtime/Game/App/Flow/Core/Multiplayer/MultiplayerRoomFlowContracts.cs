#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Room;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 多人房间流程状态，供 UI 和 Flow 观察。
    /// </summary>
    public enum MultiplayerRoomFlowState
    {
        /// <summary>未开始。</summary>
        Idle = 0,
        /// <summary>正在登录。</summary>
        LoggingIn = 1,
        /// <summary>正在创建房间。</summary>
        CreatingRoom = 2,
        /// <summary>正在加入房间。</summary>
        JoiningRoom = 3,
        /// <summary>在大厅，等待选英雄/Ready。</summary>
        InLobby = 4,
        /// <summary>正在加载资源。</summary>
        LoadingAssets = 5,
        /// <summary>等待服务端开战。</summary>
        WaitingForBattle = 6,
        /// <summary>已进入战斗。</summary>
        InBattle = 7,
        /// <summary>失败。</summary>
        Failed = 8,
        /// <summary>Waiting for the authoritative room leave command.</summary>
        LeavingRoom = 9
    }

    /// <summary>
    /// 多人房间流程的快照视图（纯 C#，零 Unity 依赖）。
    /// 由 <see cref="IRoomSnapshotProvider"/> 投影，供控制器与 UI 共享。
    /// </summary>
    public sealed class MultiplayerRoomSnapshot
    {
        public string RoomId { get; set; } = string.Empty;
        public string OwnerAccountId { get; set; } = string.Empty;
        public ulong NumericRoomId { get; set; }
        public MultiplayerRoomPhase Phase { get; set; }
        public string PhaseReason { get; set; } = string.Empty;
        public bool CanStart { get; set; }
        public string BattleId { get; set; } = string.Empty;
        public ulong WorldId { get; set; }
        public long LaunchGeneration { get; set; }
        public int LaunchManifestVersion { get; set; }
        public string LaunchManifestHash { get; set; } = string.Empty;
        public long LoadingDeadlineUnixMs { get; set; }
        public long RoomRevision { get; set; }
        public long LastEventSequence { get; set; }
        public string LastStartFailureCode { get; set; } = string.Empty;
        public IReadOnlyList<string> Members { get; set; } = Array.Empty<string>();
        public IReadOnlyList<MultiplayerRoomPlayerSnapshot> Players { get; set; } = Array.Empty<MultiplayerRoomPlayerSnapshot>();
        /// <summary>服务端为当前战斗代际声明的同步能力。</summary>
        public RoomGatewayNetworkSyncCapabilities? SyncCapabilities { get; set; }

        public bool AllPlayersReady
        {
            get
            {
                if (Players == null || Players.Count == 0) return false;
                for (var i = 0; i < Players.Count; i++)
                {
                    var player = Players[i];
                    if (!player.IsOnline || player.HeroId <= 0 || !player.LobbyReady) return false;
                }

                return true;
            }
        }
    }

    /// <summary>
    /// Accepts an authoritative battle identity once so normal start and reconnect use the same entry gate.
    /// </summary>
    public sealed class MultiplayerBattleEntryGate
    {
        private string _roomId = string.Empty;
        private string _battleId = string.Empty;
        private ulong _worldId;
        private long _launchGeneration = -1;

        public bool TryAccept(
            MultiplayerRoomFlowState flowState,
            MultiplayerRoomSnapshot? snapshot)
        {
            if (!CanEnter(flowState, snapshot)) return false;
            if (string.Equals(_roomId, snapshot!.RoomId, StringComparison.Ordinal) &&
                string.Equals(_battleId, snapshot.BattleId, StringComparison.Ordinal) &&
                _worldId == snapshot.WorldId &&
                _launchGeneration == snapshot.LaunchGeneration)
            {
                return false;
            }

            _roomId = snapshot.RoomId;
            _battleId = snapshot.BattleId;
            _worldId = snapshot.WorldId;
            _launchGeneration = snapshot.LaunchGeneration;
            return true;
        }

        public void Reset()
        {
            _roomId = string.Empty;
            _battleId = string.Empty;
            _worldId = 0UL;
            _launchGeneration = -1;
        }

        public static bool CanEnter(
            MultiplayerRoomFlowState flowState,
            MultiplayerRoomSnapshot? snapshot)
        {
            return flowState == MultiplayerRoomFlowState.InBattle &&
                   snapshot?.Phase == MultiplayerRoomPhase.InBattle &&
                   !string.IsNullOrWhiteSpace(snapshot.RoomId) &&
                   snapshot.NumericRoomId > 0UL &&
                   !string.IsNullOrWhiteSpace(snapshot.BattleId) &&
                   snapshot.WorldId > 0UL;
        }
    }

    public sealed class MultiplayerRoomPlayerSnapshot
    {
        public string AccountId { get; set; } = string.Empty;
        public uint PlayerId { get; set; }
        public int TeamId { get; set; }
        public int HeroId { get; set; }
        public int SpawnPointId { get; set; }
        public int Level { get; set; }
        public int AttributeTemplateId { get; set; }
        public int BasicAttackSkillId { get; set; }
        public IReadOnlyList<int> SkillIds { get; set; } = Array.Empty<int>();
        public bool LobbyReady { get; set; }
        public bool AssetsLoaded { get; set; }
        public int LoadingProgress { get; set; }
        public bool IsOnline { get; set; }
        public long JoinOrdinal { get; set; }
        public int LoadedManifestVersion { get; set; }
        public string LoadedManifestHash { get; set; } = string.Empty;
        public long LastSeenTicks { get; set; }
        public long OfflineSinceTicks { get; set; }
    }

    /// <summary>
    /// 多人房间阶段（与服务端 RoomPhase 对齐的纯 C# 镜像）。
    /// </summary>
    public enum MultiplayerRoomPhase
    {
        Lobby = 0,
        Loading = 1,
        Starting = 2,
        InBattle = 3,
        Closing = 4,
        Closed = 5,
        Expired = 6
    }

    public enum MultiplayerRoomRestoreNextStep
    {
        None = 0,
        SetReadyAndBeginLoading = 1,
        ReportAssetsLoaded = 2,
        WaitForBattleStart = 3,
        EnterBattle = 4
    }

    public enum MultiplayerRoomRestoreStatus
    {
        Restored = 0,
        NoActiveRoom = 1,
        NotMember = 2,
        RoomClosed = 3,
        RoomExpired = 4,
        InvalidSession = 5,
        Timeout = 6,
        Failed = 7
    }

    public enum MultiplayerRoomRestoreErrorCode
    {
        None = 0,
        NoAccountRoomMapping = 1,
        AccountNotInRoom = 2,
        RoomClosed = 3,
        RoomExpired = 4,
        InvalidSession = 5,
        Timeout = 6,
        InternalError = 7
    }

    public enum MultiplayerRoomEntryKind
    {
        TeamLobby = 0,
        Reconnect = 1,
        LateJoin = 2
    }

    public readonly struct MultiplayerRoomRestoreResult
    {
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly uint PlayerId;
        public readonly MultiplayerRoomPhase Phase;
        public readonly MultiplayerRoomRestoreNextStep NextStep;
        public readonly MultiplayerRoomEntryKind EntryKind;
        public readonly bool CanStart;
        public readonly string Message;
        public readonly MultiplayerRoomRestoreStatus Status;
        public readonly MultiplayerRoomRestoreErrorCode ErrorCode;

        public MultiplayerRoomRestoreResult(
            string roomId,
            ulong numericRoomId,
            uint playerId,
            MultiplayerRoomPhase phase,
            MultiplayerRoomRestoreNextStep nextStep,
            MultiplayerRoomEntryKind entryKind,
            bool canStart,
            string message,
            MultiplayerRoomRestoreStatus status,
            MultiplayerRoomRestoreErrorCode errorCode)
        {
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            PlayerId = playerId;
            Phase = phase;
            NextStep = nextStep;
            EntryKind = entryKind;
            CanStart = canStart;
            Message = message ?? string.Empty;
            Status = status;
            ErrorCode = errorCode;
        }

        public bool HasActiveRoom =>
            Status == MultiplayerRoomRestoreStatus.Restored &&
            !string.IsNullOrWhiteSpace(RoomId);

        public bool CanRetry =>
            Status == MultiplayerRoomRestoreStatus.Timeout ||
            (Status == MultiplayerRoomRestoreStatus.Failed &&
             ErrorCode == MultiplayerRoomRestoreErrorCode.InternalError);
    }

    public readonly struct MultiplayerRoomJoinResult
    {
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly uint PlayerId;

        public MultiplayerRoomJoinResult(string roomId, ulong numericRoomId, uint playerId)
        {
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            PlayerId = playerId;
        }
    }

    /// <summary>
    /// 选英雄/配置出战的参数。
    /// </summary>
    public readonly struct MultiplayerLoadoutSpec
    {
        public readonly int HeroId;
        public readonly int TeamId;
        public readonly int SpawnPointId;
        public readonly int Level;
        public readonly int AttributeTemplateId;
        public readonly int BasicAttackSkillId;
        public readonly int[] SkillIds;

        public MultiplayerLoadoutSpec(
            int heroId,
            int teamId,
            int spawnPointId,
            int level,
            int attributeTemplateId,
            int basicAttackSkillId,
            int[]? skillIds)
        {
            HeroId = heroId;
            TeamId = teamId;
            SpawnPointId = spawnPointId;
            Level = level;
            AttributeTemplateId = attributeTemplateId;
            BasicAttackSkillId = basicAttackSkillId;
            SkillIds = skillIds ?? Array.Empty<int>();
        }
    }

    /// <summary>
    /// 创建/加入房间的启动参数（对应 RoomGatewayLaunchSpec 的纯 C# 子集）。
    /// </summary>
    public sealed class MultiplayerRoomLaunchSpec
    {
        public string SessionToken { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string ServerId { get; set; } = string.Empty;
        public string RoomType { get; set; } = "default";
        public string RoomTitle { get; set; } = string.Empty;
        public int MaxPlayers { get; set; } = 2;
        public int MinPlayers { get; set; } = 2;
        public int GameplayId { get; set; } = 1;
        public int RuleSetId { get; set; } = 1;
        public int ConfigVersion { get; set; } = 1;
        public int ProtocolVersion { get; set; } = 1;
        public string WorldType { get; set; } = "moba";
        public string ClientId { get; set; } = "moba-client";
    }

    /// <summary>
    /// 抽象 RoomGatewaySessionFlow 的分阶段 API，使控制器可测试（零 host.extension 依赖）。
    /// </summary>
    public interface IMultiplayerRoomSession
    {
        /// <summary>Restores the authoritative room and reports the first unfinished stage.</summary>
        Task<MultiplayerRoomRestoreResult> RestoreAsync(
            MultiplayerRoomLaunchSpec spec,
            uint fallbackPlayerId,
            CancellationToken cancellationToken);

        /// <summary>阶段 1：创建房间，返回 roomId。</summary>
        Task<string> CreateRoomAsync(MultiplayerRoomLaunchSpec spec, CancellationToken cancellationToken);

        /// <summary>阶段 2：加入房间。</summary>
        Task<MultiplayerRoomJoinResult> JoinRoomAsync(
            MultiplayerRoomLaunchSpec spec,
            string roomId,
            CancellationToken cancellationToken);

        /// <summary>Leaves the authoritative room membership.</summary>
        Task LeaveRoomAsync(string roomId, CancellationToken cancellationToken);

        /// <summary>阶段 3：配置出战（PickHero）。</summary>
        Task ConfigureLoadoutAsync(string roomId, MultiplayerLoadoutSpec loadout, CancellationToken cancellationToken);

        /// <summary>阶段 4：设置准备状态。</summary>
        Task SetReadyAsync(string roomId, bool ready, CancellationToken cancellationToken);

        /// <summary>阶段 5：Owner 发起资源加载阶段。</summary>
        Task BeginLoadingAsync(string roomId, CancellationToken cancellationToken);

        /// <summary>阶段 6：成员上报资源加载完成。</summary>
        Task ReportAssetsLoadedAsync(string roomId, CancellationToken cancellationToken);

        /// <summary>Reports monotonic local loading progress to the authoritative room snapshot.</summary>
        Task ReportLoadingProgressAsync(string roomId, int progress, CancellationToken cancellationToken);

        /// <summary>Owner cancels the current loading generation and returns the room to Lobby.</summary>
        Task CancelLoadingAsync(string roomId, CancellationToken cancellationToken);

        /// <summary>阶段 7：等待战斗开始（轮询直到 Phase 进入 Starting/InBattle 或超时）。</summary>
        Task WaitForBattleStartAsync(string roomId, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 抽象 ClientRoomStore 的快照订阅能力，使控制器可测试。
    /// </summary>
    public interface IRoomSnapshotProvider
    {
        /// <summary>当前快照（或 null）。</summary>
        MultiplayerRoomSnapshot? Current { get; }

        /// <summary>快照变更事件。</summary>
        event Action<MultiplayerRoomSnapshot>? OnSnapshotChanged;
    }

    public interface IMultiplayerBattleAssetLoader
    {
        Task LoadAsync(
            MultiplayerRoomSnapshot snapshot,
            IProgress<MultiplayerAssetLoadProgress> progress,
            CancellationToken cancellationToken);
        void Release();
    }

    public readonly struct MultiplayerAssetLoadProgress
    {
        public readonly int Progress;
        public readonly int LoadedCount;
        public readonly int TotalCount;
        public readonly string CurrentAssetKey;

        public MultiplayerAssetLoadProgress(int progress, int loadedCount, int totalCount, string currentAssetKey)
        {
            Progress = Math.Max(0, Math.Min(100, progress));
            LoadedCount = loadedCount;
            TotalCount = totalCount;
            CurrentAssetKey = currentAssetKey ?? string.Empty;
        }
    }
}
