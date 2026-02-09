using System;

namespace AbilityKit.Effects.Core.Snapshots
{
    [Serializable]
    public struct ProjectileEffectSnapshot
    {
        public float DamageMul;
        public float SpeedMul;
        public int Pierce;

        public static ProjectileEffectSnapshot Default => new()
        {
            DamageMul = 1f,
            SpeedMul = 1f,
            Pierce = 0,
        };
    }
}
