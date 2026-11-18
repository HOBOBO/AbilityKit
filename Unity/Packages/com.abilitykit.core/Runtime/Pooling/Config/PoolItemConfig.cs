namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 单个对象池的声明式设置，配置提供者可以从包级配置中返回该结构。
    /// </summary>
    public readonly struct PoolItemConfig
    {
        private readonly bool _specified;

        /// <summary>
        /// 获取配置是否允许通过配置链取得对象池；不自动关闭已创建的池。
        /// </summary>
        public readonly bool Enabled;
        /// <summary>
        /// 获取构造预热容量，与 PrewarmCount 的较大值共同决定实际选项的默认容量。
        /// </summary>
        public readonly int DefaultCapacity;
        /// <summary>
        /// 获取最大空闲保留数量，不限制同时借出的对象数量；合法性在池构造时检查。
        /// </summary>
        public readonly int MaxSize;
        /// <summary>
        /// 获取预热数量；构造参数为负数时使用默认容量，实际预热仍受最大空闲容量限制。
        /// </summary>
        public readonly int PrewarmCount;
        /// <summary>
        /// 获取是否按引用身份检查重复或转换期间的归还。
        /// </summary>
        public readonly bool CollectionCheck;
        /// <summary>
        /// 获取常规裁剪策略；禁止常规裁剪时保存 KeepAll 兼容值。
        /// </summary>
        public readonly PoolTrimPolicy TrimPolicy;
        /// <summary>
        /// 获取是否禁止常规裁剪，不影响强制裁剪和清空。
        /// </summary>
        public readonly bool NeverTrim;

        /// <summary>
        /// 创建已指定的声明式配置，规范化预热数量及裁剪标志；不在此处校验容量。
        /// </summary>
        public PoolItemConfig(
            bool enabled = true,
            int defaultCapacity = 0,
            int maxSize = 1024,
            int prewarmCount = -1,
            bool collectionCheck = true,
            PoolTrimPolicy trimPolicy = default,
            bool neverTrim = false)
        {
            _specified = true;
            Enabled = enabled;
            DefaultCapacity = defaultCapacity;
            MaxSize = maxSize;
            PrewarmCount = prewarmCount < 0 ? defaultCapacity : prewarmCount;
            CollectionCheck = collectionCheck;
            TrimPolicy = neverTrim ? PoolTrimPolicy.KeepAll : trimPolicy;
            NeverTrim = neverTrim;
        }

        /// <summary>
        /// 获取配置是否由参数化构造创建；默认结构值表示未指定，不能根据 Enabled 代替此判定。
        /// </summary>
        public bool IsSpecified => _specified;

        /// <summary>
        /// 获取未指定的默认结构值，配置中心不会将其作为命中项。
        /// </summary>
        public static PoolItemConfig Unspecified => default;

        /// <summary>
        /// 获取已指定但禁用的配置，可覆盖较低优先级配置。
        /// </summary>
        public static PoolItemConfig Disabled => new PoolItemConfig(enabled: false);

        /// <summary>
        /// 按给定容量、校验和裁剪参数创建已指定且启用的配置。
        /// </summary>
        public static PoolItemConfig Default(
            int defaultCapacity = 0,
            int maxSize = 1024,
            int prewarmCount = -1,
            bool collectionCheck = true,
            PoolTrimPolicy trimPolicy = default,
            bool neverTrim = false)
        {
            return new PoolItemConfig(true, defaultCapacity, maxSize, prewarmCount, collectionCheck, trimPolicy, neverTrim);
        }
    }
}
