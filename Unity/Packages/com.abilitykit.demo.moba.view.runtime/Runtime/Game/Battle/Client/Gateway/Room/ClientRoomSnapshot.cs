using System;
using System.Collections.Generic;
using AbilityKit.Network.Room;

namespace AbilityKit.Game.Battle.Agent
{
    /// <summary>
    /// 客户端 Room 阶段枚举，与服务端 RoomPhase 完全对齐。
    /// </summary>
    public enum ClientRoomPhase
    {
        Lobby = 0,
        Loading = 1,
        Starting = 2,
        InBattle = 3,
        Closing = 4,
        Closed = 5,
        Expired = 6
    }

    /// <summary>
    /// 客户端 Room 玩家快照（纯 C# 模型，不依赖 wire 类型）。
    /// </summary>
    public sealed class ClientRoomPlayer
    {
        public string AccountId { get; set; } = string.Empty;
        public int TeamId { get; set; }
        public bool Ready { get; set; }
        public int HeroId { get; set; }
        public int SpawnPointId { get; set; }
        public int Level { get; set; }
        public int AttributeTemplateId { get; set; }
        public int BasicAttackSkillId { get; set; }
        public IReadOnlyList<int> SkillIds { get; set; } = Array.Empty<int>();
        public uint PlayerId { get; set; }

        // 阶段 4 append-only 字段
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
    /// 客户端 Room 快照（纯 C# 模型，不依赖 wire 类型）。
    /// 单一权威客户端 Room 状态视图。
    /// </summary>
    public sealed class ClientRoomSnapshot
    {
        public string RoomId { get; set; } = string.Empty;
        public string OwnerAccountId { get; set; } = string.Empty;
        public ClientRoomPhase Phase { get; set; }
        public string PhaseReason { get; set; } = string.Empty;
        public long LaunchGeneration { get; set; }
        public long LoadingDeadlineUnixMs { get; set; }
        public string LaunchManifestHash { get; set; } = string.Empty;
        public int LaunchManifestVersion { get; set; }
        public string LastStartFailureCode { get; set; } = string.Empty;
        public long RoomRevision { get; set; }
        public long LastEventSequence { get; set; }
        public bool CanStart { get; set; }
        public string BattleId { get; set; } = string.Empty;
        public ulong WorldId { get; set; }
        public ulong NumericRoomId { get; set; }
        public IReadOnlyList<string> Members { get; set; } = Array.Empty<string>();
        public IReadOnlyList<ClientRoomPlayer> Players { get; set; } = Array.Empty<ClientRoomPlayer>();
        public GatewayWorldStartAnchor WorldStartAnchor { get; set; }
        /// <summary>服务端为当前战斗代际声明的同步能力。</summary>
        public RoomGatewayNetworkSyncCapabilities SyncCapabilities { get; set; }

        /// <summary>
        /// 快照是否处于可恢复的活跃阶段（Lobby/Loading/Starting/InBattle）。
        /// </summary>
        public bool IsActive =>
            Phase == ClientRoomPhase.Lobby ||
            Phase == ClientRoomPhase.Loading ||
            Phase == ClientRoomPhase.Starting ||
            Phase == ClientRoomPhase.InBattle;
    }
}
