using System;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 用于创建和组合对象池选项的辅助入口，避免调用点重复传入过长的参数列表。
    /// </summary>
    public static class PoolOptions
    {
        /// <summary>
        /// 从显式参数生成可变对象池选项；容量合法性在池构造时检查。
        /// </summary>
        /// <typeparam name="T">池中保存的引用类型。</typeparam>
        /// <param name="createFunc">必须返回新的非空对象的工厂。</param>
        /// <param name="defaultCapacity">构造预热数量，不能为负数。</param>
        /// <param name="maxSize">最大空闲保留数量，必须为正数，不限制活动对象数量。</param>
        /// <param name="collectionCheck">是否按引用身份检测重复及转换期间的归还。</param>
        /// <param name="trimPolicy">无参常规裁剪所使用的策略。</param>
        /// <param name="neverTrim">是否禁止常规裁剪，不影响强制裁剪。</param>
        /// <returns>新建的可变选项实例。</returns>
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

        /// <summary>
        /// 从已解析配置生成可变选项；未指定配置时使用默认值，预热容量取默认容量与预热数量的较大值。
        /// </summary>
        /// <typeparam name="T">池中保存的引用类型。</typeparam>
        /// <param name="createFunc">必须返回新的非空对象的工厂。</param>
        /// <param name="config">已解析配置；未指定时使用默认选项。</param>
        /// <returns>新建的可变选项实例。</returns>
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

        /// <summary>
        /// 替换可变选项实例的三个生命周期回调，传入空值会清除对应回调；不更新现有池。
        /// </summary>
        /// <typeparam name="T">池中保存的引用类型。</typeparam>
        /// <param name="options">可变创建选项，仅在池创建时读取；不得并发修改。</param>
        /// <param name="onGet">对象获取时执行的可选回调。</param>
        /// <param name="onRelease">对象归还或预热时执行的可选回调。</param>
        /// <param name="onDestroy">永久移除对象时执行的可选回调。</param>
        /// <returns>传入的同一个可变选项实例。</returns>
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

        /// <summary>
        /// 修改选项实例的构造预热数量与空闲保留上限，不更新现有池。
        /// </summary>
        /// <typeparam name="T">池中保存的引用类型。</typeparam>
        /// <param name="options">可变创建选项，仅在池创建时读取；不得并发修改。</param>
        /// <param name="defaultCapacity">构造预热数量，不能为负数。</param>
        /// <param name="maxSize">最大空闲保留数量，必须为正数，不限制活动对象数量。</param>
        /// <returns>传入的同一个可变选项实例。</returns>
        public static ObjectPoolOptions<T> WithCapacity<T>(this ObjectPoolOptions<T> options, int defaultCapacity, int maxSize) where T : class
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.DefaultCapacity = defaultCapacity;
            options.MaxSize = maxSize;
            return options;
        }

        /// <summary>
        /// 修改可变选项实例的裁剪策略及禁止常规裁剪标志，不更新现有池。
        /// </summary>
        /// <typeparam name="T">池中保存的引用类型。</typeparam>
        /// <param name="options">可变创建选项，仅在池创建时读取；不得并发修改。</param>
        /// <param name="trimPolicy">无参常规裁剪所使用的策略。</param>
        /// <param name="neverTrim">是否禁止常规裁剪，不影响强制裁剪。</param>
        /// <returns>传入的同一个可变选项实例。</returns>
        public static ObjectPoolOptions<T> WithTrim<T>(this ObjectPoolOptions<T> options, PoolTrimPolicy trimPolicy, bool neverTrim = false) where T : class
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.TrimPolicy = neverTrim ? PoolTrimPolicy.KeepAll : trimPolicy;
            options.NeverTrim = neverTrim;
            return options;
        }
    }
}
