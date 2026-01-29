using System;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public sealed class ShootProjectileActionConfig : ActionRuntimeConfigBase
    {
        public override string Type => TriggerActionTypes.ShootProjectile;

        public int LauncherId;
        public int ProjectileId;

        public override ActionDef ToActionDef()
        {
            var dict = PooledDefArgs.Rent();

            dict["launcherId"] = LauncherId;
            dict["projectileId"] = ProjectileId;
            return new ActionDef(Type, dict);
        }
    }
}
