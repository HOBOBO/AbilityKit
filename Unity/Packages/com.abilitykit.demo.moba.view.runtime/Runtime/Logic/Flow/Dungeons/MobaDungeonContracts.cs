using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow.Dungeons
{
    /// <summary>
    /// 副本玩法处理器。副本之间的玩法差异（波次布置、队伍分配、推进节奏）都收敛在这里，
    /// 不再泄漏到 Flow 和 Session。新增玩法只需实现本接口并在副本注册中心登记。
    /// </summary>
    public interface IDungeonGameplayHandler
    {
        /// <summary>扩展点 id，必须与副本配置里的 <c>GameplayHandlerId</c> 一致。</summary>
        string Id { get; }

        /// <summary>
        /// 对局开始时布置玩法。失败应抛出异常，由入场前 dry-run 校验捕获。
        /// </summary>
        void Initialize(in DungeonGameplayContext context);

        /// <summary>每逻辑帧推进玩法。默认不推进。</summary>
        void Tick(in DungeonGameplayContext context) { }

        /// <summary>本玩法是否声明依赖某 Feature 端口组。用于端口分组校验。</summary>
        bool RequiresPortGroup(string portGroup) => false;
    }

    /// <summary>
    /// 副本场景装配契约。输入副本定义，输出装配句柄；本地局、Gateway 远程局与回放
    /// 共用同一装配路径，切换副本只需要换副本定义。
    /// </summary>
    public interface IBattleSceneProvider
    {
        string Id { get; }

        /// <summary>装配副本场景。实现必须可重复调用与幂等释放。</summary>
        IBattleSceneHandle Assemble(in DungeonSceneContext context);
    }

    /// <summary>副本场景装配句柄。</summary>
    public interface IBattleSceneHandle : IDisposable
    {
        bool IsLoaded { get; }
        /// <summary>
        /// 装配上的逻辑地图 id（对应 battle_maps）。地图是玩法资源，由场景装配器
        /// 按 <c>GameplayId</c> 自行解析，副本表不复制这个引用。无逻辑地图时为 0。
        /// </summary>
        int BattleMapId { get; }
    }

    /// <summary>
    /// 胜负结算规则。把玩法相关的胜负判定从整数枚举与固定 trigger 中解耦出来。
    /// </summary>
    public interface IBattleOutcomeRule
    {
        string Id { get; }

        /// <summary>该规则适用的玩法 id；0 表示适用所有玩法。</summary>
        int GameplayId { get; }

        /// <summary>
        /// 评估当前对局是否已分出胜负，并给出胜方队伍 id（0 表示平局/未结束）。
        /// </summary>
        bool TryEvaluate(in BattleOutcomeEvaluationContext context, out int winningTeamId);
    }

    /// <summary>玩法处理器每帧收到的上下文。</summary>
    public readonly struct DungeonGameplayContext
    {
        public DungeonGameplayContext(MobaDungeonDef dungeon, int frame, int localPlayerCount)
        {
            Dungeon = dungeon;
            Frame = frame;
            LocalPlayerCount = localPlayerCount;
        }

        public MobaDungeonDef Dungeon { get; }
        /// <summary>当前逻辑帧号。</summary>
        public int Frame { get; }
        public int LocalPlayerCount { get; }
    }

    /// <summary>场景装配收到的上下文。</summary>
    public readonly struct DungeonSceneContext
    {
        public DungeonSceneContext(MobaDungeonDef dungeon, int playerCount, int battleMapId = 0)
        {
            Dungeon = dungeon;
            PlayerCount = playerCount;
            BattleMapId = battleMapId;
        }

        public MobaDungeonDef Dungeon { get; }
        public int PlayerCount { get; }
        /// <summary>
        /// 调用方解析好的逻辑地图 id；0 表示未指定，由装配器按玩法自行决定。
        /// </summary>
        public int BattleMapId { get; }
    }

    /// <summary>胜负评估上下文。</summary>
    public readonly struct BattleOutcomeEvaluationContext
    {
        public BattleOutcomeEvaluationContext(MobaDungeonDef dungeon, int gameplayId, int frame,
            int elapsedMs, int localTeamId, double team1Score = 0d, double team2Score = 0d)
        {
            Dungeon = dungeon;
            GameplayId = gameplayId;
            Frame = frame;
            ElapsedMs = elapsedMs;
            LocalTeamId = localTeamId;
            Team1Score = team1Score;
            Team2Score = team2Score;
        }

        public MobaDungeonDef Dungeon { get; }
        public int GameplayId { get; }
        public int Frame { get; }
        public int ElapsedMs { get; }
        public int LocalTeamId { get; }
        public double Team1Score { get; }
        public double Team2Score { get; }

        /// <summary>本局是否已经超过副本配置的时长上限；0 表示不限制。</summary>
        public bool HasTimedOut =>
            Dungeon != null && Dungeon.DurationMs > 0 && ElapsedMs >= Dungeon.DurationMs;
    }
}
