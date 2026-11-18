using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Continuous
{
    /// <summary>
    /// 旧 Core 持续体管理器契约，保留至下一主版本移除窗口。
    /// Core 保留 DefaultContinuousManager 兼容实现；新代码使用 AbilityKit.Continuous。
    /// 管理器提供登记和生命周期协调，不自动执行时长、互斥、层级或叠加策略。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface IContinuousManager
    {
        /// <summary>
        /// 请求注册持续体；默认实现按对象的默认相等比较去重，不按配置标识去重。
        /// </summary>
        bool Register(IContinuous continuous);

        /// <summary>
        /// 注销持续体并传递原因；默认实现仅移除登记，不结束对象。
        /// </summary>
        void Unregister(IContinuous continuous, ContinuousEndReason reason = ContinuousEndReason.CleanedUp);

        /// <summary>
        /// 请求激活；默认实现自动注册尚未登记的对象，再进行激活准入。
        /// </summary>
        bool TryActivate(IContinuous continuous);

        /// <summary>
        /// 请求暂停；默认实现不要求对象已经注册。
        /// </summary>
        bool TryPause(IContinuous continuous);

        /// <summary>
        /// 请求恢复；默认实现执行激活准入，但不会自动注册对象。
        /// </summary>
        bool TryResume(IContinuous continuous);

        /// <summary>
        /// 按指定原因请求结束；默认实现不要求对象已经注册，由持续体维护终止状态和事件。
        /// </summary>
        bool TryEnd(IContinuous continuous, ContinuousEndReason reason = ContinuousEndReason.Completed);

        /// <summary>
        /// 请求中断；默认实现检查配置的可中断标记，并调用持续体的 Abort。
        /// </summary>
        bool TryInterrupt(IContinuous continuous, string reason);

        /// <summary>
        /// 获取所属实体的已登记持续体；默认实现按注册顺序返回当前索引列表的只读包装。
        /// </summary>
        IReadOnlyList<IContinuous> GetOwnerContinuous(long ownerId);

        /// <summary>
        /// 获取所属实体的活跃持续体；默认实现按注册顺序，以实时状态筛选并返回副本。
        /// </summary>
        IReadOnlyList<IContinuous> GetOwnerActiveContinuous(long ownerId);

        /// <summary>
        /// 中断所属实体的持续体；默认实现先复制列表，异常会中断本轮并保留此前进度。
        /// </summary>
        void InterruptAll(long ownerId, string reason);

        /// <summary>
        /// 暂停所属实体的持续体；默认实现先复制列表，回调新增项不进入本轮。
        /// </summary>
        void PauseAll(long ownerId);

        /// <summary>
        /// 恢复所属实体的持续体；默认实现先复制列表，快照中的已注销项仍可能被操作。
        /// </summary>
        void ResumeAll(long ownerId);

        /// <summary>
        /// 获取管理器维护的活动索引数量；直接变更持续体状态不会自动同步该索引。
        /// </summary>
        int ActiveCount { get; }

        /// <summary>
        /// 获取管理器的已登记持续体数量。
        /// </summary>
        int TotalCount { get; }
    }
}
