namespace AbilityKit.Modifiers
{
    /// <summary>
    /// 堆叠行为（对标 GAS 的 StackPolicy）
    /// </summary>
    public enum StackBehavior : byte
    {
        /// <summary>无堆叠（每次应用都是独立的）</summary>
        None = 0,

        /// <summary>累加：多个同源效果数值相加</summary>
        Aggregate = 1,

        /// <summary>覆盖：后到的替换先到的</summary>
        Override = 2,

        /// <summary>独立：每个效果都是独立的实例</summary>
        Independent = 3,

        /// <summary>独立覆盖：同标签的效果合并为单个</summary>
        UniqueOverride = 4
    }

    /// <summary>
    /// 堆叠刷新策略
    /// </summary>
    public enum StackRefreshPolicy : byte
    {
        /// <summary>刷新持续时间</summary>
        Refresh = 0,

        /// <summary>累加持续时间</summary>
        AdditiveDuration = 1,

        /// <summary>不刷新持续时间</summary>
        NoRefresh = 2
    }
}
