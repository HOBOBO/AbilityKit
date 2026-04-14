using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 管线生命周期所有者接口
    /// 由使用 Pipeline 的类实现（如 Ability 系统）
    /// </summary>
    public interface IPipelineLifeOwner
    {
        /// <summary>
        /// 唯一标识符
        /// </summary>
        int OwnerId { get; }

        /// <summary>
        /// 显示名称
        /// </summary>
        string OwnerName { get; }

        /// <summary>
        /// 当前状态
        /// </summary>
        EAbilityPipelineState State { get; }

        /// <summary>
        /// 当前阶段ID
        /// </summary>
        AbilityPipelinePhaseId CurrentPhaseId { get; }

        /// <summary>
        /// 是否暂停
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// 获取当前阶段
        /// </summary>
        IReadOnlyList<AbilityPipelinePhaseId> ActivePhases { get; }
    }
}
