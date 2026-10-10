using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleViewFeature
    {
        public void OnAttach(in GamePhaseContext ctx)
        {
            _ctx = ctx.RequireFeature<BattleContext>(nameof(BattleViewFeature));
            SetRuntimeQuery(_ctx?.EntityQuery);
            try
            {
                BindPresentationSession(ctx);
                AttachViewResources("Primary", _instanceKey, BattleProjectionViewCapabilities.Full);
                EnsureSubFeaturesCreated();
                _subFeatureHost?.Attach(new FeatureModuleContext<BattleViewFeature>(ctx, this));
                OnAllSubFeaturesAttached(ctx);
            }
            catch (System.Exception attachFailure)
            {
                try { OnDetach(ctx); }
                catch (System.Exception cleanupFailure)
                {
                    throw new System.AggregateException(
                        "Primary view attach and rollback both failed.", attachFailure, cleanupFailure);
                }
                throw;
            }
        }

        private void OnAllSubFeaturesAttached(in GamePhaseContext ctx)
        {
            if (_ctx == null) return;
            var worldId = _ctx.RuntimeWorldId;
            _ctx?.Hooks?.ViewBinderReady.Invoke(new ViewBinderReadyEvent(isConfirmed: false, worldId: worldId));

            CameraController?.SetCamera(null);
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            try
            {
                if (_subFeatureHost?.IsAttached == true)
                    _subFeatureHost.Detach(new FeatureModuleContext<BattleViewFeature>(ctx, this));
            }
            finally
            {
                try { ReleaseViewResources(); }
                finally
                {
                    _ctx = null;
                    ClearPresentationSession(ctx);
                }
            }
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_ctx?.EntityWorld == null) return;

            // Tick sub-features first so that:
            //   1. ViewBindingSubFeature.Tick() applies the authoritative snapshot to
            //      shell GameObjects (interpolation + smoothing).
            //   2. Camera then reads the same authoritative snapshot position
            //      (BattleTransformComponent.Position) that the HUD aim preview uses,
            //      eliminating the one-frame visual pop that occurs when a skill is
            //      cast while the player is moving.
            _subFeatureHost?.Tick(new FeatureModuleContext<BattleViewFeature>(ctx, this), deltaTime);

            if (CameraController != null &&
                _ctx.TryResolveLocalActorId(out var localActorId) &&
                CameraController.TrackedActorId != localActorId)
            {
                CameraController.TrackActor(localActorId);
            }

            CameraController?.Tick(_ctx.EntityQuery, deltaTime);
        }

        public void RebindAll()
        {
            if (_ctx?.EntityWorld == null) return;
            _subFeatureHost?.RebindAll(new FeatureModuleContext<BattleViewFeature>(default, this));

            var frame = _ctx != null ? _ctx.LastFrame : 0;
            var worldId = _ctx != null ? _ctx.RuntimeWorldId : default;
            _ctx?.Hooks?.ViewsRebound.Invoke(new ViewsReboundEvent(isConfirmed: false, worldId: worldId, frame: frame));
        }

        private void EnsureSubFeaturesCreated()
        {
            if (_subFeatureHost != null && _subFeatures.Count > 0) return;

            _subFeatures.Clear();
            _subFeatureBuilder.AddBattleViewSubFeatures(_subFeatures);

            _subFeaturePipeline.AddStandardViewSubFeatures(_subFeatures);

            _subFeatureHost = _subFeaturePipeline.CreateHost(_subFeatures);
        }
    }
}
