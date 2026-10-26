using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewAttachedVfxLifecycle
    {
        private readonly BattleVfxManager _vfx;
        private readonly EC.IEntity _vfxNode;
        private readonly BattleVfxPresentationService _presentation;

        public BattleViewAttachedVfxLifecycle(BattleVfxManager vfx, in EC.IEntity vfxNode)
        {
            _vfx = vfx;
            _vfxNode = vfxNode;
            _presentation = new BattleVfxPresentationService(vfxNode.World, vfx, in vfxNode);
        }

        public bool IsAvailable => _vfx != null && _vfxNode.IsValid;

        public bool TryCreateFollowVfx(
            EC.IECWorld world,
            EC.IEntityId target,
            BattleViewHandle handle,
            int vfxId)
        {
            if (!IsAvailable) return false;
            if (handle == null) return false;
            if (vfxId <= 0) return false;

            var request = new PresentationRequest(
                "projectile:attached:" + handle.GetHashCode() + ":" + vfxId,
                vfxId,
                new PresentationVector3(handle.PendingPos.x, handle.PendingPos.y, handle.PendingPos.z),
                attachment: new PresentationAttachment(PresentationAttachmentKind.Projectile));
            if (_presentation.TryPlay(in request, target, out var vfxEntity))
            {
                handle.VfxId = vfxId;
                handle.VfxEntityId = vfxEntity;
                return true;
            }

            return false;
        }

        public async Task<bool> TryCreateFollowVfxAsync(
            EC.IECWorld world,
            EC.IEntityId target,
            BattleViewHandle handle,
            int vfxId,
            CancellationToken cancellationToken = default)
        {
            if (!IsAvailable || handle == null || vfxId <= 0) return false;

            var request = new PresentationRequest(
                "projectile:attached:" + handle.GetHashCode() + ":" + vfxId,
                vfxId,
                new PresentationVector3(handle.PendingPos.x, handle.PendingPos.y, handle.PendingPos.z),
                attachment: new PresentationAttachment(PresentationAttachmentKind.Projectile));
            var presentationHandle = await _presentation.PlayAsync(in request, target, cancellationToken);
            if (presentationHandle == null) return false;
            try
            {
                await presentationHandle.Ready;
            }
            catch
            {
                await presentationHandle.StopAsync();
                throw;
            }

            if (cancellationToken.IsCancellationRequested ||
                handle.Destroyed ||
                handle.VfxEntityId.Index != 0)
            {
                await presentationHandle.StopAsync();
                return false;
            }

            handle.VfxId = vfxId;
            handle.VfxEntityId = presentationHandle.BackendHandle;
            return true;
        }

        public void SyncPosition(BattleViewHandle handle, in Vector3 pos)
        {
            if (!IsAvailable) return;
            if (handle == null || handle.VfxEntityId.Index == 0) return;

            _vfx.SyncFollow(_vfxNode.World, handle.VfxEntityId, in pos);
        }

        public void Destroy(BattleViewHandle handle)
        {
            Destroy(_vfxNode.World, handle);
        }

        public void Destroy(EC.IECWorld world, BattleViewHandle handle)
        {
            if (_vfx == null) return;
            if (handle == null || handle.VfxEntityId.Index == 0) return;

            _vfx.DestroyVfxEntity(world, handle.VfxEntityId);
            handle.VfxId = 0;
            handle.VfxEntityId = default;
        }
    }
}
