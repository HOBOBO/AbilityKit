using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public class ConfirmedBattleViewFeature : ViewFeatureRuntimeHostBase, IGamePhaseFeature
    {
        private readonly BattleContext _confirmedCtx;
        private readonly string _viewRole;
        private readonly bool _isPredictionView;
        private readonly BattleProjectionViewCapabilities _capabilities;
        private readonly Camera _assignedCamera;
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
            : this(confirmedCtx, viewRole, isPredictionView, capabilities, null)
        {
        }

        internal ConfirmedBattleViewFeature(BattleContext confirmedCtx, string viewRole,
            bool isPredictionView, BattleProjectionViewCapabilities capabilities, Camera camera)
        {
            _confirmedCtx = confirmedCtx;
            _viewRole = viewRole;
            _isPredictionView = isPredictionView;
            _capabilities = (capabilities & BattleProjectionViewCapabilities.Events) != 0
                ? capabilities | BattleProjectionViewCapabilities.Vfx |
                  BattleProjectionViewCapabilities.AreaEffects |
                  BattleProjectionViewCapabilities.FloatingText
                : capabilities;
            _assignedCamera = camera;
            SetRuntimeQuery(confirmedCtx?.EntityQuery);
        }

        internal bool IsPredictionView => _isPredictionView;
        internal BattleProjectionViewCapabilities Capabilities => _capabilities;
        internal Camera AssignedCamera => _assignedCamera;

        protected override BattleContext RuntimeContext => _confirmedCtx;
        protected override bool RuntimeIsConfirmed => !_isPredictionView;

        public void OnAttach(in GamePhaseContext ctx)
        {
            try
            {
                BindPresentationSession(ctx);
                AttachCore(ctx);
            }
            catch (System.Exception attachFailure)
            {
                try { OnDetach(ctx); }
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
            CameraController?.SetCamera(AssignedCamera);
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

            var frame = _confirmedCtx.LastFrame;
            var worldId = _confirmedCtx.RuntimeWorldId;
            _confirmedCtx.Hooks?.ViewsRebound.Invoke(new ViewsReboundEvent(isConfirmed: RuntimeIsConfirmed, worldId: worldId, frame: frame));
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
