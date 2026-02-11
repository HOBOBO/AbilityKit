using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterMovementAndCollision(WorldContainerBuilder builder)
        {
            builder.RegisterService<global::AbilityKit.Ability.Share.Math.CollisionService, global::AbilityKit.Ability.Share.Math.CollisionService>();
            builder.RegisterServiceAlias<global::AbilityKit.Ability.Share.Math.ICollisionService, global::AbilityKit.Ability.Share.Math.CollisionService>();
        }
    }
}
