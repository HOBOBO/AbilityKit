using AbilityKit.Ability.Share.ECS;
using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Editor
{
    internal static class BattleDebugEntityFilter
    {
        public static bool Matches(IBattleDebugFacade facade, EcsEntityId id, string filter)
        {
            return BattleDebugEntityFilterImpl.Matches(facade, id, filter);
        }
    }
}
