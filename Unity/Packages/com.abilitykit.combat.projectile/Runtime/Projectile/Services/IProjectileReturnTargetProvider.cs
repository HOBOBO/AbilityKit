#if false
using AbilityKit.Core.Math;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Core.Common.Projectile
{
    public interface IProjectileReturnTargetProvider : IService
    {
        bool TryGetReturnTargetPosition(int launcherActorId, out Vec3 position);
    }
}
#endif
