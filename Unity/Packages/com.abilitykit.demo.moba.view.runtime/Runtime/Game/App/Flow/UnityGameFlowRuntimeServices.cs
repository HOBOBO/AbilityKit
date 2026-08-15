using System;
using AbilityKit.Demo.Moba.View.Settings;
using AbilityKit.Game.EntityCreation;
using AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal sealed class UnityGameFlowRuntimeServices : IGameFlowRuntimeServices
    {
        private readonly IGameHost _host;
        private readonly GameFlowRuntimeAdapter _runtimeAdapter;

        public UnityGameFlowRuntimeServices(IGameHost host, IEntity rootOverride, IFeatureBinder featureBinderOverride = null)
        {
            _host = host;
            _runtimeAdapter = new GameFlowRuntimeAdapter(rootOverride);
            Host = host;
            FeatureBinder = featureBinderOverride ?? _runtimeAdapter.FeatureBinder;
            Features = _runtimeAdapter.Features;
            BattleEntities = _runtimeAdapter.BattleEntities;
        }

        public IGameHost Host { get; }
        public IFeatureBinder FeatureBinder { get; }
        public IGameFeatureStore Features { get; }
        public IBattleEntityRuntime BattleEntities { get; }

        public void LoadPersistentSettings(LayeredJsonSettingsStore settings)
        {
            if (_host == null)
            {
                return;
            }

            _host.RunCoroutine(UnityJsonSettingsBootstrap.LoadPersistentInto(settings, RuntimeJsonSettingsCodec.DeserializeFlat));
        }

        public void LoadPersistentSettingsSync(LayeredJsonSettingsStore settings)
        {
            UnityJsonSettingsBootstrap.LoadPersistentIntoSync(settings, RuntimeJsonSettingsCodec.DeserializeFlat);
        }

        public bool TrySaveSettingsOverridesToPersistent(LayeredJsonSettingsStore settings)
        {
            return UnityJsonSettingsBootstrap.TrySaveOverridesToPersistent(settings.OverrideValues, RuntimeJsonSettingsCodec.SerializeFlat);
        }
    }
}
