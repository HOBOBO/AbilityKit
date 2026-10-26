using AbilityKit.Game.Battle.Vfx;
using System.Threading;
using System.Threading.Tasks;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal sealed class BattleProjectileVfxSpawner
    {
        private readonly EC.IECWorld _world;
        private readonly BattleVfxManager _vfx;
        private readonly EC.IEntity _vfxNode;
        private readonly BattleVfxPresentationService _presentation;

        public BattleProjectileVfxSpawner(EC.IECWorld world, BattleVfxManager vfx, in EC.IEntity vfxNode)
        {
            _world = world;
            _vfx = vfx;
            _vfxNode = vfxNode;
            _presentation = new BattleVfxPresentationService(world, vfx, in vfxNode);
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

        public bool TrySpawn(in BattleProjectileVfxSpawnSpec spec)
        {
            if (!CanSpawn) return false;
            if (!spec.IsValid) return false;

            var request = new PresentationRequest(
                "projectile:" + spec.FollowTargetActorId + ":" + spec.VfxId,
                spec.VfxId,
                new PresentationVector3(spec.Position.x, spec.Position.y, spec.Position.z),
                new PresentationRotation(spec.Rotation.x, spec.Rotation.y, spec.Rotation.z, spec.Rotation.w),
                spec.FollowTargetActorId > 0
                    ? new PresentationAttachment(PresentationAttachmentKind.Projectile, spec.FollowTargetActorId)
                    : default);
            return _presentation.TryPlay(in request, spec.FollowTarget, out _);
        }

        public int StopFollowingActor(int projectileActorId)
        {
            if (_vfx == null) return 0;
            if (!_vfxNode.IsValid) return 0;
            if (projectileActorId <= 0) return 0;

            return _vfx.DestroyVfxByFollowTargetActorId(_vfxNode, projectileActorId);
        }
    }
}
