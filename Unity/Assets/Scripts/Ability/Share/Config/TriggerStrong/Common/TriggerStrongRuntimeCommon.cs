using System;
using AbilityKit.Ability.Triggering.Definitions;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public abstract class ConditionRuntimeConfigBase
    {
        public abstract string Type { get; }
        public abstract ConditionDef ToConditionDef();
    }

    [Serializable]
    public abstract class ActionRuntimeConfigBase
    {
        public abstract string Type { get; }
        public abstract ActionDef ToActionDef();
    }
}
