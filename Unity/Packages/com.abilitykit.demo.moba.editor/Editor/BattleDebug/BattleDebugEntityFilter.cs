using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Editor
{
    internal static class BattleDebugEntityFilter
    {
        public static bool Matches(
            IBattleDebugFacade facade,
            BattleDebugEntityId id,
            string filter)
        {
            return BattleDebugEntityFilterImpl.Matches(facade, id, filter);
        }
    }
}
