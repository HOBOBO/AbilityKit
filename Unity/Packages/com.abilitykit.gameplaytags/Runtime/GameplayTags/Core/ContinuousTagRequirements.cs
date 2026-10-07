using System;
using System.Collections.Generic;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 持续行为标签需求，对标 GAS 的 FGameplayTagRequirements。
    /// 封装多个 GameplayTagRequirements 来管理持续行为的生命周期。
    /// 
    /// 包含：
    /// - ActivationRequired : 激活所需标签
    /// - ApplicationTags    : 应用时授予的标签
    /// - RemovalRequired    : 移除所需标签
    /// - OngoingRequired   : 持续生效所需标签
    /// </summary>
    public sealed class ContinuousTagRequirements
    {
        /// <summary>
        /// 激活需求 - 检查实体是否满足激活条件
        /// </summary>
        public GameplayTagRequirements ActivationRequired { get; set; }

        /// <summary>
        /// 应用标签 - 激活时授予给实体的标签
        /// </summary>
        public GameplayTagContainer ApplicationTags { get; set; } = new();

        /// <summary>
        /// 移除需求 - 满足条件时自动移除
        /// </summary>
        public GameplayTagRequirements RemovalRequired { get; set; }

        /// <summary>
        /// 持续需求 - 持续生效期间必须满足的条件
        /// </summary>
        public GameplayTagRequirements OngoingRequired { get; set; }

        /// <summary>持续对象执行时需要避开的已编译结果位。</summary>
        public ulong ExecutionBlockedResultMask { get; set; }

        /// <summary>
        /// 创建空的标签需求
        /// </summary>
        public ContinuousTagRequirements()
        {
            ActivationRequired = new GameplayTagRequirements();
            RemovalRequired = new GameplayTagRequirements();
            OngoingRequired = new GameplayTagRequirements();
        }

        #region 查询方法

        /// <summary>
        /// 检查是否可以激活
        /// </summary>
        public bool CanActivate(GameplayTagContainer entityTags)
        {
            if (entityTags == null) return true;
            return ActivationRequired.IsSatisfiedBy(entityTags);
        }

        /// <summary>
        /// 检查是否应该移除
        /// </summary>
        public bool ShouldRemove(GameplayTagContainer entityTags)
        {
            if (entityTags == null) return false;
            if ((RemovalRequired.Required == null || RemovalRequired.Required.IsEmpty) &&
                (RemovalRequired.Blocked == null || RemovalRequired.Blocked.IsEmpty) &&
                !RemovalRequired.Query.IsValid)
                return false;
            return RemovalRequired.IsSatisfiedBy(entityTags);
        }

        /// <summary>
        /// 检查持续需求是否满足
        /// </summary>
        public bool IsOngoingSatisfied(GameplayTagContainer entityTags)
        {
            if (entityTags == null) return true;
            
            return OngoingRequired.IsSatisfiedBy(entityTags);
        }

        #endregion
    }
}
