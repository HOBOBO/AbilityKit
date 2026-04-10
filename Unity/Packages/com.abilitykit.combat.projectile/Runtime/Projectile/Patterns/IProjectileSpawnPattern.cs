using System.Collections.Generic;

namespace AbilityKit.Core.Common.Projectile
{
    public interface IProjectileSpawnPattern
    {
        void Build(in ProjectileSpawnParams baseSpawn, List<ProjectileSpawnParams> results);
    }
}
