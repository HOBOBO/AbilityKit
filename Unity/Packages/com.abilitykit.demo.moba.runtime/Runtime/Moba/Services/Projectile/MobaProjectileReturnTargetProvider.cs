using AbilityKit.Core.Common.Projectile;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Core.Math;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Demo.Moba.Services.Projectile
{
    public sealed class MobaProjectileReturnTargetProvider : IProjectileReturnTargetProvider
    {
        private readonly MobaActorRegistry _registry;

        public MobaProjectileReturnTargetProvider(MobaActorRegistry registry)
        {
            _registry = registry;
        }

        public bool TryGetReturnTargetPosition(int launcherActorId, out Vec3 position)
        {
            position = Vec3.Zero;
            if (launcherActorId <= 0) return false;
            if (_registry == null) return false;
            if (!_registry.TryGet(launcherActorId, out var e) || e == null) return false;
            if (!e.hasTransform) return false;
            position = e.transform.Value.Position;
            return true;
        }

        public void Dispose()
        {
        }
    }
}
