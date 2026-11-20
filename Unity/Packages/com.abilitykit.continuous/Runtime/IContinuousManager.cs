using System;
using System.Collections.Generic;

namespace AbilityKit.Continuous
{
    /// <summary>
    /// 管理持续体的注册、生命周期推进及按所有者查询。
    /// 本包提供 <see cref="DefaultContinuousManager"/>；领域计时和玩法规则由使用方决定。
    /// </summary>
    public interface IContinuousManager
    {
        /// <summary>
        /// 注册持续体
        /// </summary>
        /// <returns>是否注册成功</returns>
        bool Register(IContinuous continuous);

        /// <summary>
        /// 注销持续体
        /// </summary>
        void Unregister(IContinuous continuous, ContinuousEndReason reason = ContinuousEndReason.CleanedUp);

        /// <summary>
        /// 尝试激活持续体
        /// </summary>
        bool TryActivate(IContinuous continuous);

        /// <summary>
        /// 尝试暂停持续体。
        /// </summary>
        bool TryPause(IContinuous continuous);

        /// <summary>
        /// 尝试恢复持续体。
        /// </summary>
        bool TryResume(IContinuous continuous);

        /// <summary>
        /// 尝试结束持续体。
        /// </summary>
        bool TryEnd(IContinuous continuous, ContinuousEndReason reason = ContinuousEndReason.Completed);

        /// <summary>
        /// 尝试中断持续体。
        /// </summary>
        bool TryInterrupt(IContinuous continuous, string reason);

        /// <summary>
        /// 获取实体的所有持续体
        /// </summary>
        IReadOnlyList<IContinuous> GetOwnerContinuous(long ownerId);

        /// <summary>
        /// 获取实体的活跃持续体
        /// </summary>
        IReadOnlyList<IContinuous> GetOwnerActiveContinuous(long ownerId);

        /// <summary>
        /// 中断实体的所有持续体
        /// </summary>
        void InterruptAll(long ownerId, string reason);

        /// <summary>
        /// 暂停实体的所有持续体
        /// </summary>
        void PauseAll(long ownerId);

        /// <summary>
        /// 恢复实体的所有持续体
        /// </summary>
        void ResumeAll(long ownerId);

        /// <summary>
        /// 活跃持续体数量
        /// </summary>
        int ActiveCount { get; }

        /// <summary>
        /// 总持续体数量
        /// </summary>
        int TotalCount { get; }
    }
}
