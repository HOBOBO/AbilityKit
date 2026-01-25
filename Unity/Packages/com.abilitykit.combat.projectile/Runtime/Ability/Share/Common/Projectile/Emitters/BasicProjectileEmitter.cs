using System;

namespace AbilityKit.Ability.Share.Common.Projectile
{
    public sealed class BasicProjectileEmitter : IProjectileEmitter
    {
        private readonly ProjectileWorld _world;

        public BasicProjectileEmitter(ProjectileWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public ProjectileId Emit(in ProjectileSpawnParams spawn)
        {
            return _world.Spawn(in spawn);
        }
    }
}
