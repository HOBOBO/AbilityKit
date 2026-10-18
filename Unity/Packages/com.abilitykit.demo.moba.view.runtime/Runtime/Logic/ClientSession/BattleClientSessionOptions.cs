using System;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleClientSessionOptions
    {
        public BattleClientSessionOptions(int maxPendingSnapshots = 128)
        {
            if (maxPendingSnapshots < 1)
                throw new ArgumentOutOfRangeException(nameof(maxPendingSnapshots));
            MaxPendingSnapshots = maxPendingSnapshots;
        }

        public int MaxPendingSnapshots { get; }
    }
}
