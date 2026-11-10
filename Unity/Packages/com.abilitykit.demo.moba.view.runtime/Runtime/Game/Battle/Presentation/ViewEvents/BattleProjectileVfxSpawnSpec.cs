using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal readonly struct BattleProjectileVfxSpawnSpec
    {
        public BattleProjectileVfxSpawnSpec(int vfxId, in Vector3 position, EC.IEntityId followTarget)
            : this(vfxId, in position, followTarget, 0, Quaternion.identity, false)
        {
        }

        public BattleProjectileVfxSpawnSpec(int vfxId, in Vector3 position, EC.IEntityId followTarget, in Quaternion rotation)
            : this(vfxId, in position, followTarget, 0, in rotation, false)
        {
        }

        public BattleProjectileVfxSpawnSpec(int vfxId, in Vector3 position, EC.IEntityId followTarget, int followTargetActorId, in Quaternion rotation)
            : this(vfxId, in position, followTarget, followTargetActorId, in rotation, false)
        {
        }

        public BattleProjectileVfxSpawnSpec(int vfxId, in Vector3 position, EC.IEntityId followTarget, int followTargetActorId, in Quaternion rotation, bool playProjectileSpawn)
        {
            VfxId = vfxId;
            Position = position;
            FollowTarget = followTarget;
            FollowTargetActorId = followTargetActorId;
            Rotation = rotation;
            PlayProjectileSpawn = playProjectileSpawn;
        }

        public int VfxId { get; }

        public Vector3 Position { get; }

        public EC.IEntityId FollowTarget { get; }

        public int FollowTargetActorId { get; }

        public Quaternion Rotation { get; }

        public bool PlayProjectileSpawn { get; }

        public bool IsValid => VfxId > 0;
    }
}
