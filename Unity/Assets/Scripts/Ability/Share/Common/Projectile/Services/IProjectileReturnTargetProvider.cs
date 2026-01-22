#if false
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Common.Projectile
{
    public interface IProjectileReturnTargetProvider : IService
    {
        bool TryGetReturnTargetPosition(int launcherActorId, out Vec3 position);
    }
}
#endif
