using System.Collections.Generic;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleViewFeature : ViewFeatureRuntimeHostBase, IGamePhaseFeature
    {
        private BattleContext _ctx;
        private readonly string _instanceKey = System.Guid.NewGuid().ToString("N");

        private readonly List<IViewSubFeature<BattleViewFeature>> _subFeatures = new List<IViewSubFeature<BattleViewFeature>>(8);
        private readonly ViewFeatureSubFeatureBuilder _subFeatureBuilder = new ViewFeatureSubFeatureBuilder();
        private readonly ViewSubFeaturePipeline _subFeaturePipeline = new ViewSubFeaturePipeline();
        private ModuleHost<FeatureModuleContext<BattleViewFeature>, IViewSubFeature<BattleViewFeature>> _subFeatureHost;

        protected override BattleContext RuntimeContext => _ctx;
        protected override bool RuntimeIsConfirmed => false;

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
            _ctx.Hooks?.ViewBinderReady.Invoke(new ViewBinderReadyEvent(isConfirmed: false, worldId: worldId));
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

            var frame = _ctx.LastFrame;
            var worldId = _ctx.RuntimeWorldId;
            _ctx.Hooks?.ViewsRebound.Invoke(new ViewsReboundEvent(isConfirmed: false, worldId: worldId, frame: frame));
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
