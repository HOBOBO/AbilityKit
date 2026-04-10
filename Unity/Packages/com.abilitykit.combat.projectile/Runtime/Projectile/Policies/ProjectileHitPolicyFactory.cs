using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Common.Projectile
{
    internal static class ProjectileHitPolicyFactory
    {
        private static readonly Dictionary<int, PierceHitPolicy> s_pierceCache = new Dictionary<int, PierceHitPolicy>(16);
        private static readonly IProjectileHitPolicy s_infinitePierce = new InfinitePierceHitPolicy();

        public static IProjectileHitPolicy Create(ProjectileHitPolicyKind kind, int param)
        {
            switch (kind)
            {
                case ProjectileHitPolicyKind.Pierce:
                {
                    if (param < 0) return s_infinitePierce;
                    var n = System.Math.Max(1, param);
                    if (s_pierceCache.TryGetValue(n, out var cached) && cached != null) return cached;
                    var created = new PierceHitPolicy(n);
                    s_pierceCache[n] = created;
                    return created;
                }
                case ProjectileHitPolicyKind.ExitOnHit:
                default:
                    return ExitOnHitPolicy.Instance;
            }
        }

        private sealed class InfinitePierceHitPolicy : IProjectileHitPolicy
        {
            public bool ShouldExitOnHit(in ProjectileHitEvent hit, ref int hitsRemaining)
            {
                return false;
            }
        }
    }
}
