using System;

namespace AbilityKit.Ability.Impl.Moba.Effects.Runtime.Snapshots
{
    [Serializable]
    internal struct MobaProjectileEffectSnapshot
    {
        public float DamageMul;
        public float SpeedMul;
        public int Pierce;

        public static MobaProjectileEffectSnapshot Default => new()
        {
            DamageMul = 1f,
            SpeedMul = 1f,
            Pierce = 0,
        };
    }
}
