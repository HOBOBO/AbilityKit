using System;

namespace AbilityKit.Effects.Core.Defs
{
    [Serializable]
    public sealed class EffectDef
    {
        public string EffectId;
        public EffectScopeDef DefaultScope;
        public EffectItemDef[] Items;
    }

    [Serializable]
    public sealed class EffectScopeDef
    {
        public string Kind;
        public int Id;
    }

    [Serializable]
    public sealed class EffectItemDef
    {
        public string Type;
        public string Key;
        public string Op;
        public EffectValueDef Value;
        public EffectScopeDef Scope;
    }

    [Serializable]
    public sealed class EffectValueDef
    {
        public string Mode;

        public float F;
        public int I;

        public float Min;
        public float Max;

        public int MinInt;
        public int MaxInt;
    }
}
