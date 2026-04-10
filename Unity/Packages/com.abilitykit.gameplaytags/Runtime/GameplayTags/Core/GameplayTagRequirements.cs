namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 标签需求条件，用于检查标签是否满足前置要求。
    /// 对标 Unreal Engine 的 FGameplayTagRequirements。
    /// </summary>
    public readonly struct GameplayTagRequirements
    {
        /// <summary>
        /// 必须包含的标签
        /// </summary>
        public readonly GameplayTagContainer Required;

        /// <summary>
        /// 不能包含的标签
        /// </summary>
        public readonly GameplayTagContainer Blocked;

        /// <summary>
        /// 是否精确匹配（否则支持父子层级匹配）
        /// </summary>
        public readonly bool Exact;

        public GameplayTagRequirements(GameplayTagContainer required, GameplayTagContainer blocked, bool exact = false)
        {
            Required = required;
            Blocked = blocked;
            Exact = exact;
        }

        /// <summary>
        /// 创建只有必须标签的需求
        /// </summary>
        public static GameplayTagRequirements Require(params GameplayTag[] tags)
        {
            var container = new GameplayTagContainer();
            foreach (var tag in tags)
            {
                container.Add(tag);
            }
            return new GameplayTagRequirements(container, null, false);
        }

        /// <summary>
        /// 创建只有禁止标签的需求
        /// </summary>
        public static GameplayTagRequirements Block(params GameplayTag[] tags)
        {
            var container = new GameplayTagContainer();
            foreach (var tag in tags)
            {
                container.Add(tag);
            }
            return new GameplayTagRequirements(null, container, false);
        }

        /// <summary>
        /// 检查给定标签是否满足需求
        /// </summary>
        public bool IsSatisfiedBy(GameplayTagContainer tags)
        {
            if (tags == null) return false;

            if (Blocked != null && Blocked.Count > 0)
            {
                if (tags.HasAny(Blocked, Exact)) return false;
            }

            if (Required != null && Required.Count > 0)
            {
                if (!tags.HasAll(Required, Exact)) return false;
            }

            return true;
        }

        /// <summary>
        /// 检查单个标签是否满足需求
        /// </summary>
        public bool IsSatisfiedBy(GameplayTag tag)
        {
            if (Blocked != null && Blocked.Count > 0)
            {
                if (Blocked.HasTag(tag)) return false;
            }

            if (Required != null && Required.Count > 0)
            {
                if (!Required.HasTag(tag)) return false;
            }

            return true;
        }
    }
}
