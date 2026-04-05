using System;

namespace AbilityKit.Common.Marker
{
    /// <summary>
    /// 标记系统的注册表接口。
    /// 由框架提供统一的注册接口，子类实现具体的存储逻辑。
    /// </summary>
    public interface IMarkerRegistry
    {
        /// <summary>
        /// 注册一个实现类型。
        /// </summary>
        void Register(Type implType);

        /// <summary>
        /// 获取已注册的类型数量。
        /// </summary>
        int Count { get; }
    }
}
