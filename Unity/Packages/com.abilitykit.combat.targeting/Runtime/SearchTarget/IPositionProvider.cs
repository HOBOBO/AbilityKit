using UnityEngine;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget
{
    public interface IPositionProvider
    {
        bool TryGetPositionXZ(EcsEntityId id, out Vector2 positionXZ);
    }
}
