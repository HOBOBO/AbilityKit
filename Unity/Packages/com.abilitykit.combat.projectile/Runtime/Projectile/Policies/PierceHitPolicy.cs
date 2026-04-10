using System;

namespace AbilityKit.Core.Common.Projectile
{
    public sealed class PierceHitPolicy : IProjectileHitPolicy
    {
        public readonly int MaxHits;

        public PierceHitPolicy(int maxHits)
        {
            if (maxHits < 1) throw new ArgumentOutOfRangeException(nameof(maxHits));
            MaxHits = maxHits;
        }

        public bool ShouldExitOnHit(in ProjectileHitEvent hit, ref int hitsRemaining)
        {
            if (hitsRemaining <= 0) hitsRemaining = MaxHits;

            hitsRemaining--;
            return hitsRemaining <= 0;
        }
    }
}
