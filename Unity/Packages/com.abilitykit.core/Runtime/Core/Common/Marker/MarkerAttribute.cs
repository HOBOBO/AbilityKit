using System;
using System.Reflection;

namespace AbilityKit.Common.Marker
{
    /// <summary>
    /// 框架级类型标记基类。
    /// 继承此 Attribute 的子类可用于框架自动识别、注册、代码生成等场景。
    /// </summary>
    /// <example>
    /// <code>
    /// // 定义自定义 Attribute
    /// [AttributeUsage(AttributeTargets.Class)]
    /// public sealed class ServiceAttribute : MarkerAttribute
    /// {
    ///     public Type ServiceType { get; }
    ///     public ServiceAttribute(Type serviceType) => ServiceType = serviceType;
    /// }
    ///
    /// // 使用
    /// [Service(typeof(IMyService))]
    /// public sealed class MyServiceImpl : IMyService { }
    ///
    /// // 框架扫描并处理
    /// MarkerScanner&lt;ServiceAttribute&gt;.Scan(assemblies, registry);
    /// </code>
    /// </example>
    public abstract class MarkerAttribute : Attribute
    {
        /// <summary>
        /// 框架扫描到带此 Attribute 的类型时调用。
        /// 子类可在此方法中实现自定义的注册逻辑。
        /// </summary>
        /// <param name="implType">标记了此 Attribute 的具体类型</param>
        /// <param name="registry">框架提供的 Registry</param>
        public virtual void OnScanned(Type implType, IMarkerRegistry registry) { }
    }
}
