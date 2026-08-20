using System;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 用于创建和组合对象池选项的辅助入口，避免调用点重复传入过长的参数列表。
    /// </summary>
    public static class PoolOptions
    {
        /// <summary>Creates object-pool options from explicit capacity and trim settings.</summary>
        /// <typeparam name="T">The reference type stored by the pool.</typeparam>
        /// <param name="createFunc">A factory that must return a non-null element.</param>
        /// <param name="defaultCapacity">The initial prewarm count.</param>
        /// <param name="maxSize">The maximum number of inactive elements retained.</param>
        /// <param name="collectionCheck">Whether duplicate releases are detected.</param>
        /// <param name="trimPolicy">The default trim policy.</param>
        /// <param name="neverTrim">Whether regular trim operations are disabled.</param>
        /// <returns>A new mutable options instance.</returns>
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

        /// <summary>Creates object-pool options from a resolved configuration value.</summary>
        /// <typeparam name="T">The reference type stored by the pool.</typeparam>
        /// <param name="createFunc">A factory that must return a non-null element.</param>
        /// <param name="config">The resolved configuration, or an unspecified value to use defaults.</param>
        /// <returns>A new mutable options instance.</returns>
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

        /// <summary>Replaces the optional lifecycle callbacks on an options instance.</summary>
        /// <typeparam name="T">The reference type stored by the pool.</typeparam>
        /// <param name="options">The options to mutate.</param>
        /// <param name="onGet">The optional acquisition callback.</param>
        /// <param name="onRelease">The optional return callback.</param>
        /// <param name="onDestroy">The optional permanent-removal callback.</param>
        /// <returns><paramref name="options"/>.</returns>
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

        /// <summary>Replaces the initial and retained capacities on an options instance.</summary>
        /// <typeparam name="T">The reference type stored by the pool.</typeparam>
        /// <param name="options">The options to mutate.</param>
        /// <param name="defaultCapacity">The initial prewarm count.</param>
        /// <param name="maxSize">The maximum number of inactive elements retained.</param>
        /// <returns><paramref name="options"/>.</returns>
        public static ObjectPoolOptions<T> WithCapacity<T>(this ObjectPoolOptions<T> options, int defaultCapacity, int maxSize) where T : class
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.DefaultCapacity = defaultCapacity;
            options.MaxSize = maxSize;
            return options;
        }

        /// <summary>Replaces the trimming behavior on an options instance.</summary>
        /// <typeparam name="T">The reference type stored by the pool.</typeparam>
        /// <param name="options">The options to mutate.</param>
        /// <param name="trimPolicy">The default trim policy.</param>
        /// <param name="neverTrim">Whether regular trim operations are disabled.</param>
        /// <returns><paramref name="options"/>.</returns>
        public static ObjectPoolOptions<T> WithTrim<T>(this ObjectPoolOptions<T> options, PoolTrimPolicy trimPolicy, bool neverTrim = false) where T : class
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.TrimPolicy = neverTrim ? PoolTrimPolicy.KeepAll : trimPolicy;
            options.NeverTrim = neverTrim;
            return options;
        }
    }
}
