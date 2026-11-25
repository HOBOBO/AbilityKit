namespace AbilityKit.Network.Runtime
{
    /// <summary>
    /// 客户端同步控制器的公共契约。控制器负责一种 <see cref="NetworkSyncModel"/> 的运行时行为，如预测回滚、权威插值或批量状态同步。此抽象与玩法无关，让各示例通过统一共享接口描述活动同步策略。
    /// </summary>
    public interface INetworkSyncController
    {
        /// <summary>
        /// 此控制器实现的同步策略。
        /// </summary>
        NetworkSyncModel SyncModel { get; }

        /// <summary>
        /// 控制器是否正在运行活动同步会话。
        /// </summary>
        bool IsStarted { get; }

        /// <summary>
        /// 控制器当前已推进到的本地模拟或播放帧。
        /// </summary>
        int CurrentFrame { get; }
    }
}
