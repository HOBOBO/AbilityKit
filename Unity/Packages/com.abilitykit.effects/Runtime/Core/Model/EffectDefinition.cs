namespace AbilityKit.Effects.Core.Model
{
    public sealed class EffectDefinition
    {
        public readonly string EffectId;
        public readonly EffectScopeKey DefaultScope;
        public readonly EffectStatItem[] Stats;

        public EffectDefinition(string effectId, in EffectScopeKey defaultScope, EffectStatItem[] stats)
        {
            EffectId = effectId;
            DefaultScope = defaultScope;
            Stats = stats;
        }
    }
}
