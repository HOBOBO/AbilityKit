using System;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public sealed class DebugLogActionConfig : ActionRuntimeConfigBase
    {
        public override string Type => TriggerActionTypes.DebugLog;

        public string Message;

        public override ActionDef ToActionDef()
        {
            var dict = PooledDefArgs.Rent();
            dict["message"] = Message;

            return new ActionDef(Type, dict);
        }
    }
}
