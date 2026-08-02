using AbilityKit.Game.Battle.Hierarchy;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleViewFeature
    {
        /// <summary>Hierarchy root that owns all categorized view sub-roots.</summary>
        private BattleViewHierarchyRoot _hierarchyRoot;

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Features.TryGet(out _ctx);
            SetRuntimeQuery(_ctx?.EntityQuery);
            BindPresentationSession(ctx);

            // Acquire the shared battle-scene hierarchy root. Predicted and confirmed
            // view features release independent leases during teardown.
            var hierarchyName = "[Battle:" +
                                (_ctx != null ? _ctx.RuntimeWorldId.ToString() : "unknown") +
                                ":Primary]";
            _hierarchyRoot = BattleViewHierarchyRoot.Acquire(hierarchyName);
            var hierarchy = _hierarchyRoot.Manager;

            IViewFeatureRuntime runtime = this;
            var resources = runtime.Resources;
            // Make the hierarchy available to the resource provider so newly created
            // shells are parented under the matching active category root.
            resources?.SetHierarchyManager(hierarchy);
            // Expose the hierarchy to sub-features (VFX, binders, etc.).
            runtime.Hierarchy = hierarchy;

            // Shell pool uses the framework ObjectPool<GameObject> per modelId bucket.
            // Factory captures resources so fresh instances are created via Resources when needed.
            ShellPool = new BattleViewShellPool(
                factory: modelId => resources?.CreateShellGameObject(actorId: 0, modelId) ?? CreateFallbackShell(modelId),
                defaultCapacity: 8,
                maxSize: 16,
                hierarchy: hierarchy);

            // Area VFX pool uses the framework ObjectPool<GameObject> per (templateId, kind) bucket.
            // Each kind has its own creation path via the factory lambdas.
            AreaVfxPool = BattleAreaVfxPool.UsingFactory(
                (templateId, kind) => BattleAreaPoolObjectFactory.Create(resources, templateId, kind),
                hierarchy: hierarchy,
                capacityPerKindPerTemplate: 8);
            CameraController = new BattleViewCameraController(BattleCameraConfig.Default);

            // Register pool providers with the debug overlay so the inspector
            // shows live reuse counts on the [Battle] root GameObject.
            var overlay = _hierarchyRoot.GetOrAddStatsOverlay();
            overlay.RegisterProvider(new BattleViewShellPoolStatsProvider(ShellPool));
            overlay.RegisterProvider(new BattleAreaVfxPoolStatsProvider(AreaVfxPool));
            // VFX pool is owned by the BattleVfxManager (created in sub-features).
            // The overlay is queried later when the manager is constructed.

            EnsureSubFeaturesCreated();
            _subFeatureHost?.Attach(new FeatureModuleContext<BattleViewFeature>(ctx, this));
            OnAllSubFeaturesAttached(ctx);
        }

        private static GameObject CreateFallbackShell(int modelId)
        {
            return new BattleViewPrimitiveFactory().CreateActorFallback(actorId: 0, modelId);
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
            _subFeatureHost?.Detach(new FeatureModuleContext<BattleViewFeature>(ctx, this));

            ShellPool?.Clear();
            ShellPool = null;

            AreaVfxPool?.Clear();
            AreaVfxPool = null;

            CameraController?.Reset();
            CameraController = null;

            // Clear the stats overlay's provider list before releasing the hierarchy root,
            // so no dangling references survive when the overlay's GameObject is destroyed.
            if (_hierarchyRoot != null)
            {
                var overlay = _hierarchyRoot.GetComponent<BattleViewPoolStatsOverlay>();
                overlay?.ClearAllProviders();
            }

            // Release this feature's lease. The shared root survives until the last
            // predicted/confirmed view feature has detached.
            if (_hierarchyRoot != null)
            {
                _hierarchyRoot.Release();
                _hierarchyRoot = null;
            }

            _ctx = null;
            ClearPresentationSession(ctx);
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
