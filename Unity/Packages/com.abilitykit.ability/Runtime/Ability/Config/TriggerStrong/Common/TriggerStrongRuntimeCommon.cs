using System;
using AbilityKit.Ability.Triggering.Definitions;

namespace AbilityKit.Ability.Config
{
    [Serializable]
    public abstract class ConditionConfigBase
    {
        public abstract string Type { get; }
        public abstract ConditionDef ToConditionDef();
    }

    [Serializable]
    public abstract class ActionConfigBase
    {
        public abstract string Type { get; }
        public abstract ActionDef ToActionDef();
    }
}
