using AbilityKit.Ability.World.DI;

namespace AbilityKit.Demo.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterMovementAndCollision(WorldContainerBuilder builder)
        {
            builder.RegisterService<global::AbilityKit.Core.Math.CollisionService, global::AbilityKit.Core.Math.CollisionService>();
            builder.RegisterServiceAlias<global::AbilityKit.Core.Math.ICollisionService, global::AbilityKit.Core.Math.CollisionService>();
        }
    }
}
