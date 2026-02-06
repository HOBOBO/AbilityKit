using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed partial class ConfirmedBattleViewFeature
    {
        public void OnAttach(in GamePhaseContext ctx)
        {
            EnsureModulesCreated();
            _moduleHost?.Attach(new FeatureModuleContext<ConfirmedBattleViewFeature>(ctx, this));

            var worldId = _confirmedCtx != null ? _confirmedCtx.RuntimeWorldId : default;
            _events?.Publish(new ViewBinderReadyEvent(isConfirmed: true, worldId: worldId));
            _events?.Flush();
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            _moduleHost?.Detach(new FeatureModuleContext<ConfirmedBattleViewFeature>(ctx, this));
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_confirmedCtx?.EntityWorld == null) return;
            _moduleHost?.Tick(new FeatureModuleContext<ConfirmedBattleViewFeature>(ctx, this), deltaTime);
            _events?.Flush();
        }

        public void RebindAll()
        {
            if (_confirmedCtx?.EntityWorld == null) return;
            _moduleHost?.RebindAll(new FeatureModuleContext<ConfirmedBattleViewFeature>(default, this));

            var frame = _confirmedCtx != null ? _confirmedCtx.LastFrame : 0;
            var worldId = _confirmedCtx != null ? _confirmedCtx.RuntimeWorldId : default;
            _events?.Publish(new ViewsReboundEvent(isConfirmed: true, worldId: worldId, frame: frame));
            _events?.Flush();
        }

        private void EnsureModulesCreated()
        {
            if (_moduleHost != null && _subFeatures.Count > 0) return;

            _subFeatures.Clear();
            AddFeatureSubFeatures(_subFeatures);

            ViewSubFeaturePipeline.AddStandardViewModules(_subFeatures);

            _moduleHost = ViewSubFeaturePipeline.CreateModuleHost(_subFeatures);
        }
    }
}
