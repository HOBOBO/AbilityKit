using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Triggering.CodeGen;
using UnityEngine.Scripting;

namespace AbilityKit.Demo.Moba.Triggering
{
    [TriggerActionType("shoot_projectile", "鍙戝皠瀛愬脊", "琛屼负/Projectile", 0)]
    [TriggerAction("shoot_projectile")]
    [TriggerParam(0, "launcherId")]
    [TriggerParam(1, "projectileId")]
    [Preserve]
    public sealed class ShootProjectileActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return ShootProjectileAction.FromDef(def);
        }
    }
}
