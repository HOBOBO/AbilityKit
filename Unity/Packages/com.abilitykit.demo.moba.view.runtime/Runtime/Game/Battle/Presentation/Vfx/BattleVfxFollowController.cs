using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Platform.Unity.Presentation;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Battle.Vfx
{
    internal sealed class BattleVfxFollowController
    {
        private readonly BattleVfxFollowTargetPositionResolver _targetPositions;
        private readonly BattleVfxLifetimePolicy _lifetime;
        private readonly BattleVfxEntityCollector _collector;
        private readonly List<EC.IEntityId> _ids;
        private readonly IPresentationLifetimeAdapter<GameObject> _lifetimeAdapter;

        public BattleVfxFollowController(
            BattleVfxLifetimePolicy lifetime = null,
            BattleVfxFollowTargetPositionResolver targetPositions = null,
            BattleVfxEntityCollector collector = null,
            BattleVfxFollowControllerFactory factory = null,
            IPresentationLifetimeAdapter<GameObject> lifetimeAdapter = null)
        {
            factory ??= new BattleVfxFollowControllerFactory();

            _lifetime = lifetime ?? factory.CreateLifetimePolicy();
            _targetPositions = targetPositions ?? factory.CreateTargetPositionResolver();
            _collector = collector ?? factory.CreateCollector();
            _ids = factory.CreateIdBuffer();
            _lifetimeAdapter = lifetimeAdapter ?? new UnityPresentationLifetimeAdapter();
        }

        public void Tick(in EC.IEntity vfxRoot, BattleViewBinder binder, Action<EC.IECWorld, EC.IEntityId> destroyAction)
        {
            Tick(vfxRoot, binder, query: null, destroyAction);
        }

        public void Tick(in EC.IEntity vfxRoot, BattleViewBinder binder, IBattleEntityQuery query, Action<EC.IECWorld, EC.IEntityId> destroyAction)
        {
            if (!vfxRoot.IsValid) return;
            var world = vfxRoot.World;
            if (world == null) return;
            if (destroyAction == null) return;

            _ids.Clear();
            _collector.Collect(vfxRoot, _ids);
            if (_ids.Count == 0) return;

            for (int i = 0; i < _ids.Count; i++)
            {
                var id = _ids[i];
                if (!world.IsAlive(id)) continue;

                var entity = world.Wrap(id);
                var expired = _lifetime.IsExpired(entity);
                if (entity.TryGetRef(out BattlePresentationLifetimeComponent presentation) && presentation != null)
                {
                    var policy = presentation.Request.Lifecycle;
                    var resourceComplete = entity.TryGetRef(out BattleViewGameObjectComponent view) && view != null &&
                        _lifetimeAdapter.TryIsComplete(view.GameObject, out var complete) && complete;
                    expired = presentation.State.Tick(in policy, Time.unscaledDeltaTime, expired, resourceComplete);
                }
                if (expired)
                {
                    destroyAction(world, id);
                    continue;
                }

                if (!TrySyncFollow(world, entity, binder, query) &&
                    entity.TryGetRef(out BattlePresentationLifetimeComponent active) && active != null &&
                    entity.TryGetRef(out BattleViewFollowComponent missing) && missing != null &&
                    (missing.Target.IsValid || missing.TargetActorId > 0))
                {
                    var policy = active.Request.Lifecycle;
                    if (active.State.OnTargetMissing(in policy)) destroyAction(world, id);
                    else if (active.State.IsDetached) { missing.Target = default; missing.TargetActorId = 0; }
                }
            }
        }

        public int DestroyByFollowTargetActorId(in EC.IEntity vfxRoot, int targetActorId, Action<EC.IECWorld, EC.IEntityId> destroyAction)
        {
            if (targetActorId <= 0) return 0;
            if (!vfxRoot.IsValid) return 0;
            var world = vfxRoot.World;
            if (world == null) return 0;
            if (destroyAction == null) return 0;

            _ids.Clear();
            _collector.Collect(vfxRoot, _ids);
            if (_ids.Count == 0) return 0;

            var destroyed = 0;
            for (int i = 0; i < _ids.Count; i++)
            {
                var id = _ids[i];
                if (!world.IsAlive(id)) continue;

                var entity = world.Wrap(id);
                if (!entity.TryGetRef(out BattleViewFollowComponent follow) || follow == null) continue;
                if (follow.TargetActorId != targetActorId) continue;
                if (entity.TryGetRef(out BattlePresentationLifetimeComponent presentation) && presentation != null)
                {
                    if (!presentation.Request.FollowAttachment) continue;
                    var loss = presentation.Request.Lifecycle.TargetLoss;
                    if (loss == PresentationTargetLossPolicy.Rebind) { follow.Target = default; continue; }
                    if (loss == PresentationTargetLossPolicy.Detach)
                    {
                        var policy = presentation.Request.Lifecycle;
                        presentation.State.OnTargetMissing(in policy);
                        follow.Target = default; follow.TargetActorId = 0;
                        continue;
                    }
                }

                destroyAction(world, id);
                destroyed++;
            }

            return destroyed;
        }

        public void SyncFollow(EC.IECWorld world, EC.IEntityId vfxEntityId, in Vector3 targetPos)
        {
            var forward = Vector3.zero;
            SyncFollow(world, vfxEntityId, in targetPos, in forward);
        }

        public void SyncFollow(EC.IECWorld world, EC.IEntityId vfxEntityId, in Vector3 targetPos, in Vector3 targetForward)
        {
            if (world == null) return;
            if (!world.IsAlive(vfxEntityId)) return;

            var entity = world.Wrap(vfxEntityId);
            if (entity.TryGetRef(out BattlePresentationLifetimeComponent presentation) && presentation != null &&
                presentation.State.IsDetached) return;
            if (presentation != null)
            {
                if (!presentation.Request.FollowAttachment) return;
                if (SyncPresentation(world, entity, null, null)) return;
                // A manual pose still observes request offset space when no shell is available.
                var attachment = presentation.Request.Attachment;
                var rotation = targetForward.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(targetForward.normalized, Vector3.up) : Quaternion.identity;
                if (entity.TryGetRef(out BattleViewGameObjectComponent requestedVisual) && requestedVisual?.GameObject != null)
                    requestedVisual.GameObject.transform.SetPositionAndRotation(
                        UnityPresentationTransformUtility.ResolvePosition(targetPos, rotation, Vector3.one, in attachment), rotation);
                return;
            }
            if (!entity.TryGetRef(out BattleViewGameObjectComponent goComp) || goComp == null || goComp.GameObject == null) return;

            var pos = targetPos;
            if (entity.TryGetRef(out BattleViewFollowComponent follow) && follow != null)
            {
                pos += follow.Offset;
            }

            var tr = goComp.GameObject.transform;
            tr.position = pos;
            if (targetForward.sqrMagnitude > 0.0001f)
            {
                tr.rotation = Quaternion.LookRotation(targetForward.normalized, Vector3.up);
            }
        }

        private bool TrySyncFollow(EC.IECWorld world, EC.IEntity entity, BattleViewBinder binder, IBattleEntityQuery query)
        {
            if (entity.TryGetRef(out BattlePresentationLifetimeComponent presentation) && presentation != null)
                return SyncPresentation(world, entity, binder, query);
            if (_targetPositions.TryResolve(world, entity, binder, query, out var position, out var forward))
            {
                SyncFollow(world, entity.Id, in position, in forward);
                return true;
            }
            return false;
        }

        internal bool SyncPresentation(EC.IECWorld world, EC.IEntity entity, BattleViewBinder binder, IBattleEntityQuery query, bool initial = false)
        {
            if (!entity.TryGetRef(out BattlePresentationLifetimeComponent lifetime) || lifetime == null ||
                !entity.TryGetRef(out BattleViewGameObjectComponent visual) || visual?.GameObject == null) return false;
            var state = visual.GameObject.GetComponent<UnityPresentationInstanceState>();
            if (state == null) return false;
            var request = lifetime.Request;
            state.UpdateRequest(in request);
            state.RecordLifetime(lifetime.State.IsDetached, lifetime.State.IsStopping);
            var tr = visual.GameObject.transform;
            var attached = request.Attachment.IsAttached;
            var resolved = false;
            if (attached && !state.IsDetached && (request.FollowAttachment || !state.HasPlacedAttachment) &&
                entity.TryGetRef(out BattleViewFollowComponent follow) && follow != null &&
                BattleVfxFollowTargetPositionResolver.TryResolveTarget(world, follow, query, out var targetId))
            {
                GameObject targetObject = null;
                if (binder != null) binder.TryGetShellGameObject(targetId, out targetObject);
                if (targetObject == null && world.Wrap(targetId).TryGetRef(out BattleViewGameObjectComponent targetView))
                    targetObject = targetView?.GameObject;
                if (targetObject != null)
                {
                    var attachment = request.Attachment;
                    var target = UnityPresentationTransformUtility.ResolveSocket(targetObject.transform, in attachment);
                    tr.SetPositionAndRotation(UnityPresentationTransformUtility.ResolvePosition(target, in attachment), target.rotation);
                    state.RecordTarget(target, target.lossyScale);
                    resolved = true;
                }
                else if (_targetPositions.TryResolve(world, entity, binder, query, out var position, out var forward))
                {
                    var rotation = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity;
                    var attachment = request.Attachment;
                    // Without a shell, fall back to the logical projection including its scale.
                    var targetScale = world.Wrap(targetId).TryGetRef(out BattleTransformComponent logical) && logical != null
                        ? logical.Scale : Vector3.one;
                    tr.SetPositionAndRotation(UnityPresentationTransformUtility.ResolvePosition(position, rotation, targetScale, in attachment), rotation);
                    state.RecordTarget(null, targetScale);
                    resolved = true;
                }
            }
            else if (!attached && initial && !state.IsDetached)
            {
                tr.SetPositionAndRotation(new Vector3(request.Position.X, request.Position.Y, request.Position.Z),
                    new Quaternion(request.Rotation.X, request.Rotation.Y, request.Rotation.Z, request.Rotation.W));
            }
            var scales = state.Sources.Resolve(PresentationScaleChannel.Vfx | PresentationScaleChannel.Cue, PresentationVector3.One);
            var content = Vector3.Scale(state.BaseScale, new Vector3(scales.X, scales.Y, scales.Z));
            var inherit = attached && request.FollowAttachment && request.ParentScaleMode == PresentationParentScaleMode.Inherit;
            UnityPresentationTransformUtility.ApplyWorldScale(tr, content, inherit ? state.LastTargetScale : Vector3.one);
            if (initial && !request.FollowAttachment) state.HasPlacedAttachment = true;
            var ordering = request.Ordering;
            state.OrderingAdapter?.Apply(visual.GameObject, state.AttachmentTarget != null ? state.AttachmentTarget.gameObject : null, in ordering);
            return resolved || !attached || !request.FollowAttachment || state.IsDetached;
        }
    }

    internal sealed class BattleVfxFollowControllerFactory
    {
        public BattleVfxLifetimePolicy CreateLifetimePolicy()
        {
            return new BattleVfxLifetimePolicy();
        }

        public BattleVfxFollowTargetPositionResolver CreateTargetPositionResolver()
        {
            return new BattleVfxFollowTargetPositionResolver();
        }

        public BattleVfxEntityCollector CreateCollector()
        {
            return new BattleVfxEntityCollector();
        }

        public List<EC.IEntityId> CreateIdBuffer()
        {
            return new List<EC.IEntityId>(32);
        }
    }
}
