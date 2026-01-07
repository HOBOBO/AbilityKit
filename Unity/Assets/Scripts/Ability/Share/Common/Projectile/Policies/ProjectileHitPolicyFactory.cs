using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Common.Projectile
{
    internal static class ProjectileHitPolicyFactory
    {
        private static readonly Dictionary<int, PierceHitPolicy> s_pierceCache = new Dictionary<int, PierceHitPolicy>(16);

        public static IProjectileHitPolicy Create(ProjectileHitPolicyKind kind, int param)
        {
            switch (kind)
            {
                case ProjectileHitPolicyKind.Pierce:
                {
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
    }
}
