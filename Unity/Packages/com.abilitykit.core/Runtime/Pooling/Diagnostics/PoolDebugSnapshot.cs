using System;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 描述一个键对应的池及其编辑器诊断快照，不包含作用域的生命周期控制。
    /// </summary>
    public readonly struct PoolDebugSnapshot
    {
        /// <summary>
        /// 获取对象池保存的元素类型。
        /// </summary>
        public readonly Type ElementType;
        /// <summary>
        /// 获取元素类型和作用域内的池键。
        /// </summary>
        public readonly PoolKey Key;
        /// <summary>
        /// 获取捕获的对象池计数值。
        /// </summary>
        public readonly PoolStats Stats;
        /// <summary>
        /// 获取快照记录的最大空闲保留数量。
        /// </summary>
        public readonly int MaxSize;
        /// <summary>
        /// 获取快照记录的禁止常规裁剪标志。
        /// </summary>
        public readonly bool NeverTrim;

        /// <summary>
        /// 创建允许常规裁剪的编辑器池快照。
        /// </summary>
        public PoolDebugSnapshot(Type elementType, PoolKey key, PoolStats stats, int maxSize)
            : this(elementType, key, stats, maxSize, neverTrim: false)
        {
        }

        /// <summary>
        /// 使用显式裁剪标志创建编辑器池快照，不主动查询或锁定对象池。
        /// </summary>
        public PoolDebugSnapshot(Type elementType, PoolKey key, PoolStats stats, int maxSize, bool neverTrim)
        {
            ElementType = elementType;
            Key = key;
            Stats = stats;
            MaxSize = maxSize;
            NeverTrim = neverTrim;
        }
    }
}
