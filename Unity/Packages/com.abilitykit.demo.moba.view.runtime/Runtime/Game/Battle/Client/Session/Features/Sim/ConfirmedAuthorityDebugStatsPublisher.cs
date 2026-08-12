using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Flow.Battle.ViewEvents;

namespace AbilityKit.Game.Flow
{
    internal static class ConfirmedAuthorityDebugStatsPublisher
    {
        public static void Initialize(
            BattleSessionDiagnostics diagnostics,
            WorldId authWorldId)
        {
            diagnostics?.InitializeConfirmedAuthority(authWorldId.Value);
        }

        public static void Update(
            BattleSessionDiagnostics diagnostics,
            int confirmedFrame,
            int predictedFrame,
            int inputTargetFrame,
            int driveTargetFrame,
            int lastTickedFrame,
            DebugBattleViewEventSink viewEventSink)
        {
            diagnostics?.UpdateConfirmedAuthority(
                confirmedFrame,
                predictedFrame,
                inputTargetFrame,
                driveTargetFrame,
                lastTickedFrame,
                viewEventSink?.Total ?? 0,
                viewEventSink?.GetRecentLines());
        }

        public static void Clear(
            BattleSessionDiagnostics diagnostics,
            BattleContext ctx)
        {
            diagnostics?.ClearConfirmedAuthority();

            if (ctx != null)
            {
                ctx.PredictionStats = null;
            }
        }
    }
}
