using System.Collections.Generic;

namespace AbilityKit.Ability.StateSync
{
    /// <summary>
    /// 在宿主指定的模拟帧管理已注册实体的本地检查点，不接收权威状态流。
    /// </summary>
    public interface IStateManager
    {
        /// <summary>
        /// 注册后续捕获和恢复所使用的实体；标识重复时保留原注册。
        /// </summary>
        void RegisterRollbackable(IRollbackable entity);

        /// <summary>
        /// 注销指定标识，不清除已保留的历史记录。
        /// </summary>
        void UnregisterRollbackable(long entityId);

        /// <summary>
        /// 捕获当前注册实体在宿主给定帧的本地恢复状态。
        /// </summary>
        void CaptureState(int frame);

        /// <summary>
        /// 尝试恢复有记录且仍已注册的实体；成功不表示完整世界恢复。
        /// </summary>
        bool TryRestore(int frame);

        /// <summary>
        /// 比较两帧的世界快照，不比较独立保存的逐实体恢复记录。
        /// </summary>
        IStateDiff ComputeDiff(int fromFrame, int toFrame);

        /// <summary>
        /// 取得世界快照自身的序列化结果；不含逐实体恢复记录，缺失时返回空值。
        /// </summary>
        byte[] GetFullState(int frame);

        /// <summary>
        /// 返回世界快照缓冲中升序保留的帧。
        /// </summary>
        IReadOnlyList<int> GetCapturedFrames();

        /// <summary>
        /// 清空检查点历史，保留实体注册。
        /// </summary>
        void ClearHistory();
    }
}
