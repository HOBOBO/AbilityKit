using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Ability.Impl.Triggering
{
    [TriggerActionType("shoot_projectile", "发射子弹", "行为/Projectile", 0)]
    [Preserve]
    public sealed class ShootProjectileActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return ShootProjectileAction.FromDef(def);
        }
    }
}
