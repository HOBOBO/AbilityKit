using UnityEngine;
using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget
{
    public interface IPositionProvider
    {
        bool TryGetPositionXZ(EcsEntityId id, out Vector2 positionXZ);
    }
}
