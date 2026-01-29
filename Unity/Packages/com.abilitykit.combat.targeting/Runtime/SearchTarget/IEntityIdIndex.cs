using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget
{
    public interface IEntityIdIndex
    {
        bool TryGetList(int key, out IReadOnlyList<EcsEntityId> ids);
    }
}
