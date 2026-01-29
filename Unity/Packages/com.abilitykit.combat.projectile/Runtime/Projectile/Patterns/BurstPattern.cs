using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Common.Projectile
{
    public sealed class BurstPattern : IProjectileSpawnPattern
    {
        private readonly int _count;

        public BurstPattern(int count)
        {
            _count = count <= 0 ? 1 : count;
        }

        public void Build(in ProjectileSpawnParams baseSpawn, List<ProjectileSpawnParams> results)
        {
            if (results == null) return;

            for (int i = 0; i < _count; i++)
            {
                results.Add(baseSpawn);
            }
        }
    }
}
