using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Common.Projectile
{
    public interface IProjectileSpawnPattern
    {
        void Build(in ProjectileSpawnParams baseSpawn, List<ProjectileSpawnParams> results);
    }
}
