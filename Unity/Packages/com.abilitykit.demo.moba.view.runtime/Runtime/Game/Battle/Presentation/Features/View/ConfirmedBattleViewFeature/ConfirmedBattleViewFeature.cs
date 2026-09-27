using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public partial class ConfirmedBattleViewFeature : ViewFeatureRuntimeHostBase, IGamePhaseFeature
    {
        private readonly BattleContext _confirmedCtx;
        private readonly string _viewRole;
        private readonly bool _isPredictionView;
        private readonly BattleProjectionViewCapabilities _capabilities;
        private readonly string _instanceKey = System.Guid.NewGuid().ToString("N");

        private readonly System.Collections.Generic.List<IViewSubFeature<ConfirmedBattleViewFeature>> _subFeatures = new System.Collections.Generic.List<IViewSubFeature<ConfirmedBattleViewFeature>>(8);
        private readonly ViewFeatureSubFeatureBuilder _subFeatureBuilder = new ViewFeatureSubFeatureBuilder();
        private readonly ViewSubFeaturePipeline _subFeaturePipeline = new ViewSubFeaturePipeline();
        private ModuleHost<FeatureModuleContext<ConfirmedBattleViewFeature>, IViewSubFeature<ConfirmedBattleViewFeature>> _subFeatureHost;

        public ConfirmedBattleViewFeature(BattleContext confirmedCtx, string viewRole = "Confirmed",
            bool isPredictionView = false)
            : this(confirmedCtx, viewRole, isPredictionView,
                isPredictionView ? BattleProjectionViewCapabilities.Actors : BattleProjectionViewCapabilities.Full)
        {
        }

        internal ConfirmedBattleViewFeature(BattleContext confirmedCtx, string viewRole,
            bool isPredictionView, BattleProjectionViewCapabilities capabilities)
        {
            _confirmedCtx = confirmedCtx;
            _viewRole = viewRole;
            _isPredictionView = isPredictionView;
            _capabilities = (capabilities & BattleProjectionViewCapabilities.Events) != 0
                ? capabilities | BattleProjectionViewCapabilities.Vfx |
                  BattleProjectionViewCapabilities.AreaEffects |
                  BattleProjectionViewCapabilities.FloatingText
                : capabilities;
            SetRuntimeQuery(confirmedCtx?.EntityQuery);
        }

        internal bool IsPredictionView => _isPredictionView;
        internal BattleProjectionViewCapabilities Capabilities => _capabilities;
    }
}
