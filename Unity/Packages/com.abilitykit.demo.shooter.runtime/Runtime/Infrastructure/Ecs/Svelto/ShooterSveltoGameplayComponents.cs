using Svelto.ECS;

namespace AbilityKit.Demo.Shooter.Runtime
{
    public struct ShooterSveltoTransformComponent : IEntityComponent
    {
        public float X;
        public float Y;
        public float DirectionX;
        public float DirectionY;
    }

    public struct ShooterSveltoHealthComponent : IEntityComponent
    {
        public int Current;
        public int Max;
        public int Alive;
    }

    public struct ShooterSveltoNavigationComponent : IEntityComponent
    {
        public float VelocityX;
        public float VelocityY;
        public float Radius;
        public float MaxSpeed;
    }

    public struct ShooterSveltoWeaponComponent : IEntityComponent
    {
        public int LoadoutId;
        public float ProjectileSpeed;
        public int ProjectileLifeFrames;
        public int Damage;
        public int CooldownFrames;
        public int ProjectilesPerShot;
        public float SpreadRadians;
    }

    public struct ShooterSveltoCooldownComponent : IEntityComponent
    {
        public int RemainingFrames;
    }

    public struct ShooterSveltoTargetComponent : IEntityComponent
    {
        public uint TargetEntityId;
    }

    public struct ShooterSveltoProjectileDamageComponent : IEntityComponent
    {
        public int Damage;
        public uint OwnerEntityId;
        public ShooterSveltoGameplayFaction TargetFaction;
        public uint TargetEntityId;
    }

    public enum ShooterSveltoGameplayFaction
    {
        Unknown = 0,
        Shooter = 1,
        Target = 2
    }
}
