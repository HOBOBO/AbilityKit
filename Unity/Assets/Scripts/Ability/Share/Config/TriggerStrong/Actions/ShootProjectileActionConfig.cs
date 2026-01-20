using System;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public sealed class ShootProjectileActionConfig : ActionRuntimeConfigBase
    {
        public override string Type => TriggerActionTypes.ShootProjectile;

        public ProjectileEmitterType EmitterType = ProjectileEmitterType.Linear;
        public int ProjectileCode;
        public float Speed = 10f;
        public int LifetimeFrames = 30;
        public float MaxDistance = 0f;

        public override ActionDef ToActionDef()
        {
            var dict = PooledDefArgs.Rent();
            dict["emitterType"] = (int)EmitterType;
            dict["projectileCode"] = ProjectileCode;
            dict["speed"] = Speed;
            dict["lifetimeFrames"] = LifetimeFrames;
            dict["maxDistance"] = MaxDistance;
            return new ActionDef(Type, dict);
        }
    }
}
