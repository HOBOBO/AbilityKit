using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Editor
{
    internal static class BattleDebugEntityFilter
    {
        public static bool Matches(
            AbilityKit.Demo.Moba.Diagnostics.IBattleDiagnosticReadOnlySession session,
            BattleDebugEntityId id,
            string filter)
        {
            return BattleDebugEntityFilterImpl.Matches(session, id, filter);
        }
    }
}
