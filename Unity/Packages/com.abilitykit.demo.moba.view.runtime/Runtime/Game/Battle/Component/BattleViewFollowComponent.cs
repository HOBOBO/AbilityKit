using UnityEngine;
using EC = global::AbilityKit.Ability.EC;

namespace AbilityKit.Game.Battle.Component
{
    public sealed class BattleViewFollowComponent
    {
        public EC.EntityId Target;
        public Vector3 Offset;
    }
}
