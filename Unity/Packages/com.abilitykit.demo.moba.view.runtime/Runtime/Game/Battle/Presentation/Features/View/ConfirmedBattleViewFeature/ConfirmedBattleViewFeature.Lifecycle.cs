using AbilityKit.Game.Battle.Hierarchy;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed partial class ConfirmedBattleViewFeature
    {
        /// <summary>Hierarchy root that owns all categorized view sub-roots.</summary>
        private BattleViewHierarchyRoot _hierarchyRoot;

        public void OnAttach(in GamePhaseContext ctx)
        {
            BindPresentationSession(ctx);

            // Acquire the shared battle-scene hierarchy root. Predicted and confirmed
            // view features release independent leases during teardown.
            _hierarchyRoot = BattleViewHierarchyRoot.Acquire();
            var hierarchy = _hierarchyRoot.Manager;

            var resources = PresentationResources;
            // Make the hierarchy available to the resource provider so newly created
            // shells are parented under the matching active category root.
            resources?.SetHierarchyManager(hierarchy);
            // Expose the hierarchy to sub-features (VFX, binders, etc.).
            IViewFeatureRuntime runtime = this;
            runtime.Hierarchy = hierarchy;

            ShellPool = new BattleViewShellPool(
                factory: modelId => resources?.CreateShellGameObject(actorId: 0, modelId) ?? CreateFallbackShell(modelId),
                defaultCapacity: 8,
                maxSize: 16,
                hierarchy: hierarchy);

            AreaVfxPool = BattleAreaVfxPool.UsingFactory(
                (templateId, kind) => CreateAreaPoolObject(resources, templateId, kind),
                hierarchy: hierarchy,
                capacityPerKindPerTemplate: 8);
            CameraController = new BattleViewCameraController(BattleCameraConfig.Default);

            EnsureSubFeaturesCreated();
            _subFeatureHost?.Attach(new FeatureModuleContext<ConfirmedBattleViewFeature>(ctx, this));
            OnAllSubFeaturesAttached(ctx);
        }

        private void OnAllSubFeaturesAttached(in GamePhaseContext ctx)
        {
            if (_confirmedCtx == null) return;
            var worldId = _confirmedCtx.RuntimeWorldId;
            _confirmedCtx.Hooks?.ViewBinderReady.Invoke(new ViewBinderReadyEvent(isConfirmed: true, worldId: worldId));

            CameraController?.SetCamera(null);
        }

        private static GameObject CreateFallbackShell(int modelId)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"ShellFallback_{modelId}";
            return go;
        }

        private static GameObject CreateAreaPoolObject(
            BattleViewResourceProvider resources,
            int templateId,
            BattleAreaVfxPool.PoolKind kind)
        {
            if (resources == null) return null;

            var aoe = resources.TryGetAoe(templateId);
            switch (kind)
            {
                case BattleAreaVfxPool.PoolKind.Model:
                    return resources.CreateModelGo(templateId);
                case BattleAreaVfxPool.PoolKind.Range:
                    return resources.CreateAoeRangeGo(
                        templateId,
                        aoe != null ? aoe.Radius : 1f,
                        aoe != null ? aoe.DelayMs : 0);
                case BattleAreaVfxPool.PoolKind.Vfx:
                    return resources.CreateVfxGo(aoe != null ? aoe.VfxId : templateId);
                default:
                    return null;
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            _subFeatureHost?.Detach(new FeatureModuleContext<ConfirmedBattleViewFeature>(ctx, this));

            ShellPool?.Clear();
            ShellPool = null;

            AreaVfxPool?.Clear();
            AreaVfxPool = null;

            CameraController?.Reset();
            CameraController = null;

            // Clear the stats overlay's provider list before releasing the hierarchy root.
            if (_hierarchyRoot != null)
            {
                var overlay = _hierarchyRoot.GetComponent<BattleViewPoolStatsOverlay>();
                overlay?.ClearAllProviders();
            }

            // Release this feature's lease. The shared root survives until the last
            // predicted/confirmed view feature has detached.
            _hierarchyRoot?.Release();
            _hierarchyRoot = null;

            ClearPresentationSession(ctx);
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
            _confirmedCtx?.Hooks?.ViewsRebound.Invoke(new ViewsReboundEvent(isConfirmed: true, worldId: worldId, frame: frame));
        }

        private void EnsureSubFeaturesCreated()
        {
            if (_subFeatureHost != null && _subFeatures.Count > 0) return;

            _subFeatures.Clear();
            _subFeatureBuilder.AddConfirmedViewSubFeatures(_subFeatures);

            _subFeaturePipeline.AddStandardViewSubFeatures(_subFeatures);

            _subFeatureHost = _subFeaturePipeline.CreateHost(_subFeatures);
        }
    }
}
