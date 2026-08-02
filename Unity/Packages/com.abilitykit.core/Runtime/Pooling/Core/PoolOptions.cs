using System;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 用于创建和组合对象池选项的辅助入口，避免调用点重复传入过长的参数列表。
    /// </summary>
    public static class PoolOptions
    {
        public static ObjectPoolOptions<T> For<T>(
            Func<T> createFunc,
            int defaultCapacity = 0,
            int maxSize = 1024,
            bool collectionCheck = true,
            PoolTrimPolicy trimPolicy = default,
            bool neverTrim = false) where T : class
        {
            return new ObjectPoolOptions<T>(createFunc)
            {
                DefaultCapacity = defaultCapacity,
                MaxSize = maxSize,
                CollectionCheck = collectionCheck,
                TrimPolicy = neverTrim ? PoolTrimPolicy.KeepAll : trimPolicy,
                NeverTrim = neverTrim,
            };
        }

        public static ObjectPoolOptions<T> FromConfig<T>(Func<T> createFunc, PoolItemConfig config) where T : class
        {
            if (!config.IsSpecified)
            {
                return For(createFunc);
            }

            return new ObjectPoolOptions<T>(createFunc)
            {
                DefaultCapacity = Math.Max(config.DefaultCapacity, config.PrewarmCount),
                MaxSize = config.MaxSize,
                CollectionCheck = config.CollectionCheck,
                TrimPolicy = config.NeverTrim ? PoolTrimPolicy.KeepAll : config.TrimPolicy,
                NeverTrim = config.NeverTrim,
            };
        }

        public static ObjectPoolOptions<T> WithLifecycle<T>(
            this ObjectPoolOptions<T> options,
            Action<T>? onGet = null,
            Action<T>? onRelease = null,
            Action<T>? onDestroy = null) where T : class
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.OnGet = onGet;
            options.OnRelease = onRelease;
            options.OnDestroy = onDestroy;
            return options;
        }

        public static ObjectPoolOptions<T> WithCapacity<T>(this ObjectPoolOptions<T> options, int defaultCapacity, int maxSize) where T : class
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.DefaultCapacity = defaultCapacity;
            options.MaxSize = maxSize;
            return options;
        }

        public static ObjectPoolOptions<T> WithTrim<T>(this ObjectPoolOptions<T> options, PoolTrimPolicy trimPolicy, bool neverTrim = false) where T : class
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.TrimPolicy = neverTrim ? PoolTrimPolicy.KeepAll : trimPolicy;
            options.NeverTrim = neverTrim;
            return options;
        }
    }
}
