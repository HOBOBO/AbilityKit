namespace AbilityKit.Core.Pooling
{
    /// <summary>Provides a thread-safe point-in-time snapshot of object-pool counters.</summary>
    public readonly struct PoolStats
    {
        /// <summary>Gets the number of elements successfully created.</summary>
        public readonly int CreatedTotal;
        /// <summary>Gets the number of acquisition attempts.</summary>
        public readonly int GetTotal;
        /// <summary>Gets the number of release attempts.</summary>
        public readonly int ReleaseTotal;
        /// <summary>Gets the number of elements currently retained for reuse.</summary>
        public readonly int InactiveCount;
        /// <summary>Gets the number of acquired or lifecycle-transitioning elements.</summary>
        public readonly int ActiveCount;
        /// <summary>Gets the greatest observed active count.</summary>
        public readonly int PeakActiveCount;
        /// <summary>Gets the number of acquisitions served from inactive storage.</summary>
        public readonly int HitCount;
        /// <summary>Gets the number of acquisitions that required creation.</summary>
        public readonly int MissCount;
        /// <summary>Gets the number of released elements destroyed because inactive storage was full.</summary>
        public readonly int OverflowDestroyCount;
        /// <summary>Gets the number of elements destroyed by clear operations.</summary>
        public readonly int ClearDestroyCount;
        /// <summary>Gets the number of inactive elements discarded without destruction.</summary>
        public readonly int DroppedInactiveCount;
        /// <summary>Gets the number of elements destroyed by trim operations.</summary>
        public readonly int TrimDestroyCount;

        /// <summary>Creates a snapshot containing the original five aggregate counters.</summary>
        public PoolStats(int createdTotal, int getTotal, int releaseTotal, int inactiveCount, int activeCount)
            : this(createdTotal, getTotal, releaseTotal, inactiveCount, activeCount, activeCount, 0, createdTotal, 0, 0, 0, 0)
        {
        }

        /// <summary>Creates a snapshot containing all lifecycle and destruction counters.</summary>
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
