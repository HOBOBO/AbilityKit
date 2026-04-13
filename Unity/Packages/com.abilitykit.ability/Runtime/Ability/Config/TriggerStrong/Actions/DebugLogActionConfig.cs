using System;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Config
{
    [Serializable]
    public sealed class DebugLogActionConfig : ActionConfigBase
    {
        public override string Type => TriggerActionTypes.DebugLog;

        public string Message;
        public bool DumpArgs;

        public override ActionDef ToActionDef()
        {
            var dict = PooledDefArgs.Rent();
            dict["message"] = Message;
            dict["dump_args"] = DumpArgs;

            return new ActionDef(Type, dict);
        }
    }
}
