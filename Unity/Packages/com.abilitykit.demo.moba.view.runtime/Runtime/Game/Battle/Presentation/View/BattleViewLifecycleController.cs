using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewLifecycleController
    {
        private readonly BattleViewDestroyedEntityCleanupOperation _destroyedEntities;
        private readonly BattleViewAllEntitiesCleanupOperation _allEntities;

        public BattleViewLifecycleController(
            BattleViewHandleStore handles,
            BattleViewShellController shells,
            BattleViewAttachedVfxController attachedVfx,
            BattleViewTransformController transforms,
            BattleViewLifecycleControllerFactory factory = null)
        {
            factory ??= new BattleViewLifecycleControllerFactory();

            var destroyImmediatePolicy = factory.CreateDestroyImmediatePolicy();
            var handleMarker = factory.CreateHandleMarker();
            var visualCleanup = factory.CreateVisualCleanup(shells, attachedVfx, destroyImmediatePolicy);

            _destroyedEntities = factory.CreateDestroyedEntityCleanup(handles, handleMarker, visualCleanup);
            _allEntities = factory.CreateAllEntitiesCleanup(handles, transforms, handleMarker, visualCleanup);
        }

        public void OnDestroyed(EC.IEntityId id)
        {
            _destroyedEntities.Cleanup(id);
        }

        public void Clear()
        {
            _allEntities.Cleanup();
        }
    }

    internal sealed class BattleViewLifecycleControllerFactory
    {
        public BattleViewDestroyImmediatePolicy CreateDestroyImmediatePolicy()
        {
            return new BattleViewDestroyImmediatePolicy();
        }

        public BattleViewHandleLifecycleMarker CreateHandleMarker()
        {
            return new BattleViewHandleLifecycleMarker();
        }

        public BattleViewHandleVisualCleanupOperation CreateVisualCleanup(
            BattleViewShellController shells,
            BattleViewAttachedVfxController attachedVfx,
            BattleViewDestroyImmediatePolicy destroyImmediatePolicy)
        {
            return new BattleViewHandleVisualCleanupOperation(shells, attachedVfx, destroyImmediatePolicy);
        }

        public BattleViewDestroyedEntityCleanupOperation CreateDestroyedEntityCleanup(
            BattleViewHandleStore handles,
            BattleViewHandleLifecycleMarker marker,
            BattleViewHandleVisualCleanupOperation visualCleanup)
        {
            return new BattleViewDestroyedEntityCleanupOperation(handles, marker, visualCleanup);
        }

        public BattleViewAllEntitiesCleanupOperation CreateAllEntitiesCleanup(
            BattleViewHandleStore handles,
            BattleViewTransformController transforms,
            BattleViewHandleLifecycleMarker marker,
            BattleViewHandleVisualCleanupOperation visualCleanup)
        {
            return new BattleViewAllEntitiesCleanupOperation(handles, transforms, marker, visualCleanup);
        }
    }

    internal sealed class BattleViewDestroyImmediatePolicy
    {
        public bool ShouldDestroyImmediately()
        {
            return !Application.isPlaying;
        }
    }

    internal sealed class BattleViewHandleLifecycleMarker
    {
        public void MarkDestroyed(BattleViewHandle handle)
        {
            if (handle == null) return;

            handle.Destroyed = true;
            handle.Version++;
            ClearRuntimeSamples(handle);
        }

        public void ClearRuntimeSamples(BattleViewHandle handle)
        {
            handle?.Pos.Clear();
        }
    }

    internal sealed class BattleViewHandleVisualCleanupOperation
    {
        private readonly BattleViewShellController _shells;
        private readonly BattleViewAttachedVfxController _attachedVfx;
        private readonly BattleViewDestroyImmediatePolicy _destroyImmediatePolicy;

        public BattleViewHandleVisualCleanupOperation(
            BattleViewShellController shells,
            BattleViewAttachedVfxController attachedVfx,
            BattleViewDestroyImmediatePolicy destroyImmediatePolicy)
        {
            _shells = shells;
            _attachedVfx = attachedVfx;
            _destroyImmediatePolicy = destroyImmediatePolicy;
        }

        public void Cleanup(BattleViewHandle handle)
        {
            _shells.Destroy(handle, _destroyImmediatePolicy.ShouldDestroyImmediately());
            _attachedVfx.Destroy(handle);
        }
    }

    internal sealed class BattleViewDestroyedEntityCleanupOperation
    {
        private readonly BattleViewHandleStore _handles;
        private readonly BattleViewHandleLifecycleMarker _marker;
        private readonly BattleViewHandleVisualCleanupOperation _visualCleanup;

        public BattleViewDestroyedEntityCleanupOperation(
            BattleViewHandleStore handles,
            BattleViewHandleLifecycleMarker marker,
            BattleViewHandleVisualCleanupOperation visualCleanup)
        {
            _handles = handles;
            _marker = marker;
            _visualCleanup = visualCleanup;
        }

        public void Cleanup(EC.IEntityId id)
        {
            if (!_handles.TryGet(id, out var handle)) return;

            BattleViewActorTransformRegistry.Remove(handle.ActorId);
            _marker.MarkDestroyed(handle);
            _visualCleanup.Cleanup(handle);
            _handles.Remove(id);
        }
    }

    internal sealed class BattleViewAllEntitiesCleanupOperation
    {
        private readonly BattleViewHandleStore _handles;
        private readonly BattleViewTransformController _transforms;
        private readonly BattleViewHandleLifecycleMarker _marker;
        private readonly BattleViewHandleVisualCleanupOperation _visualCleanup;

        public BattleViewAllEntitiesCleanupOperation(
            BattleViewHandleStore handles,
            BattleViewTransformController transforms,
            BattleViewHandleLifecycleMarker marker,
            BattleViewHandleVisualCleanupOperation visualCleanup)
        {
            _handles = handles;
            _transforms = transforms;
            _marker = marker;
            _visualCleanup = visualCleanup;
        }

        public void Cleanup()
        {
            _handles.ForEach((_, handle) =>
            {
                if (handle != null) BattleViewActorTransformRegistry.Remove(handle.ActorId);
                _visualCleanup.Cleanup(handle);
                _marker.ClearRuntimeSamples(handle);
            });

            _handles.Clear();
            _transforms.Reset();
        }
    }
}
