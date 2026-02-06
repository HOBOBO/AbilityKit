namespace AbilityKit.Game.Flow.Modules
{
    public readonly struct FeatureModuleContext<TFeature>
    {
        public readonly GamePhaseContext Phase;
        public readonly TFeature Feature;

        public FeatureModuleContext(in GamePhaseContext phase, TFeature feature)
        {
            Phase = phase;
            Feature = feature;
        }
    }
}
