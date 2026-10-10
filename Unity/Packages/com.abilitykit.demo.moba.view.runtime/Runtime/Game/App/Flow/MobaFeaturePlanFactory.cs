using System;
using AbilityKit.Game.View.Flow;

namespace AbilityKit.Game.Flow
{
    internal sealed class MobaFeaturePlanFactory
    {
        private static readonly string[] DefaultBattleOrder =
        {
            "context", "input", "session", "gameplay_presentation", "entity", "sync",
            "view", "hud", "loading_screen", "end_settlement", "debug_ongui"
        };

        private readonly MobaFeatureFactoryRegistry _featureFactories;

        public MobaFeaturePlanFactory(MobaFeatureFactoryRegistry featureFactories)
        {
            _featureFactories = featureFactories ?? throw new ArgumentNullException(nameof(featureFactories));
        }

        public PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature> CreateBootFeaturePlan(int capacity = 3)
        {
            return new PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>(capacity)
                .Add("demo_lobby", (in GamePhaseContext ctx) => _featureFactories.Create("demo_lobby", in ctx))
                .Add("formal_lobby", (in GamePhaseContext ctx) => _featureFactories.Create("formal_lobby", in ctx))
                .Add("root_debug", (in GamePhaseContext ctx) => _featureFactories.Create("root_debug", in ctx));
        }

        public PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature> CreateBattleFeaturePlan(int capacity = 10) =>
            CreateBattleFeaturePlan(MobaBattleFlowProfile.CreateStandard().Resolve(), capacity);

        public PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature> CreateBattleFeaturePlan(
            MobaBattleFlowComposition composition, int capacity = 10)
        {
            if (composition == null) throw new ArgumentNullException(nameof(composition));
            var remaining = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var stateId in composition.StateOrder)
                foreach (var featureId in composition.GetFeatures(stateId))
                    remaining.Add(featureId);

            var plan = new PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>(Math.Max(capacity, remaining.Count));
            foreach (var featureId in DefaultBattleOrder)
                if (remaining.Remove(featureId)) Add(featureId);
            foreach (var stateId in composition.StateOrder)
                foreach (var featureId in composition.GetFeatures(stateId))
                    if (remaining.Remove(featureId)) Add(featureId);
            return plan;

            void Add(string featureId) =>
                plan.Add(featureId, (in GamePhaseContext ctx) => _featureFactories.Create(featureId, in ctx));
        }
    }
}
