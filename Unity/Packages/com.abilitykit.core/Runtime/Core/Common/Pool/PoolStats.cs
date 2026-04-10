namespace AbilityKit.Core.Common.Pool
{
    public readonly struct PoolStats
    {
        public readonly int CreatedTotal;
        public readonly int GetTotal;
        public readonly int ReleaseTotal;
        public readonly int InactiveCount;
        public readonly int ActiveCount;

        public PoolStats(int createdTotal, int getTotal, int releaseTotal, int inactiveCount, int activeCount)
        {
            CreatedTotal = createdTotal;
            GetTotal = getTotal;
            ReleaseTotal = releaseTotal;
            InactiveCount = inactiveCount;
            ActiveCount = activeCount;
        }
    }
}
