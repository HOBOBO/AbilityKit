using System.Collections.Generic;

namespace AbilityKit.Core.Common.Projectile
{
    public sealed class SingleShotPattern : IProjectileSpawnPattern
    {
        public void Build(in ProjectileSpawnParams baseSpawn, List<ProjectileSpawnParams> results)
        {
            if (results == null) return;
            results.Add(baseSpawn);
        }
    }
}
