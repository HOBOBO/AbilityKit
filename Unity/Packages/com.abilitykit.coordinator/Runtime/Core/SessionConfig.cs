using System;
using System.Collections.Generic;
using AbilityKit.Coordinator.Core;

namespace AbilityKit.Coordinator
{
    /// <summary>
    /// 会话配置。
    /// </summary>
    public struct SessionConfig
    {
        // ============== 标识 ==============

        /// <summary>
        /// 会话标识。
        /// </summary>
        public SessionId SessionId;

        /// <summary>
        /// 地图或关卡标识。
        /// </summary>
        public int MapId;

        /// <summary>
        /// 世界标识。
        /// </summary>
        public int WorldId;

        /// <summary>
        /// 世界蓝图类型（例如 moba 的 "battle"）。
        /// </summary>
        public string WorldType;

        // ============== 玩家 ==============

        /// <summary>
        /// 本地玩家标识。
        /// </summary>
        public int LocalPlayerId;

        /// <summary>
        /// 客户端标识。
        /// </summary>
        public int ClientId;

        // ============== 同步模式 ==============

        /// <summary>
        /// 同步模式。
        /// </summary>
        public SyncMode SyncMode;

        /// <summary>
        /// 主机模式。
        /// </summary>
        public HostMode HostMode;

        /// <summary>
        /// 目标帧率（每秒帧数）。
        /// </summary>
        public int TickRate;

        // ============== 功能 ==============

        /// <summary>
        /// 要求协调器或本地同步适配器驱动逻辑世界前必须存在 ILogicWorldDriveGate。
        /// </summary>
        public bool RequireLogicWorldDriveGate;

        /// <summary>
        /// 协调器启动时通过 ISpawnService 创建玩家生成点。
        /// </summary>
        public bool UseCoordinatorSpawnService;

        /// <summary>
        /// 启用回放录制。
        /// </summary>
        public bool EnableReplayRecording;

        /// <summary>
        /// 启用回放播放。
        /// </summary>
        public bool EnableReplayPlayback;

        /// <summary>
        /// 启用客户端预测。
        /// </summary>
        public bool EnableClientPrediction;

        /// <summary>
        /// 最大预测超前帧数。
        /// </summary>
        public int MaxPredictionAheadFrames;

        // ============== 网络 ==============

        /// <summary>
        /// 远程连接使用的服务器端点。
        /// </summary>
        public NetworkEndpoint ServerEndpoint;

        /// <summary>
        /// 房间标识。
        /// </summary>
        public long RoomId;

        // ============== 子功能配置 ==============

        /// <summary>
        /// 子功能配置列表。
        /// </summary>
        public List<SubFeatureConfigItem> SubFeatures;

        /// <summary>
        /// 根据面向用户的会话选项解析实际运行时策略。
        /// </summary>
        public SessionRuntimePolicy ResolveRuntimePolicy()
        {
            return SessionRuntimePolicy.FromConfig(in this);
        }

        // ============== 工厂方法 ==============

        /// <summary>
        /// 默认配置，Tick 率为 30 FPS。
        /// </summary>
        public static SessionConfig Default => new SessionConfig
        {
            SessionId = SessionId.New(),
            WorldType = "battle",
            TickRate = 30,
            SyncMode = SyncMode.Lockstep,
            HostMode = HostMode.Local,
            UseCoordinatorSpawnService = true,
            EnableReplayRecording = false,
            EnableReplayPlayback = false,
            EnableClientPrediction = false,
            MaxPredictionAheadFrames = 3,
            ServerEndpoint = NetworkEndpoint.None,
            RoomId = 0
        };

        /// <summary>
        /// 创建本地单人配置。
        /// </summary>
        public static SessionConfig CreateLocal(int playerId, int mapId = 1, int tickRate = 30)
        {
            return new SessionConfig
            {
                SessionId = SessionId.New(),
                MapId = mapId,
                WorldId = 1,
                LocalPlayerId = playerId,
                ClientId = playerId,
                SyncMode = SyncMode.Lockstep,
                HostMode = HostMode.Local,
                TickRate = tickRate,
                UseCoordinatorSpawnService = true,
                EnableReplayRecording = false,
                EnableReplayPlayback = false,
                EnableClientPrediction = false,
                MaxPredictionAheadFrames = 3,
                ServerEndpoint = NetworkEndpoint.None,
                RoomId = 0
            };
        }

        /// <summary>
        /// 创建客户端状态同步配置。
        /// </summary>
        public static SessionConfig CreateStateSyncClient(int playerId, string serverHost, int serverPort, long roomId = 0)
        {
            return new SessionConfig
            {
                SessionId = SessionId.New(),
                WorldId = 1,
                LocalPlayerId = playerId,
                ClientId = playerId,
                SyncMode = SyncMode.StateSync,
                HostMode = HostMode.Client,
                TickRate = 30,
                UseCoordinatorSpawnService = true,
                EnableReplayRecording = true,
                EnableReplayPlayback = false,
                EnableClientPrediction = false,
                MaxPredictionAheadFrames = 0,
                ServerEndpoint = new NetworkEndpoint(serverHost, serverPort),
                RoomId = roomId
            };
        }

        /// <summary>
        /// 创建混合多人配置（客户端预测）。
        /// </summary>
        public static SessionConfig CreateHybrid(int playerId, string serverHost, int serverPort, long roomId = 0)
        {
            return new SessionConfig
            {
                SessionId = SessionId.New(),
                WorldId = 1,
                LocalPlayerId = playerId,
                ClientId = playerId,
                SyncMode = SyncMode.Hybrid,
                HostMode = HostMode.Client,
                TickRate = 30,
                UseCoordinatorSpawnService = true,
                EnableReplayRecording = true,
                EnableReplayPlayback = false,
                EnableClientPrediction = true,
                MaxPredictionAheadFrames = 3,
                ServerEndpoint = new NetworkEndpoint(serverHost, serverPort),
                RoomId = roomId
            };
        }

        /// <summary>
        /// 创建主机配置（用于局域网多人）。
        /// </summary>
        public static SessionConfig CreateHost(int playerId, int mapId = 1, int tickRate = 30)
        {
            return new SessionConfig
            {
                SessionId = SessionId.New(),
                MapId = mapId,
                WorldId = 1,
                LocalPlayerId = playerId,
                ClientId = playerId,
                SyncMode = SyncMode.Lockstep,
                HostMode = HostMode.Host,
                TickRate = tickRate,
                UseCoordinatorSpawnService = true,
                EnableReplayRecording = true,
                EnableReplayPlayback = false,
                EnableClientPrediction = false,
                MaxPredictionAheadFrames = 0,
                ServerEndpoint = NetworkEndpoint.None,
                RoomId = SessionId.New().Value
            };
        }
    }

    /// <summary>
    /// 从 SessionConfig 派生出的实际运行时策略。
    /// </summary>
    public readonly struct SessionRuntimePolicy
    {
        public readonly SyncMode RequestedSyncMode;
        public readonly SyncMode EffectiveSyncMode;
        public readonly HostMode HostMode;
        public readonly bool RequiresNetwork;
        public readonly bool SupportsPrediction;
        public readonly bool EnableClientPrediction;
        public readonly int MaxPredictionAheadFrames;
        public readonly bool RequireLogicWorldDriveGate;
        public readonly bool UseCoordinatorSpawnService;

        public SessionRuntimePolicy(
            SyncMode requestedSyncMode,
            SyncMode effectiveSyncMode,
            HostMode hostMode,
            bool requiresNetwork,
            bool supportsPrediction,
            bool enableClientPrediction,
            int maxPredictionAheadFrames,
            bool requireLogicWorldDriveGate,
            bool useCoordinatorSpawnService)
        {
            RequestedSyncMode = requestedSyncMode;
            EffectiveSyncMode = effectiveSyncMode;
            HostMode = hostMode;
            RequiresNetwork = requiresNetwork;
            SupportsPrediction = supportsPrediction;
            EnableClientPrediction = enableClientPrediction;
            MaxPredictionAheadFrames = maxPredictionAheadFrames;
            RequireLogicWorldDriveGate = requireLogicWorldDriveGate;
            UseCoordinatorSpawnService = useCoordinatorSpawnService;
        }

        public static SessionRuntimePolicy FromConfig(in SessionConfig config)
        {
            var effectiveSyncMode = config.HostMode == HostMode.Local
                ? SyncMode.Lockstep
                : config.SyncMode;
            var supportsPrediction = effectiveSyncMode == SyncMode.Hybrid;
            var enablePrediction = supportsPrediction && config.EnableClientPrediction;

            return new SessionRuntimePolicy(
                requestedSyncMode: config.SyncMode,
                effectiveSyncMode: effectiveSyncMode,
                hostMode: config.HostMode,
                // Lockstep is a synchronization algorithm, not a transport decision.
                // A Host/Client session still needs networking even when it uses lockstep.
                requiresNetwork: config.HostMode != HostMode.Local,
                supportsPrediction: supportsPrediction,
                enableClientPrediction: enablePrediction,
                maxPredictionAheadFrames: enablePrediction ? Math.Max(0, config.MaxPredictionAheadFrames) : 0,
                requireLogicWorldDriveGate: config.RequireLogicWorldDriveGate,
                useCoordinatorSpawnService: config.UseCoordinatorSpawnService);
        }
    }

    /// <summary>
    /// 子功能配置项。
    /// </summary>
    public struct SubFeatureConfigItem
    {
        /// <summary>
        /// 子功能类型名称。
        /// </summary>
        public string TypeName;

        /// <summary>
        /// 是否启用。
        /// </summary>
        public bool Enabled;

        /// <summary>
        /// 配置数据（JSON 序列化）。
        /// </summary>
        public string ConfigJson;

        public SubFeatureConfigItem(string typeName, bool enabled = true, string configJson = null)
        {
            TypeName = typeName;
            Enabled = enabled;
            ConfigJson = configJson;
        }
    }
}
