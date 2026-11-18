using System;

namespace AbilityKit.Core.Markers
{
    /// <summary>
    /// 兼容扫描回调使用的最小类型登记契约。
    /// 存储方式、去重规则和计数含义由具体注册表决定。
    /// </summary>
    public interface IMarkerRegistry
    {
        /// <summary>
        /// 登记实现类型，具体过滤和副作用由实现决定。
        /// </summary>
        /// <param name="implType">交给注册表处理的类型。</param>
        void Register(Type implType);

        /// <summary>
        /// 获取实现定义的登记数量；普通注册表统计类型，键控注册表统计键。
        /// </summary>
        int Count { get; }
    }
}
