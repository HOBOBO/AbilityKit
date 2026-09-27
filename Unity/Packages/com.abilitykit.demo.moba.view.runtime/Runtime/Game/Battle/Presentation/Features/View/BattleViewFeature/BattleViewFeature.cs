using System.Collections.Generic;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleViewFeature : ViewFeatureRuntimeHostBase, IGamePhaseFeature
    {
        private BattleContext _ctx;
        private readonly string _instanceKey = System.Guid.NewGuid().ToString("N");

        private readonly List<IViewSubFeature<BattleViewFeature>> _subFeatures = new List<IViewSubFeature<BattleViewFeature>>(8);
        private readonly ViewFeatureSubFeatureBuilder _subFeatureBuilder = new ViewFeatureSubFeatureBuilder();
        private readonly ViewSubFeaturePipeline _subFeaturePipeline = new ViewSubFeaturePipeline();
        private ModuleHost<FeatureModuleContext<BattleViewFeature>, IViewSubFeature<BattleViewFeature>> _subFeatureHost;
    }
}
