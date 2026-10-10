using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public partial class ConfirmedBattleViewFeature
    {
        public void OnAttach(in GamePhaseContext ctx)
        {
            try
            {
                BindPresentationSession(ctx);
                AttachCore(ctx);
            }
            catch (System.Exception attachFailure)
            {
                try
                {
                    OnDetach(ctx);
                }
                catch (System.Exception cleanupFailure)
                {
                    throw new System.AggregateException(
                        "Side view attach and rollback both failed.", attachFailure, cleanupFailure);
                }
                throw;
            }
        }

        private void AttachCore(in GamePhaseContext ctx)
        {
            AttachViewResources(_viewRole, _instanceKey, _capabilities);

            EnsureSubFeaturesCreated();
            _subFeatureHost?.Attach(new FeatureModuleContext<ConfirmedBattleViewFeature>(ctx, this));
            OnAllSubFeaturesAttached(ctx);
        }

        private void OnAllSubFeaturesAttached(in GamePhaseContext ctx)
        {
            if (_confirmedCtx == null) return;
            var worldId = _confirmedCtx.RuntimeWorldId;
            _confirmedCtx.Hooks?.ViewBinderReady.Invoke(new ViewBinderReadyEvent(isConfirmed: RuntimeIsConfirmed, worldId: worldId));

            CameraController?.SetCamera(null);
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            try
            {
                if (_subFeatureHost?.IsAttached == true)
                    _subFeatureHost.Detach(new FeatureModuleContext<ConfirmedBattleViewFeature>(ctx, this));
            }
            finally
            {
                try { ReleaseViewResources(); }
                finally { ClearPresentationSession(ctx); }
            }
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_confirmedCtx?.EntityWorld == null) return;

            // Tick sub-features first so that:
            //   1. ViewBindingSubFeature.Tick() applies the authoritative snapshot to
            //      shell GameObjects (interpolation + smoothing).
            //   2. Camera then reads the same authoritative snapshot position
            //      (BattleTransformComponent.Position) that the HUD aim preview uses,
            //      eliminating the one-frame visual pop that occurs when a skill is
            //      cast while the player is moving.
            _subFeatureHost?.Tick(new FeatureModuleContext<ConfirmedBattleViewFeature>(ctx, this), deltaTime);

            if (CameraController != null &&
                _confirmedCtx.TryResolveLocalActorId(out var localActorId) &&
                CameraController.TrackedActorId != localActorId)
            {
                CameraController.TrackActor(localActorId);
            }

            CameraController?.Tick(_confirmedCtx.EntityQuery, deltaTime);
        }

        public void RebindAll()
        {
            if (_confirmedCtx?.EntityWorld == null) return;
            _subFeatureHost?.RebindAll(new FeatureModuleContext<ConfirmedBattleViewFeature>(default, this));

            var frame = _confirmedCtx != null ? _confirmedCtx.LastFrame : 0;
            var worldId = _confirmedCtx != null ? _confirmedCtx.RuntimeWorldId : default;
            _confirmedCtx?.Hooks?.ViewsRebound.Invoke(new ViewsReboundEvent(isConfirmed: RuntimeIsConfirmed, worldId: worldId, frame: frame));
        }

        private void EnsureSubFeaturesCreated()
        {
            if (_subFeatureHost != null && _subFeatures.Count > 0) return;

            _subFeatures.Clear();
            _subFeatureBuilder.AddConfirmedViewSubFeatures(
                _subFeatures, _capabilities, includeBuiltInEventAdapters: !_isPredictionView);

            _subFeaturePipeline.AddStandardViewSubFeatures(_subFeatures, _capabilities);

            _subFeatureHost = _subFeaturePipeline.CreateHost(_subFeatures);
        }
    }
}
