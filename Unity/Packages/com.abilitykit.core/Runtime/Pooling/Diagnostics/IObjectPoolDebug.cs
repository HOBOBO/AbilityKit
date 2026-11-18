using System;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 为编辑器提供无需反射的元素类型、容量及统计快照读取。
    /// </summary>
    internal interface IObjectPoolDebug
    {
        Type ElementType { get; }
        PoolStats Stats { get; }
        int MaxSize { get; }
        bool NeverTrim { get; }
    }
}
