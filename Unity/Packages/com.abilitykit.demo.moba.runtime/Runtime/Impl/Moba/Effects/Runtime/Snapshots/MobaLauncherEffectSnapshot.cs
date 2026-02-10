using System;

namespace AbilityKit.Ability.Impl.Moba.Effects.Runtime.Snapshots
{
    [Serializable]
    internal struct MobaLauncherEffectSnapshot
    {
        public int IntervalBaseAddFrames;
        public float IntervalPostMul;

        public int ExtraProjectilesPerShot;
        public int ExtraTotalCount;

        public static MobaLauncherEffectSnapshot Default => new()
        {
            IntervalBaseAddFrames = 0,
            IntervalPostMul = 1f,
            ExtraProjectilesPerShot = 0,
            ExtraTotalCount = 0,
        };
    }
}
