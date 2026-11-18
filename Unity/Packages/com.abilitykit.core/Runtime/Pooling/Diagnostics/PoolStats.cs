namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 保存对象池计数的值类型快照；只有从池的 Stats 获取时才由池锁保证同一时点。
    /// </summary>
    public readonly struct PoolStats
    {
        /// <summary>
        /// 获取工厂成功返回非空对象的累计数量，包含预热和后续初始化失败的对象。
        /// </summary>
        public readonly int CreatedTotal;
        /// <summary>
        /// 获取获取操作的尝试次数，包含工厂或初始化失败的调用。
        /// </summary>
        public readonly int GetTotal;
        /// <summary>
        /// 获取非空对象的归还尝试次数，包含重复校验拒绝及回调失败的调用。
        /// </summary>
        public readonly int ReleaseTotal;
        /// <summary>
        /// 获取当前留在空闲栈中可供复用的对象数量。
        /// </summary>
        public readonly int InactiveCount;
        /// <summary>
        /// 获取尚未归还或处于获取及归还转换中的对象计数；不包含预热中的对象，销毁承诺已提交的对象也不包含。
        /// </summary>
        public readonly int ActiveCount;
        /// <summary>
        /// 获取池在获取路径中观察到的最大活动对象数量。
        /// </summary>
        public readonly int PeakActiveCount;
        /// <summary>
        /// 获取从空闲栈命中的次数，即使后续初始化失败也计入。
        /// </summary>
        public readonly int HitCount;
        /// <summary>
        /// 获取空闲栈未命中、需要创建对象的次数，即使工厂失败也计入。
        /// </summary>
        public readonly int MissCount;
        /// <summary>
        /// 获取因空闲容量已满而承诺销毁的对象数量，不代表销毁回调全部成功。
        /// </summary>
        public readonly int OverflowDestroyCount;
        /// <summary>
        /// 获取因销毁式清空而承诺移除的对象数量，不代表销毁回调全部成功。
        /// </summary>
        public readonly int ClearDestroyCount;
        /// <summary>
        /// 获取通过非销毁清空直接丢弃的空闲对象累计数量。
        /// </summary>
        public readonly int DroppedInactiveCount;
        /// <summary>
        /// 获取因裁剪而承诺移除的对象数量，不代表销毁回调全部成功。
        /// </summary>
        public readonly int TrimDestroyCount;

        /// <summary>
        /// 使用原有五项计数创建兼容快照，其余计数按兼容规则补齐。
        /// </summary>
        public PoolStats(int createdTotal, int getTotal, int releaseTotal, int inactiveCount, int activeCount)
            : this(createdTotal, getTotal, releaseTotal, inactiveCount, activeCount, activeCount, 0, createdTotal, 0, 0, 0, 0)
        {
        }

        /// <summary>
        /// 使用全部生命周期及移除计数创建值类型快照，不校验传入计数间的一致性。
        /// </summary>
        public PoolStats(
            int createdTotal,
            int getTotal,
            int releaseTotal,
            int inactiveCount,
            int activeCount,
            int peakActiveCount,
            int hitCount,
            int missCount,
            int overflowDestroyCount,
            int clearDestroyCount,
            int droppedInactiveCount,
            int trimDestroyCount)
        {
            CreatedTotal = createdTotal;
            GetTotal = getTotal;
            ReleaseTotal = releaseTotal;
            InactiveCount = inactiveCount;
            ActiveCount = activeCount;
            PeakActiveCount = peakActiveCount;
            HitCount = hitCount;
            MissCount = missCount;
            OverflowDestroyCount = overflowDestroyCount;
            ClearDestroyCount = clearDestroyCount;
            DroppedInactiveCount = droppedInactiveCount;
            TrimDestroyCount = trimDestroyCount;
        }
    }
}
