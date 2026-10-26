using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using EC = AbilityKit.World.ECS;
using EntityBattleNetId = AbilityKit.Game.Battle.Entity.BattleNetId;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal sealed class BattlePresentationCueUnityPort :
        IPresentationCueViewPort<
            BattlePresentationCueRequestKey,
            BattlePresentationCueSpawnRequest,
            EC.IEntityId>,
        IPresentationActorPlacementSource
    {
        private readonly IBattleEntityQuery _query;
        private readonly BattlePresentationCueVfxSpawner _spawner;
        private readonly BattleVfxPresentationBackend _backend;

        public BattlePresentationCueUnityPort(
            IBattleEntityQuery query,
            BattlePresentationCueVfxSpawner spawner)
        {
            _query = query;
            _spawner = spawner;
            _backend = new BattleVfxPresentationBackend(spawner, ResolveFollowTarget);
        }

        public bool IsAlive(EC.IEntityId handle)
        {
            return _backend.IsAlive(handle);
        }

        public bool TryCreate(
            BattlePresentationCueRequestKey key,
            in BattlePresentationCueSpawnRequest payload,
            out EC.IEntityId handle)
        {
            handle = default;
            if (!_backend.CanPlay) return false;

            if (!TryCreateRequest(key, in payload, out var request) ||
                !_backend.TryPlay(in request, out var createdHandle))
            {
                return false;
            }

            handle = createdHandle;
            return true;
        }

        public void Update(
            EC.IEntityId handle,
            in BattlePresentationCueSpawnRequest payload)
        {
            if (TryCreateRequest(default, in payload, out var request)) _backend.Update(handle, in request);
        }

        public void Destroy(EC.IEntityId handle)
        {
            _backend.Stop(handle);
        }

        private bool TryCreateRequest(
            BattlePresentationCueRequestKey key,
            in BattlePresentationCueSpawnRequest payload,
            out PresentationRequest request)
        {
            request = default;
            if (payload.IsEmpty) return false;
            var position = BattlePresentationCuePlacementResolver.ResolvePosition(in payload, this);
            var followActorId = BattlePresentationCuePlacementResolver.ResolveFollowActorId(in payload, this);
            var attachment = followActorId > 0
                ? new PresentationAttachment(
                    PresentationAttachmentKind.Actor,
                    followActorId,
                    offset: new PresentationVector3(payload.Offset.X, payload.Offset.Y, payload.Offset.Z))
                : default;
            var parameters = new List<PresentationParameter>(4)
            {
                new PresentationParameter("stackCount", payload.StackCount),
                new PresentationParameter("maxStackCount", payload.MaxStackCount),
                new PresentationParameter("elapsedSeconds", payload.ElapsedSeconds),
                new PresentationParameter("remainingSeconds", payload.RemainingSeconds),
            };
            if (!_spawner.TryCreatePresentationRequest(
                    payload.VfxId,
                    key.IsEmpty ? payload.RequestKey.ToString() : key.ToString(),
                    new PresentationVector3(position.X, position.Y, position.Z),
                    out var configured,
                    attachment,
                    PresentationRotation.Identity,
                    payload.Scale)) return false;
            request = new PresentationRequest(
                configured.LogicalKey,
                configured.AssetId,
                configured.Position,
                configured.Rotation,
                configured.Attachment,
                payload.DurationMsOverride > 0 ? payload.DurationMsOverride : configured.DurationMs,
                payload.Scale,
                payload.Radius,
                parameters,
                configured.ResourceKey,
                configured.AssetKind,
                configured.Playback,
                configured.Policy);
            return request.IsValid;
        }

        public bool TryGetPosition(int actorId, out SnapshotVec3 position)
        {
            position = default;
            if (!TryResolveActorEntity(actorId, out var entity)) return false;
            if (!entity.TryGetRef(out BattleTransformComponent transform) || transform == null) return false;

            position = new SnapshotVec3(transform.Position.x, transform.Position.y, transform.Position.z);
            return true;
        }

        public bool HasActor(int actorId)
        {
            return TryResolveActorEntity(actorId, out _);
        }

        private bool TryResolveActorEntity(int actorId, out EC.IEntity entity)
        {
            entity = default;
            if (actorId <= 0 || _query == null) return false;

            return _query.TryResolve(new EntityBattleNetId(actorId), out entity);
        }

        private EC.IEntityId ResolveFollowTarget(int actorId)
        {
            return TryResolveActorEntity(actorId, out var entity) ? entity.Id : default;
        }
    }

    internal sealed class BattlePresentationCueVfxSpawner
    {
        private readonly EC.IECWorld _world;
        private readonly BattleVfxManager _vfx;
        private readonly EC.IEntity _vfxNode;

        public BattlePresentationCueVfxSpawner(
            EC.IECWorld world,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode)
        {
            _world = world;
            _vfx = vfx;
            _vfxNode = vfxNode;
        }

        public bool CanSpawn
        {
            get
            {
                if (_world == null) return false;
                if (_vfx == null) return false;
                if (!_vfxNode.IsValid) return false;
                return true;
            }
        }

        public bool TryResolveVfxId(in PresentationRequest request, out int vfxId)
        {
            vfxId = 0;
            return _vfx != null && _vfx.TryResolvePresentationVfxId(in request, out vfxId);
        }

        public bool TryCreatePresentationRequest(
            int vfxId,
            string logicalKey,
            PresentationVector3 position,
            out PresentationRequest request,
            PresentationAttachment attachment,
            PresentationRotation rotation,
            float scale)
        {
            request = default;
            return _vfx != null && _vfx.TryCreatePresentationRequest(
                vfxId,
                logicalKey,
                position,
                out request,
                attachment,
                rotation,
                scale);
        }

        public bool IsAlive(EC.IEntityId id)
        {
            return id.IsValid && _world != null && _world.IsAlive(id);
        }

        public bool TrySpawn(
            int vfxId,
            in Vector3 position,
            EC.IEntityId followTarget,
            in Quaternion rotation,
            in Vector3 followOffset,
            int followTargetActorId,
            int durationMsOverride,
            float scale,
            float radius,
            int stackCount,
            int maxStackCount,
            float elapsedSeconds,
            float remainingSeconds,
            out EC.IEntity entity)
        {
            entity = default;
            if (!CanSpawn || vfxId <= 0) return false;

            if (!_vfx.TryCreateVfxEntity(
                    _world,
                    _vfxNode,
                    vfxId,
                    followTarget,
                    followTargetActorId,
                    in followOffset,
                    in position,
                    in rotation,
                    durationMsOverride,
                    out entity))
            {
                return false;
            }

            ApplyPresentationScale(entity, scale, radius);
            ApplyCueState(entity, stackCount, maxStackCount, elapsedSeconds, remainingSeconds);
            return true;
        }

        public bool TrySpawn(
            in PresentationRequest request,
            EC.IEntityId followTarget,
            out EC.IEntity entity)
        {
            entity = default;
            if (!CanSpawn || !_vfx.TryCreatePresentationVfxEntity(
                    _world,
                    _vfxNode,
                    in request,
                    followTarget,
                    out entity)) return false;

            ApplyPresentationScale(entity, request.Scale, request.Radius);
            ApplyCueState(
                entity,
                ReadInt(request.Parameters, "stackCount"),
                ReadInt(request.Parameters, "maxStackCount"),
                ReadFloat(request.Parameters, "elapsedSeconds"),
                ReadFloat(request.Parameters, "remainingSeconds"));
            return true;
        }

        public bool TrySpawn(
            int vfxId,
            in Vector3 position,
            EC.IEntityId followTarget,
            int durationMsOverride,
            float scale,
            float radius,
            out EC.IEntity entity)
        {
            var rotation = Quaternion.identity;
            var followOffset = Vector3.zero;
            return TrySpawn(
                vfxId,
                in position,
                followTarget,
                in rotation,
                in followOffset,
                0,
                durationMsOverride,
                scale,
                radius,
                0,
                0,
                0f,
                0f,
                out entity);
        }

        public void Destroy(EC.IEntityId id)
        {
            if (_world == null || id == default) return;
            _vfx.DestroyVfxEntity(_world, id);
        }

        public void Update(
            EC.IEntityId id,
            float scale,
            float radius,
            int durationMsOverride,
            int stackCount,
            int maxStackCount,
            float elapsedSeconds,
            float remainingSeconds)
        {
            if (!id.IsValid) return;

            ApplyPresentationScale(id, scale, radius);
            RefreshLifetime(id, durationMsOverride);
            ApplyCueState(id, stackCount, maxStackCount, elapsedSeconds, remainingSeconds);
        }

        private static int ReadInt(IReadOnlyList<PresentationParameter> parameters, string key)
        {
            return (int)ReadFloat(parameters, key);
        }

        private static float ReadFloat(IReadOnlyList<PresentationParameter> parameters, string key)
        {
            if (parameters == null) return 0f;
            for (var i = 0; i < parameters.Count; i++)
                if (parameters[i].Key == key) return parameters[i].Value;
            return 0f;
        }

        private void ApplyCueState(
            EC.IEntity entity,
            int stackCount,
            int maxStackCount,
            float elapsedSeconds,
            float remainingSeconds)
        {
            if (!entity.IsValid ||
                !entity.TryGetRef(out BattleViewGameObjectComponent goComponent) ||
                goComponent == null || goComponent.GameObject == null)
                return;

            var metadata = goComponent.GameObject.GetComponent<BattleVfxCueMetadata>();
            if (metadata == null) return;
            metadata.Apply(stackCount, maxStackCount, elapsedSeconds, remainingSeconds);
        }

        private void ApplyCueState(
            EC.IEntityId id,
            int stackCount,
            int maxStackCount,
            float elapsedSeconds,
            float remainingSeconds)
        {
            if (!id.IsValid || _world == null || !_world.IsAlive(id)) return;
            ApplyCueState(_world.Wrap(id), stackCount, maxStackCount, elapsedSeconds, remainingSeconds);
        }

        private static void ApplyPresentationScale(
            EC.IEntity entity,
            float scale,
            float radius)
        {
            if (!entity.IsValid) return;
            if (!entity.TryGetRef(out BattleViewGameObjectComponent goComp) ||
                goComp == null ||
                goComp.GameObject == null)
            {
                return;
            }

            var resolvedScale = scale > 0f ? scale : 1f;
            var radiusScale = radius > 0f ? radius : 1f;
            goComp.GameObject.transform.localScale =
                Vector3.one * resolvedScale * radiusScale;
        }

        private void RefreshLifetime(EC.IEntityId id, int durationMsOverride)
        {
            if (durationMsOverride <= 0) return;
            if (_world == null || !_world.IsAlive(id)) return;

            var entity = _world.Wrap(id);
            if (!entity.TryGetRef(out BattleVfxLifetimeComponent lifetime) ||
                lifetime == null)
            {
                return;
            }

            lifetime.ExpireAtTime = Time.time + (durationMsOverride / 1000f);
        }

        private void ApplyPresentationScale(
            EC.IEntityId id,
            float scale,
            float radius)
        {
            if (!id.IsValid || _world == null || !_world.IsAlive(id)) return;

            var entity = _world.Wrap(id);
            ApplyPresentationScale(entity, scale, radius);
        }
    }

    internal sealed class BattleVfxPresentationBackend : IPresentationBackend<EC.IEntityId>
    {
        private readonly BattlePresentationCueVfxSpawner _spawner;
        private readonly System.Func<int, EC.IEntityId> _resolveTarget;

        public BattleVfxPresentationBackend(
            BattlePresentationCueVfxSpawner spawner,
            System.Func<int, EC.IEntityId> resolveTarget)
        {
            _spawner = spawner;
            _resolveTarget = resolveTarget;
        }

        public bool IsAlive(EC.IEntityId handle) => _spawner != null && _spawner.IsAlive(handle);
        public bool CanPlay => _spawner != null && _spawner.CanSpawn;

        public bool TryPlay(in PresentationRequest request, out EC.IEntityId handle)
        {
            var target = request.Attachment.ActorId > 0
                ? _resolveTarget?.Invoke(request.Attachment.ActorId) ?? default
                : default;
            return TryPlay(in request, target, out handle);
        }

        public bool TryPlay(
            in PresentationRequest request,
            EC.IEntityId directFollowTarget,
            out EC.IEntityId handle)
        {
            handle = default;
            if (!request.IsValid) return false;
            if (!_spawner.TrySpawn(
                    in request,
                    directFollowTarget,
                    out var entity)) return false;
            handle = entity.Id;
            return true;
        }

        public Task<PresentationHandle<EC.IEntityId>> PlayAsync(
            in PresentationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(TryPlay(in request, out var handle)
                ? new PresentationHandle<EC.IEntityId>(handle, stop: StopAsync)
                : null);
        }

        public Task<PresentationHandle<EC.IEntityId>> PlayAsync(
            in PresentationRequest request,
            EC.IEntityId directFollowTarget,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(TryPlay(in request, directFollowTarget, out var handle)
                ? new PresentationHandle<EC.IEntityId>(handle, stop: StopAsync)
                : null);
        }

        public void Update(EC.IEntityId handle, in PresentationRequest request)
        {
            _spawner?.Update(
                handle,
                request.Scale,
                request.Radius,
                request.DurationMs,
                ReadInt(request.Parameters, "stackCount"),
                ReadInt(request.Parameters, "maxStackCount"),
                ReadFloat(request.Parameters, "elapsedSeconds"),
                ReadFloat(request.Parameters, "remainingSeconds"));
        }

        public void Stop(EC.IEntityId handle) => _spawner?.Destroy(handle);

        public Task StopAsync(EC.IEntityId handle)
        {
            Stop(handle);
            return Task.CompletedTask;
        }

        private static int ReadInt(IReadOnlyList<PresentationParameter> parameters, string key)
        {
            return (int)ReadFloat(parameters, key);
        }

        private static float ReadFloat(IReadOnlyList<PresentationParameter> parameters, string key)
        {
            if (parameters == null) return 0f;
            for (var i = 0; i < parameters.Count; i++)
                if (parameters[i].Key == key) return parameters[i].Value;
            return 0f;
        }
    }

    internal sealed class BattleVfxPresentationService
    {
        private readonly BattleVfxPresentationBackend _backend;
        private readonly PresentationSession<EC.IEntityId> _session;

        public BattleVfxPresentationService(
            EC.IECWorld world,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode,
            System.Func<int, EC.IEntityId> resolveTarget = null)
        {
            _backend = new BattleVfxPresentationBackend(
                new BattlePresentationCueVfxSpawner(world, vfx, in vfxNode),
                resolveTarget);
            _session = new PresentationSession<EC.IEntityId>(_backend);
        }

        public bool TryPlay(in PresentationRequest request, EC.IEntityId followTarget, out EC.IEntityId handle)
        {
            return _backend.TryPlay(in request, followTarget, out handle);
        }

        public Task<PresentationHandle<EC.IEntityId>> PlayAsync(
            in PresentationRequest request,
            CancellationToken cancellationToken = default)
        {
            return _session.PlayAsync(request, cancellationToken: cancellationToken);
        }

        public Task<PresentationHandle<EC.IEntityId>> PlayAsync(
            in PresentationRequest request,
            EC.IEntityId followTarget,
            CancellationToken cancellationToken = default)
        {
            return _backend.PlayAsync(in request, followTarget, cancellationToken);
        }

        public Task<PresentationHandle<EC.IEntityId>> PlayTransientAsync(
            in PresentationRequest request,
            CancellationToken cancellationToken = default)
        {
            return _session.EmitTransientAsync(request, cancellationToken);
        }

        public Task StopAsync(string logicalKey) => _session.StopAsync(logicalKey);
        public Task ClearAsync() => _session.ClearAsync();

        public bool TryPlayTransient(
            string logicalKey,
            int vfxId,
            in Vector3 position,
            int durationMs,
            out EC.IEntityId handle)
        {
            var request = new PresentationRequest(
                logicalKey,
                vfxId,
                new PresentationVector3(position.x, position.y, position.z),
                durationMs: durationMs);
            return _backend.TryPlay(in request, out handle);
        }

        public void PlayTransient(
            string logicalKey,
            int vfxId,
            in Vector3 position,
            int durationMs)
        {
            var request = new PresentationRequest(
                logicalKey,
                vfxId,
                new PresentationVector3(position.x, position.y, position.z),
                durationMs: durationMs);
            _ = PlayTransientSafelyAsync(request);
        }

        private async Task PlayTransientSafelyAsync(PresentationRequest request)
        {
            try
            {
                var handle = await PlayTransientAsync(request).ConfigureAwait(false);
                if (handle == null) return;
                try { await handle.Ready.ConfigureAwait(false); }
                catch { await handle.StopAsync().ConfigureAwait(false); }
            }
            catch (System.OperationCanceledException)
            {
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
