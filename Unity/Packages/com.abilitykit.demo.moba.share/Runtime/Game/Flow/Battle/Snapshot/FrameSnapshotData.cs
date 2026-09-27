using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// 快照帧数据
    /// 包含帧同步所需的所有数据
    /// </summary>
    public readonly struct FrameSnapshotData
    {
        /// <summary>
        /// 帧索引
        /// </summary>
        public int FrameIndex { get; }

        /// <summary>
        /// 快照时间戳
        /// </summary>
        public double Timestamp { get; }

        /// <summary>
        /// 快照类型
        /// </summary>
        public SnapshotType Type { get; }

        /// <summary>
        /// 进入游戏数据
        /// </summary>
        public EnterGameData EnterGame { get; }

        /// <summary>
        /// 角色变换数据列表
        /// </summary>
        public IReadOnlyList<ActorTransformData> ActorTransforms { get; }

        /// <summary>
        /// 弹道事件数据列表
        /// </summary>
        public IReadOnlyList<ProjectileEventData> ProjectileEvents { get; }

        /// <summary>
        /// 区域事件数据列表
        /// </summary>
        public IReadOnlyList<AreaEventData> AreaEvents { get; }

        /// <summary>
        /// 伤害事件数据列表
        /// </summary>
        public IReadOnlyList<DamageEventData> DamageEvents { get; }

        /// <summary>
        /// 状态哈希数据
        /// </summary>
        public StateHashData StateHash { get; }

        /// <summary>
        /// 角色生成数据列表
        /// </summary>
        public IReadOnlyList<ActorSpawnData> ActorSpawns { get; }

        /// <summary>
        /// Platform-neutral actor removal data.
        /// </summary>
        public IReadOnlyList<ActorDespawnData> ActorDespawns { get; }

        /// <summary>
        /// Platform-neutral skill presentation states.
        /// </summary>
        public IReadOnlyList<SkillStateData> SkillStates { get; }

        /// <summary>
        /// 表现 Cue 数据列表
        /// </summary>
        public IReadOnlyList<PresentationCueData> PresentationCues { get; }

        public FrameSnapshotData(
            int frameIndex,
            double timestamp,
            SnapshotType type,
            EnterGameData enterGame = default,
            IReadOnlyList<ActorTransformData> actorTransforms = null,
            IReadOnlyList<ProjectileEventData> projectileEvents = null,
            IReadOnlyList<AreaEventData> areaEvents = null,
            IReadOnlyList<DamageEventData> damageEvents = null,
            StateHashData stateHash = default,
            IReadOnlyList<ActorSpawnData> actorSpawns = null,
            IReadOnlyList<PresentationCueData> presentationCues = null,
            IReadOnlyList<ActorDespawnData> actorDespawns = null,
            IReadOnlyList<SkillStateData> skillStates = null)
        {
            FrameIndex = frameIndex;
            Timestamp = timestamp;
            Type = type;
            EnterGame = enterGame;
            ActorTransforms = actorTransforms ?? Array.Empty<ActorTransformData>();
            ProjectileEvents = projectileEvents ?? Array.Empty<ProjectileEventData>();
            AreaEvents = areaEvents ?? Array.Empty<AreaEventData>();
            DamageEvents = damageEvents ?? Array.Empty<DamageEventData>();
            StateHash = stateHash;
            ActorSpawns = actorSpawns ?? Array.Empty<ActorSpawnData>();
            ActorDespawns = actorDespawns ?? Array.Empty<ActorDespawnData>();
            SkillStates = skillStates ?? Array.Empty<SkillStateData>();
            PresentationCues = presentationCues ?? Array.Empty<PresentationCueData>();
        }
    }

    /// <summary>
    /// 快照类型
    /// </summary>
    public enum SnapshotType
    {
        /// <summary>
        /// 全量快照
        /// </summary>
        Full = 0,

        /// <summary>
        /// 增量快照
        /// </summary>
        Delta = 1,

        /// <summary>
        /// 关键帧快照
        /// </summary>
        KeyFrame = 2,
    }

    /// <summary>
    /// 进入游戏数据
    /// </summary>
    public readonly struct EnterGameData
    {
        public bool HasValue { get; }
        public int MapId { get; }
        public int LocalPlayerId { get; }
        public IReadOnlyList<int> PlayerIds { get; }
        public IReadOnlyList<TeamData> Teams { get; }

        public EnterGameData(int mapId, int localPlayerId, IReadOnlyList<int> playerIds, IReadOnlyList<TeamData> teams)
        {
            MapId = mapId;
            LocalPlayerId = localPlayerId;
            PlayerIds = playerIds;
            Teams = teams;
            HasValue = true;
        }

        public static readonly EnterGameData Default = default;
    }

    /// <summary>
    /// 队伍数据
    /// </summary>
    public readonly struct TeamData
    {
        public int TeamId { get; }
        public IReadOnlyList<int> PlayerIds { get; }

        public TeamData(int teamId, IReadOnlyList<int> playerIds)
        {
            TeamId = teamId;
            PlayerIds = playerIds;
        }
    }

}
