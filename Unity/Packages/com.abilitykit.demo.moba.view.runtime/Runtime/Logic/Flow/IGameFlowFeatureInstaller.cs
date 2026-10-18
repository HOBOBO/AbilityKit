namespace AbilityKit.Game.Flow
{
    public interface IGameFlowFeatureInstaller
    {
        int AttachBootFeatures();
    }

    public interface IBattleViewFeatureHost
    {
        void Attach(IGamePhaseFeature feature);
        void Detach(IGamePhaseFeature feature);
    }
}
