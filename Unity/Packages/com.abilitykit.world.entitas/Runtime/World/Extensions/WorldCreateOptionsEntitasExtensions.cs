using System;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.World
{
    /// <summary>
    /// WorldCreateOptions 的 Entitas 扩展方法。
    /// 用于设置和获取 Entitas 上下文工厂。
    /// </summary>
    public static class WorldCreateOptionsEntitasExtensions
    {
        /// <summary>
        /// 内部使用的工厂包装类。
        /// </summary>
        private sealed class EntitasContextsFactoryBox
        {
            public IEntitasContextsFactory Factory;
        }

        /// <summary>
        /// 设置 Entitas 上下文工厂。
        /// </summary>
        /// <param name="options">世界创建选项</param>
        /// <param name="factory">Entitas 上下文工厂</param>
        /// <returns>返回选项本身，用于链式调用</returns>
        public static WorldCreateOptions SetEntitasContextsFactory(this WorldCreateOptions options, IEntitasContextsFactory factory)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            options.Extensions[typeof(EntitasContextsFactoryBox)] = new EntitasContextsFactoryBox { Factory = factory };
            return options;
        }

        /// <summary>
        /// 尝试获取 Entitas 上下文工厂。
        /// </summary>
        /// <param name="options">世界创建选项</param>
        /// <param name="factory">输出的工厂实例</param>
        /// <returns>是否成功获取</returns>
        public static bool TryGetEntitasContextsFactory(this WorldCreateOptions options, out IEntitasContextsFactory factory)
        {
            factory = null;
            if (options == null) return false;

            if (options.Extensions.TryGetValue(typeof(EntitasContextsFactoryBox), out var boxed) && boxed is EntitasContextsFactoryBox box && box.Factory != null)
            {
                factory = box.Factory;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取 Entitas 上下文工厂，如果未设置则抛出异常。
        /// </summary>
        /// <param name="options">世界创建选项</param>
        /// <returns>Entitas 上下文工厂</returns>
        /// <exception cref="InvalidOperationException">当未设置工厂时抛出</exception>
        public static IEntitasContextsFactory GetEntitasContextsFactoryOrThrow(this WorldCreateOptions options)
        {
            if (TryGetEntitasContextsFactory(options, out var factory) && factory != null) return factory;
            throw new InvalidOperationException("[EntitasWorld] EntitasContextsFactory is required. Set it via WorldCreateOptions.SetEntitasContextsFactory(...).");
        }
    }
}