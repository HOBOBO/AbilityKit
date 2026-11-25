namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 描述持续行为的激活、授予、移除和持续生效条件。
    /// 本类型只保存配置并判断条件；授予和撤销标签由消费者执行。
    /// </summary>
    public sealed class ContinuousTagRequirements
    {
        /// <summary>
        /// 必须满足的激活条件。
        /// </summary>
        public GameplayTagRequirements ActivationRequired { get; set; }

        /// <summary>
        /// 供生命周期消费者在激活时授予的标签。
        /// </summary>
        public GameplayTagContainer ApplicationTags { get; set; } = new();

        /// <summary>
        /// 有配置且条件成立时触发移除；未配置时不触发。
        /// 如果只配置禁止标签，则这些标签均不存在时条件成立。
        /// </summary>
        public GameplayTagRequirements RemovalRequired { get; set; }

        /// <summary>
        /// 持续生效期间必须满足的条件。
        /// </summary>
        public GameplayTagRequirements OngoingRequired { get; set; }

        /// <summary>
        /// 持续对象执行时需要避开的已编译结果位，由消费者检查。
        /// </summary>
        public ulong ExecutionBlockedResultMask { get; set; }

        /// <summary>
        /// 创建无激活限制、无移除条件、无持续限制的配置。
        /// </summary>
        public ContinuousTagRequirements()
        {
            ActivationRequired = new GameplayTagRequirements();
            RemovalRequired = new GameplayTagRequirements();
            OngoingRequired = new GameplayTagRequirements();
        }

        /// <summary>
        /// 检查激活条件；未提供实体标签容器时不限制激活。
        /// </summary>
        public bool CanActivate(GameplayTagContainer entityTags)
        {
            if (entityTags == null) return true;
            return ActivationRequired.IsSatisfiedBy(entityTags);
        }

        /// <summary>
        /// 检查是否配置并满足移除条件；未提供实体标签容器时不触发移除。
        /// 空容器是有效输入，与未提供容器不同。
        /// </summary>
        public bool ShouldRemove(GameplayTagContainer entityTags)
        {
            if (entityTags == null) return false;
            var condition = RemovalRequired;
            var hasCondition = (condition.Required != null && !condition.Required.IsEmpty) ||
                               (condition.Blocked != null && !condition.Blocked.IsEmpty) ||
                               condition.Query.IsValid;
            return hasCondition && condition.IsSatisfiedBy(entityTags);
        }

        /// <summary>
        /// 检查持续条件；未提供实体标签容器时不限制持续生效。
        /// </summary>
        public bool IsOngoingSatisfied(GameplayTagContainer entityTags)
        {
            if (entityTags == null) return true;
            return OngoingRequired.IsSatisfiedBy(entityTags);
        }
    }
}
