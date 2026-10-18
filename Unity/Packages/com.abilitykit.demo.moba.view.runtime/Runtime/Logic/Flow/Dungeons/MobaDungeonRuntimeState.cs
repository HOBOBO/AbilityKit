using System;

namespace AbilityKit.Game.Flow.Dungeons
{
    /// <summary>
    /// 本局副本运行态。作为每局战斗作用域（per-battle scope）服务存在，随 <c>scope.Dispose()</c> 释放，
    /// 因此新的一局天然拿到干净的实例，不需要手工清零。
    /// </summary>
    public sealed class MobaDungeonRuntimeState : IDisposable
    {
        /// <summary>本局玩家数，供场景装配与玩法处理器使用。</summary>
        public int PlayerCount { get; set; }

        /// <summary>
        /// 本局副本随机源。副本内的随机决策必须走它，决策才会被记录并可被回放复现。
        /// 种子来自副本定义；未设置时按 0 处理，保证同一副本每局使用固定种子。
        /// </summary>
        public MobaDungeonRandomSource Random { get; set; }

        /// <summary>已装配的副本场景句柄；进副本时装、退出时释放。</summary>
        public IBattleSceneHandle SceneHandle { get; set; }

        public void Dispose()
        {
            SceneHandle?.Dispose();
            SceneHandle = null;
            PlayerCount = 0;
            Random = null;
        }
    }
}
