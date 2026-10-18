using AbilityKit.Demo.Moba.View.Settings;

namespace AbilityKit.Game.Flow
{
    public interface IGameFlowRuntimeServices
    {
        IGameHost Host { get; }
        IFeatureBinder FeatureBinder { get; }
        IGameFeatureStore Features { get; }
        IBattleEntityRuntime BattleEntities { get; }

        void LoadPersistentSettings(LayeredJsonSettingsStore settings);
        void LoadPersistentSettingsSync(LayeredJsonSettingsStore settings);
        bool TrySaveSettingsOverridesToPersistent(LayeredJsonSettingsStore settings);
    }
}
