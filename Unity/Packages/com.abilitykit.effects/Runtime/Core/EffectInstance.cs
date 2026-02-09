using System;
using AbilityKit.Effects.Core.Model;

namespace AbilityKit.Effects.Core
{
    [Serializable]
    public sealed class EffectInstance
    {
        public string InstanceId;
        public EffectDefinition Def;

        public EffectScopeKey Scope;

        public int ExpireFrame;
        public bool IsPermanent;

        public EffectInstance(string instanceId, EffectDefinition def, EffectScopeKey scope)
        {
            InstanceId = instanceId;
            Def = def;
            Scope = scope;
            ExpireFrame = 0;
            IsPermanent = true;
        }
    }
}
