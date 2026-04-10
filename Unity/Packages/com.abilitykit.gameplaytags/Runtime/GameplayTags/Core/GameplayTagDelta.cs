namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 标签变化量，记录标签的添加和移除。
    /// </summary>
    public readonly struct GameplayTagDelta
    {
        /// <summary>
        /// 新增的标签
        /// </summary>
        public readonly GameplayTagContainer Added;

        /// <summary>
        /// 移除的标签
        /// </summary>
        public readonly GameplayTagContainer Removed;

        public GameplayTagDelta(GameplayTagContainer added, GameplayTagContainer removed)
        {
            Added = added;
            Removed = removed;
        }

        /// <summary>
        /// 是否为空（无变化）
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                var addedEmpty = Added == null || Added.Count == 0;
                var removedEmpty = Removed == null || Removed.Count == 0;
                return addedEmpty && removedEmpty;
            }
        }

        /// <summary>
        /// 创建空的变化量
        /// </summary>
        public static GameplayTagDelta Empty => new GameplayTagDelta(null, null);

        /// <summary>
        /// 合并两个变化量
        /// </summary>
        public static GameplayTagDelta operator +(GameplayTagDelta a, GameplayTagDelta b)
        {
            var added = new GameplayTagContainer();
            var removed = new GameplayTagContainer();

            if (a.Added != null) added.AppendTags(a.Added);
            if (b.Added != null) added.AppendTags(b.Added);

            if (a.Removed != null) removed.AppendTags(a.Removed);
            if (b.Removed != null) removed.AppendTags(b.Removed);

            return new GameplayTagDelta(added, removed);
        }
    }
}
