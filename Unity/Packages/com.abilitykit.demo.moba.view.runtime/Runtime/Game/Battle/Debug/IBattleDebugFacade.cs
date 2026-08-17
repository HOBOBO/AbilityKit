using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.ECS;

namespace AbilityKit.Game.Battle
{
    public interface IBattleDebugFacade
    {
        bool TryGetSession(out BattleLogicSession session);

        bool TryListEntities(
            out IReadOnlyList<BattleDebugEntityId> ids);

        bool TryResolveUnit(
            BattleDebugEntityId id,
            out IUnitFacade unit);
    }
}
