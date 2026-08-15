using System;
using System.Collections.Generic;
using System.Reflection;

namespace AbilityKit.Core.Markers
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
    [Obsolete("Global marker bootstrapping does not belong to Core; use owner-controlled discovery or generated registration before the next major version.")]
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
    [Obsolete("Global marker bootstrapping does not belong to Core; use owner-controlled discovery or generated registration before the next major version.")]
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
    [Obsolete("Static registration side effects do not belong to Core; use explicit owner-controlled or generated registration before the next major version.")]
    public abstract class StaticMarkerBootstrapper<TSelf, TAttr, TRegistry>
        where TSelf : StaticMarkerBootstrapper<TSelf, TAttr, TRegistry>, new()
        where TAttr : MarkerAttribute
        where TRegistry : class, IMarkerRegistry
    {
        // 字段值本身不读取；保留它是为了配合 static 构造里的注册副作用（Register 在 static ctor 中完成），
        // 并作为“已注册”的静态标记。故在此显式抑制 CS0414。
#pragma warning disable CS0414
        private static readonly bool _registered;
#pragma warning restore CS0414

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
