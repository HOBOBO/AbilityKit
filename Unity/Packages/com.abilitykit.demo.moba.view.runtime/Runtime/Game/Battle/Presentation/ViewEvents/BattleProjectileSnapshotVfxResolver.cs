using AbilityKit.Demo.Moba.Share;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal sealed class BattleProjectileSnapshotVfxResolver
    {
        private readonly BattleProjectileFollowTargetResolver _followTargets;
        private readonly BattleProjectileVfxResolver _vfx;
        private readonly BattleProjectileSnapshotPositionResolver _positions;
        private readonly BattleProjectileVfxSpawnSpecFactory _specs;

        public BattleProjectileSnapshotVfxResolver(
            BattleProjectileFollowTargetResolver followTargets,
            BattleViewResourceProvider resources = null,
            BattleProjectileVfxResolver vfx = null,
            BattleProjectileSnapshotPositionResolver positions = null,
            BattleProjectileVfxSpawnSpecFactory specs = null)
        {
            _followTargets = followTargets;
            _vfx = vfx ?? new BattleProjectileVfxResolver(resources);
            _positions = positions ?? new BattleProjectileSnapshotPositionResolver();
            _specs = specs ?? new BattleProjectileVfxSpawnSpecFactory();
        }

        public bool TryResolve(in ProjectileEventData entry, out BattleProjectileVfxSpawnSpec spec)
        {
            spec = default;

            var vfxId = _vfx.ResolveSnapshotVfxId(entry.TemplateId, entry.Kind);
            if (vfxId <= 0) return false;

            var position = _positions.Resolve(in entry);
            var followId = _followTargets != null
                ? _followTargets.Resolve(entry.ProjectileActorId)
                : default;
            var rotation = ResolveRotation(entry.ForwardX, entry.ForwardY, entry.ForwardZ);

            spec = _specs.Create(vfxId, in position, followId, entry.ProjectileActorId, in rotation);
            return true;
        }

        private static Quaternion ResolveRotation(float forwardX, float forwardY, float forwardZ)
        {
            var forward = new Vector3(forwardX, forwardY, forwardZ);
            if (forward.sqrMagnitude <= 0.0001f) return Quaternion.identity;
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }

    internal sealed class BattleProjectileSnapshotPositionResolver
    {
        public Vector3 Resolve(in ProjectileEventData entry)
        {
            return new BattleProjectileVfxResolver().ResolveSnapshotPosition(entry.X, entry.Y, entry.Z);
        }
    }

    internal sealed class BattleProjectileVfxSpawnSpecFactory
    {
        public BattleProjectileVfxSpawnSpec Create(int vfxId, in Vector3 position, EC.IEntityId followTarget)
        {
            return new BattleProjectileVfxSpawnSpec(vfxId, in position, followTarget);
        }

        public BattleProjectileVfxSpawnSpec Create(int vfxId, in Vector3 position, EC.IEntityId followTarget, in Quaternion rotation)
        {
            return new BattleProjectileVfxSpawnSpec(vfxId, in position, followTarget, in rotation);
        }

        public BattleProjectileVfxSpawnSpec Create(int vfxId, in Vector3 position, EC.IEntityId followTarget, int followTargetActorId, in Quaternion rotation)
        {
            return new BattleProjectileVfxSpawnSpec(vfxId, in position, followTarget, followTargetActorId, in rotation);
        }
    }
}
