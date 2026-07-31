using Svelto.ECS;

namespace AbilityKit.Demo.Shooter.Runtime
{
    public sealed class ShooterSveltoGameplayShooterDescriptor : GenericEntityDescriptor<ShooterSveltoTransformComponent, ShooterSveltoHealthComponent, ShooterSveltoWeaponComponent, ShooterSveltoCooldownComponent, ShooterSveltoTargetComponent>
    {
    }

    public sealed class ShooterSveltoGameplayTargetDescriptor : GenericEntityDescriptor<ShooterSveltoTransformComponent, ShooterSveltoHealthComponent, ShooterSveltoNavigationComponent>
    {
    }

    public sealed class ShooterSveltoGameplayProjectileDescriptor : GenericEntityDescriptor<ShooterSveltoTransformComponent, ShooterSveltoProjectileComponent, ShooterSveltoProjectileDamageComponent>
    {
    }
}
