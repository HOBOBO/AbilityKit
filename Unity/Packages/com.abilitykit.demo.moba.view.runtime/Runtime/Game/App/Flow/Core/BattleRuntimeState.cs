#nullable enable
namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 战斗会话的纯运行态标志（per-battle-scope）。
    ///
    /// 迁移背景（见 MobaFlowSpec.md Step 2 后续刀）：
    /// 这两个标志原本是 <c>GameFlowDomain</c> 上的 <c>_battleSessionStarted</c> /
    /// <c>_battleFirstFrameReceived</c> 字段，靠 <c>ResetBattleSessionRuntimeState()</c> 每局手工清零。
    /// 现改为 <see cref="WorldLifetime.Scoped"/> 服务：每场战斗的 scope 新建一个实例（默认 false），
    /// 退出战斗 <c>scope.Dispose()</c> 时随之释放，"每局重置"由 scope 生命周期天然保证。
    ///
    /// 这是"真实运行态从字段迁到 scoped 服务"的首个闭环：纯 C#、零跨阶段输入、可完整镜像进桌面 xUnit。
    /// </summary>
    public interface IBattleRuntimeState
    {
        /// <summary>会话是否已开始（对应原 <c>_battleSessionStarted</c>）。</summary>
        bool SessionStarted { get; set; }

        /// <summary>同步模式所需的战斗世界资源是否已建立。</summary>
        bool WorldReady { get; set; }

        /// <summary>是否已收到首帧（对应原 <c>_battleFirstFrameReceived</c>）。</summary>
        bool FirstFrameReceived { get; set; }

        bool AssetsLoadCompleted { get; set; }

        /// <summary>把运行态清零。新 scope 实例本就为 false，此方法用于 Prepare-enter 的显式重置语义。</summary>
        void Reset();
    }

    internal sealed class BattleRuntimeState : IBattleRuntimeState
    {
        public bool SessionStarted { get; set; }

        public bool WorldReady { get; set; }

        public bool FirstFrameReceived { get; set; }

        public bool AssetsLoadCompleted { get; set; }

        public void Reset()
        {
            SessionStarted = false;
            WorldReady = false;
            FirstFrameReceived = false;
            AssetsLoadCompleted = false;
        }
    }
}
