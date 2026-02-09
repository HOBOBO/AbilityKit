using System;

namespace AbilityKit.Effects.Core.Snapshots
{
    [Serializable]
    public struct LauncherEffectSnapshot
    {
        public int IntervalBaseAddFrames;
        public float IntervalPostMul;

        public int ExtraProjectilesPerShot;
        public int ExtraTotalCount;

        public static LauncherEffectSnapshot Default => new()
        {
            IntervalBaseAddFrames = 0,
            IntervalPostMul = 1f,
            ExtraProjectilesPerShot = 0,
            ExtraTotalCount = 0,
        };
    }
}
