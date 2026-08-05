using UnityEngine;
using EC = global::AbilityKit.World.ECS;

namespace AbilityKit.Game.Battle.Component
{
    public sealed class BattleViewFollowComponent
    {
        public EC.IEntityId Target;
        public int TargetActorId;
        public Vector3 Offset;
    }
}
