using System;

namespace AbilityKit.Context
{
    /// <summary>
    /// 属性接口
    /// 所有属性实现此接口
    /// </summary>
    public interface IProperty
    {
        /// <summary>
        /// 属性类型 ID
        /// </summary>
        int TypeId { get; }
    }
}
