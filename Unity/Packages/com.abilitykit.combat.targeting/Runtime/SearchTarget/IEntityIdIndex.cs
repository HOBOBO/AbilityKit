using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget
{
    public interface IEntityIdIndex
    {
        bool TryGetList(int key, out IReadOnlyList<EcsEntityId> ids);
    }
}
