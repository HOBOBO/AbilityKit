using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;

namespace AbilityKit.Demo.Moba.Services.Projectile.Launch
{
    public sealed class MobaRandomizedProjectileSpawnPattern : IProjectileSpawnPattern
    {
        private readonly IProjectileSpawnPattern _inner;
        private readonly ProjectileMO _projectile;
        private readonly IWorldRandom _random;

        public MobaRandomizedProjectileSpawnPattern(IProjectileSpawnPattern inner, ProjectileMO projectile, IWorldRandom random)
        {
            _inner = inner;
            _projectile = projectile;
            _random = random;
        }

        public void Build(in ProjectileSpawnParams baseSpawn, List<ProjectileSpawnParams> results)
        {
            if (results == null) return;
            if (_inner == null)
            {
                results.Add(baseSpawn);
                return;
            }

            var start = results.Count;
            _inner.Build(in baseSpawn, results);
            if (_projectile == null || _random == null) return;
            if (!HasRandomOffset(_projectile)) return;

            for (var i = start; i < results.Count; i++)
            {
                var spawn = results[i];
                var position = spawn.Position + ResolveLocalRandomOffset(in spawn.Direction, _projectile.SpawnRandomOffsetX, _projectile.SpawnRandomOffsetY, _projectile.SpawnRandomOffsetZ, _random);
                results[i] = spawn.WithPosition(in position);
            }
        }

        private static bool HasRandomOffset(ProjectileMO projectile)
        {
            return projectile.SpawnRandomOffsetX > 0f
                   || projectile.SpawnRandomOffsetY > 0f
                   || projectile.SpawnRandomOffsetZ > 0f;
        }

        private static Vec3 ResolveLocalRandomOffset(in Vec3 direction, float x, float y, float z, IWorldRandom random)
        {
            var local = ResolveAxisRandomOffset(x, y, z, random);
            if (local.SqrMagnitude <= 0f) return Vec3.Zero;

            var forward = direction.SqrMagnitude > 0f ? direction.Normalized : Vec3.Forward;
            var up = Vec3.Up;
            var right = Vec3.Cross(in up, in forward).Normalized;
            if (right.SqrMagnitude <= 0f) right = Vec3.Right;

            return right * local.X + Vec3.Up * local.Y + forward * local.Z;
        }

        private static Vec3 ResolveAxisRandomOffset(float x, float y, float z, IWorldRandom random)
        {
            return new Vec3(
                NextSigned(random, x),
                NextSigned(random, y),
                NextSigned(random, z));
        }

        private static float NextSigned(IWorldRandom random, float halfRange)
        {
            if (halfRange <= 0f || random == null) return 0f;
            return (random.NextFloat01() * 2f - 1f) * halfRange;
        }
    }
}
