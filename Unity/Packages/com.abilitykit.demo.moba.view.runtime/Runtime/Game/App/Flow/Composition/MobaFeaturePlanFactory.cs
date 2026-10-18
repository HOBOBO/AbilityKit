using System;
using System.Collections.Generic;
using AbilityKit.Game.View.Flow;

namespace AbilityKit.Game.Flow
{
    internal sealed class MobaFeaturePlanFactory
    {
        private readonly MobaFeatureFactoryRegistry _featureFactories;

        public MobaFeaturePlanFactory(MobaFeatureFactoryRegistry featureFactories)
        {
            _featureFactories = featureFactories ?? throw new ArgumentNullException(nameof(featureFactories));
        }

        public PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature> CreateBootFeaturePlan(int capacity = 3) =>
            CreateBootFeaturePlan(MobaFlowConfiguration.CreateDefault(), capacity);

        public PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature> CreateBootFeaturePlan(
            MobaFlowConfiguration configuration, int capacity = 3)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var plan = new PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>(capacity);
            AddFeatures(configuration.BootFeatures.FeatureIds);
            AddFeatures(configuration.LobbyFeatures.FeatureIds);
            return plan;

            void AddFeatures(IReadOnlyList<string> featureIds)
            {
                foreach (var featureId in featureIds)
                    if (ids.Add(featureId))
                        plan.Add(featureId, (in GamePhaseContext ctx) => _featureFactories.Create(featureId, in ctx));
            }
        }

        public PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature> CreateBattleFeaturePlan(int capacity = 10) =>
            CreateBattleFeaturePlan(MobaBattleFlowProfile.CreateStandard().Resolve(), capacity);

        public PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature> CreateBattleFeaturePlan(
            MobaBattleFlowComposition composition, int capacity = 10)
        {
            if (composition == null) throw new ArgumentNullException(nameof(composition));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var plan = new PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>(capacity);
            foreach (var stateId in composition.StateOrder)
                foreach (var featureId in composition.GetFeatures(stateId))
                    if (ids.Add(featureId)) Add(featureId);
            return plan;

            void Add(string featureId) =>
                plan.Add(featureId, (in GamePhaseContext ctx) => _featureFactories.Create(featureId, in ctx));
        }
    }
}
