using System;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 保存空闲对象的裁剪目标；默认结构值回落到池默认容量，裁剪只移除对象而不创建对象。
    /// </summary>
    public readonly struct PoolTrimPolicy
    {
        private readonly bool _isSpecified;

        /// <summary>
        /// 获取默认容量回落模式下的最低保留目标；不会为了满足此数值创建对象。
        /// </summary>
        public readonly int MinInactiveCount;
        /// <summary>
        /// 获取固定保留目标；等于 int.MaxValue 时改用默认容量与最低保留目标的较大值。
        /// </summary>
        public readonly int MaxInactiveCount;

        /// <summary>
        /// 创建非负且上限不小于下限的策略；有限上限直接作为裁剪目标，最大整数上限启用默认容量回落。
        /// </summary>
        public PoolTrimPolicy(int minInactiveCount, int maxInactiveCount)
        {
            if (minInactiveCount < 0) throw new ArgumentException("MinInactiveCount must be >= 0", nameof(minInactiveCount));
            if (maxInactiveCount < 0) throw new ArgumentException("MaxInactiveCount must be >= 0", nameof(maxInactiveCount));
            if (maxInactiveCount < minInactiveCount) throw new ArgumentException("MaxInactiveCount must be >= MinInactiveCount", nameof(maxInactiveCount));

            _isSpecified = true;
            MinInactiveCount = minInactiveCount;
            MaxInactiveCount = maxInactiveCount;
        }

        /// <summary>
        /// 获取保留目标为零的裁剪策略。
        /// </summary>
        public static PoolTrimPolicy KeepNone => new PoolTrimPolicy(0, 0);

        /// <summary>
        /// 获取保留目标回落到池默认容量的裁剪策略。
        /// </summary>
        public static PoolTrimPolicy KeepDefaultCapacity => new PoolTrimPolicy(0, int.MaxValue);

        /// <summary>
        /// 获取与 KeepDefaultCapacity 等价的历史兼容策略值；该值本身不禁止裁剪，禁止常规裁剪应使用 NeverTrim。
        /// </summary>
        public static PoolTrimPolicy KeepAll => new PoolTrimPolicy(0, int.MaxValue);

        internal int ResolveTargetInactiveCount(int defaultCapacity)
        {
            if (!_isSpecified)
            {
                return System.Math.Max(0, defaultCapacity);
            }

            if (MaxInactiveCount == int.MaxValue)
            {
                return System.Math.Max(MinInactiveCount, defaultCapacity);
            }

            return MaxInactiveCount;
        }
    }
}
