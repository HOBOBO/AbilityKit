using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Hierarchy;

namespace AbilityKit.Game.Flow
{
    internal sealed class ViewInstanceResources : IDisposable
    {
        private readonly BattleViewResourceProvider _resources;
        private BattleViewHierarchyRoot _hierarchyRoot;
        private BattleViewShellPoolStatsProvider _shellStats;
        private BattleAreaVfxPoolStatsProvider _areaStats;

        private ViewInstanceResources(BattleViewResourceProvider resources)
        {
            _resources = resources;
        }

        internal BattleViewHierarchyManager Hierarchy => _hierarchyRoot?.Manager;
        internal BattleViewShellPool ShellPool { get; private set; }
        internal BattleAreaVfxPool AreaVfxPool { get; private set; }
        internal BattleViewCameraController CameraController { get; private set; }

        internal static ViewInstanceResources Create(
            string hierarchyName,
            BattleViewResourceProvider resources,
            BattleProjectionViewCapabilities capabilities,
            bool allowMainCameraFallback = false)
        {
            var owner = new ViewInstanceResources(resources);
            try
            {
                owner._hierarchyRoot = BattleViewHierarchyRoot.Acquire(hierarchyName);
                var hierarchy = owner._hierarchyRoot.Manager;
                resources?.SetHierarchyManager(hierarchy);
                owner.ShellPool = new BattleViewShellPool(
                    modelId => resources?.CreateShellGameObject(0, modelId) ??
                               new BattleViewPrimitiveFactory().CreateActorFallback(0, modelId),
                    defaultCapacity: 8,
                    maxSize: 16,
                    hierarchy: hierarchy);
                if ((capabilities & BattleProjectionViewCapabilities.AreaEffects) != 0)
                    owner.AreaVfxPool = BattleAreaVfxPool.UsingFactory(
                        (templateId, kind) => BattleAreaPoolObjectFactory.Create(resources, templateId, kind),
                        hierarchy: hierarchy,
                        capacityPerKindPerTemplate: 8);
                if ((capabilities & BattleProjectionViewCapabilities.Camera) != 0)
                    owner.CameraController = new BattleViewCameraController(
                        BattleCameraConfig.Default, allowMainCameraFallback);

                var overlay = owner._hierarchyRoot.GetOrAddStatsOverlay();
                owner._shellStats = new BattleViewShellPoolStatsProvider(owner.ShellPool);
                overlay.RegisterProvider(owner._shellStats);
                if (owner.AreaVfxPool != null)
                {
                    owner._areaStats = new BattleAreaVfxPoolStatsProvider(owner.AreaVfxPool);
                    overlay.RegisterProvider(owner._areaStats);
                }
                return owner;
            }
            catch (Exception createFailure)
            {
                try { owner.Dispose(); }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException(
                        "View resource creation and rollback both failed.",
                        createFailure, cleanupFailure);
                }
                throw;
            }
        }

        public void Dispose()
        {
            var failures = new List<Exception>();
            void Release(Action action)
            {
                try { action(); }
                catch (Exception exception) { failures.Add(exception); }
            }

            var camera = CameraController;
            CameraController = null;
            if (camera != null) Release(camera.Reset);
            var areaPool = AreaVfxPool;
            AreaVfxPool = null;
            if (areaPool != null) Release(areaPool.Clear);
            var shellPool = ShellPool;
            ShellPool = null;
            if (shellPool != null) Release(shellPool.Clear);
            var root = _hierarchyRoot;
            _hierarchyRoot = null;
            if (root != null)
            {
                var overlay = root.GetComponent<BattleViewPoolStatsOverlay>();
                if (overlay != null)
                {
                    Release(() => overlay.UnregisterProvider(_areaStats));
                    Release(() => overlay.UnregisterProvider(_shellStats));
                }
                Release(() => _resources?.SetHierarchyManager(null));
                Release(root.Release);
            }
            _areaStats = null;
            _shellStats = null;
            if (failures.Count > 0)
                throw new AggregateException("View resource cleanup failed.", failures);
        }
    }
}
