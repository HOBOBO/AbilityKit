using AbilityKit.Game.Battle.Vfx;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal sealed class BattleProjectileVfxSpawner
    {
        private readonly EC.IECWorld _world;
        private readonly BattleVfxManager _vfx;
        private readonly EC.IEntity _vfxNode;

        public BattleProjectileVfxSpawner(EC.IECWorld world, BattleVfxManager vfx, in EC.IEntity vfxNode)
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

        public bool TrySpawn(in BattleProjectileVfxSpawnSpec spec)
        {
            if (!CanSpawn) return false;
            if (!spec.IsValid) return false;

            var position = spec.Position;
            var rotation = spec.Rotation;
            return _vfx.TryCreateVfxEntity(
                _world,
                _vfxNode,
                spec.VfxId,
                spec.FollowTarget,
                spec.FollowTargetActorId,
                in position,
                in rotation,
                out _);
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
