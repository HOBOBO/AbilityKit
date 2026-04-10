using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.ECS;

namespace AbilityKit.Game.Battle
{
    public interface IBattleDebugFacade
    {
        bool TryGetSession(out BattleLogicSession session);

        bool TryListEntities(out IReadOnlyList<EcsEntityId> ids);

        bool TryResolveUnit(EcsEntityId id, out IUnitFacade unit);
    }
}
