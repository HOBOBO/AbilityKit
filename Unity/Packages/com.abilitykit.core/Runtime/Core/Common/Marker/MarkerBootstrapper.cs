using System;
using System.Collections.Generic;
using System.Reflection;

namespace AbilityKit.Core.Common.Marker
{
    /// <summary>
    /// Marker 引导器基类。
    /// 继承此类可以自动注册到 MarkerSystem，实现模块化的标记扫描。
    /// </summary>
    /// <typeparam name="TAttr">MarkerAttribute 子类</typeparam>
    /// <typeparam name="TRegistry">对应的 Registry 类型</typeparam>
    /// <example>
    /// <code>
    /// public sealed class MyMarkerBootstrapper : MarkerBootstrapper&lt;MyAttribute, MyRegistry&gt;
    /// {
    ///     public MyMarkerBootstrapper() : base(MyRegistry.Instance) { }
    /// }
    /// </code>
    /// </example>
    public abstract class MarkerBootstrapper<TAttr, TRegistry>
        where TAttr : MarkerAttribute
        where TRegistry : class, IMarkerRegistry
    {
        /// <summary>
        /// Registry 实例。
        /// </summary>
        protected TRegistry Registry { get; }

        /// <summary>
        /// 要扫描的程序集过滤器。
        /// </summary>
        protected virtual Func<Assembly, bool>? AssemblyFilter => null;

        /// <summary>
        /// 是否在静态构造函数中自动注册到 MarkerSystem。
        /// 默认为 true。
        /// </summary>
        protected virtual bool AutoRegister => true;

        /// <summary>
        /// 创建引导器。
        /// </summary>
        /// <param name="registry">Registry 实例</param>
        protected MarkerBootstrapper(TRegistry registry)
        {
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));

            if (AutoRegister)
            {
                MarkerSystem.Register<TAttr, TRegistry>(Registry, AssemblyFilter);
            }
        }
    }

    /// <summary>
    /// KeyedMarkerRegistry 的引导器基类。
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TAttr">MarkerAttribute 子类</typeparam>
    /// <typeparam name="TRegistry">对应的 Registry 类型</typeparam>
    public abstract class KeyedMarkerBootstrapper<TKey, TAttr, TRegistry>
        where TAttr : MarkerAttribute
        where TRegistry : KeyedMarkerRegistry<TKey, TAttr>
    {
        /// <summary>
        /// Registry 实例。
        /// </summary>
        protected TRegistry Registry { get; }

        /// <summary>
        /// 要扫描的程序集过滤器。
        /// </summary>
        protected virtual Func<Assembly, bool>? AssemblyFilter => null;

        /// <summary>
        /// 是否在静态构造函数中自动注册到 MarkerSystem。
        /// </summary>
        protected virtual bool AutoRegister => true;

        protected KeyedMarkerBootstrapper(TRegistry registry)
        {
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));

            if (AutoRegister)
            {
                MarkerSystem.Register<TAttr, TRegistry>(Registry, AssemblyFilter);
            }
        }
    }

    /// <summary>
    /// 提供静态初始化的便捷基类。
    /// 适合需要在模块加载时立即注册的场景。
    /// </summary>
    /// <typeparam name="TSelf">子类类型</typeparam>
    /// <typeparam name="TAttr">MarkerAttribute 子类</typeparam>
    /// <typeparam name="TRegistry">Registry 类型</typeparam>
    public abstract class StaticMarkerBootstrapper<TSelf, TAttr, TRegistry>
        where TSelf : StaticMarkerBootstrapper<TSelf, TAttr, TRegistry>, new()
        where TAttr : MarkerAttribute
        where TRegistry : class, IMarkerRegistry
    {
        private static readonly bool _registered;

        static StaticMarkerBootstrapper()
        {
            var self = new TSelf();
            self.Register();
            _registered = true;
        }

        /// <summary>
        /// 获取 Registry 实例。
        /// </summary>
        protected abstract TRegistry CreateRegistry();

        /// <summary>
        /// 获取程序集过滤器。
        /// </summary>
        protected virtual Func<Assembly, bool>? AssemblyFilter => null;

        /// <summary>
        /// 注册到 MarkerSystem。
        /// </summary>
        protected virtual void Register()
        {
            MarkerSystem.Register<TAttr, TRegistry>(CreateRegistry(), AssemblyFilter);
        }

        /// <summary>
        /// 确保静态初始化已完成。
        /// </summary>
        public static void EnsureInitialized()
        {
            // 触发静态构造函数
            _ = typeof(TSelf);
        }
    }
}
