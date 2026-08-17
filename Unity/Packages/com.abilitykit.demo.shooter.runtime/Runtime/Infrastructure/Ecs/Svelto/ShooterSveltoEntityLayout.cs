using AbilityKit.World.Svelto;

namespace AbilityKit.Demo.Shooter.Runtime
{
    internal static class ShooterSveltoEntityLayout
    {
        public static void BuildPlayer(ISveltoWorldContext context, in ShooterSveltoPlayerComponent player)
        {
            var initializer = context.EntityFactory.BuildEntity<ShooterSveltoPlayerDescriptor>((uint)player.PlayerId, ShooterSveltoGroups.Players);
            initializer.Init(player);
        }

        public static void BuildProjectile(ISveltoWorldContext context, in ShooterSveltoProjectileComponent projectile)
        {
            var initializer = context.EntityFactory.BuildEntity<ShooterSveltoProjectileDescriptor>((uint)projectile.BulletId, ShooterSveltoGroups.Projectiles);
            initializer.Init(projectile);
        }

        public static void BuildGameplayTarget(ISveltoWorldContext context, uint entityId, in ShooterSveltoTransformComponent transform, in ShooterSveltoHealthComponent health)
        {
            var navigation = default(ShooterSveltoNavigationComponent);
            BuildGameplayTarget(context, entityId, in transform, in health, in navigation);
        }

        public static void BuildGameplayTarget(
            ISveltoWorldContext context,
            uint entityId,
            in ShooterSveltoTransformComponent transform,
            in ShooterSveltoHealthComponent health,
            in ShooterSveltoNavigationComponent navigation)
        {
            var initializer = context.EntityFactory.BuildEntity<ShooterSveltoGameplayTargetDescriptor>(entityId, ShooterSveltoGroups.GameplayTargets);
            initializer.Init(transform);
            initializer.Init(health);
            initializer.Init(navigation);
        }
    }
}
