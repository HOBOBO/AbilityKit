using System;

namespace AbilityKit.Core.Markers
{
    /// <summary>
    /// 兼容类型扫描的特性基类，本身不提供自动发现、实例创建或代码生成。
    /// 特性用法限制由派生特性的 AttributeUsage 决定。
    /// </summary>
    public abstract class MarkerAttribute : Attribute
    {
        /// <summary>
        /// 处理扫描到的类型；默认空实现不登记任何内容。
        /// 派生实现负责所需的注册和类型约束，回调异常由扫描入口传播。
        /// </summary>
        /// <param name="implType">当前扫描到的非抽象、非接口类型。</param>
        /// <param name="registry">本次扫描传入的注册表。</param>
        public virtual void OnScanned(Type implType, IMarkerRegistry registry) { }
    }
}
