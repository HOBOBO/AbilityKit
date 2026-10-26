using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewAttachedVfxController
    {
        private readonly BattleViewProjectileAttachedVfxResolver _projectileVfx;
        private readonly BattleViewAttachedVfxLifecycle _lifecycle;
        private readonly Dictionary<BattleViewHandle, PendingVfx> _pending =
            new Dictionary<BattleViewHandle, PendingVfx>();

        private sealed class PendingVfx
        {
            public int VfxId;
            public CancellationTokenSource Cancellation;
        }

        public BattleViewAttachedVfxController(
            BattleVfxManager vfx,
            in EC.IEntity vfxNode,
            BattleViewResourceProvider resources = null,
            BattleViewAttachedVfxControllerFactory controllers = null)
        {
            controllers ??= new BattleViewAttachedVfxControllerFactory();

            _projectileVfx = controllers.CreateProjectileVfxResolver(resources);
            _lifecycle = controllers.CreateLifecycle(vfx, in vfxNode);
        }

        public void SyncProjectileVfx(EC.IEntity entity, BattleViewHandle handle, BattleEntityMetaComponent meta)
        {
            if (!_lifecycle.IsAvailable) return;
            if (handle == null) return;

            var plan = _projectileVfx.Resolve(meta);
            if (!plan.HasVfx)
            {
                CancelPending(handle);
                _lifecycle.Destroy(entity.World, handle);
                return;
            }

            if (plan.IsSatisfiedBy(handle))
            {
                return;
            }

            if (_pending.TryGetValue(handle, out var pending))
            {
                if (pending.VfxId == plan.DesiredVfxId) return;
                pending.Cancellation.Cancel();
                pending.Cancellation.Dispose();
                _pending.Remove(handle);
            }

            _lifecycle.Destroy(entity.World, handle);
            var cancellation = new CancellationTokenSource();
            _pending[handle] = new PendingVfx
            {
                VfxId = plan.DesiredVfxId,
                Cancellation = cancellation,
            };
            _ = CreatePendingAsync(entity.World, entity.Id, handle, plan.DesiredVfxId, cancellation);
        }

        public void SyncPosition(BattleViewHandle handle, in Vector3 pos)
        {
            _lifecycle.SyncPosition(handle, in pos);
        }

        public void Destroy(BattleViewHandle handle)
        {
            CancelPending(handle);
            _lifecycle.Destroy(handle);
        }

        public void Destroy(EC.IECWorld world, BattleViewHandle handle)
        {
            CancelPending(handle);
            _lifecycle.Destroy(world, handle);
        }

        private async Task CreatePendingAsync(
            EC.IECWorld world,
            EC.IEntityId target,
            BattleViewHandle handle,
            int vfxId,
            CancellationTokenSource cancellation)
        {
            try
            {
                await _lifecycle.TryCreateFollowVfxAsync(
                    world,
                    target,
                    handle,
                    vfxId,
                    cancellation.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (_pending.TryGetValue(handle, out var pending) &&
                    ReferenceEquals(pending.Cancellation, cancellation))
                {
                    _pending.Remove(handle);
                }
                cancellation.Dispose();
            }
        }

        private void CancelPending(BattleViewHandle handle)
        {
            if (handle == null || !_pending.TryGetValue(handle, out var pending)) return;
            _pending.Remove(handle);
            pending.Cancellation.Cancel();
            pending.Cancellation.Dispose();
        }
    }

    internal sealed class BattleViewAttachedVfxControllerFactory
    {
        public BattleViewProjectileAttachedVfxResolver CreateProjectileVfxResolver(BattleViewResourceProvider resources)
        {
            return new BattleViewProjectileAttachedVfxResolver(resources);
        }

        public BattleViewAttachedVfxLifecycle CreateLifecycle(BattleVfxManager vfx, in EC.IEntity vfxNode)
        {
            return new BattleViewAttachedVfxLifecycle(vfx, in vfxNode);
        }
    }
}
