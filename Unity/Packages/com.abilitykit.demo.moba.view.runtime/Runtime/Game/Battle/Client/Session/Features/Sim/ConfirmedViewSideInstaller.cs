using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Flow
{
    internal static class ConfirmedViewSideInstaller
    {
        internal static bool ShouldRenderConfirmedView(BattleStartPlan plan)
        {
            // The primary BattleViewFeature is the sole scene renderer. The confirmed
            // world remains a reconciliation/debug data source and must not own a
            // second camera, VFX stack, or hierarchy in the normal runtime pipeline.
            return false;
        }

        public static void EnsureInstalled(
            BattleContext ctx,
            GameFlowDomain flow,
            BattleSessionConfirmedWorldRuntime handles,
            WorldId authWorldId,
            bool enabled)
        {
            if (ShouldInstall(flow, handles, enabled))
            {
                var viewSide = ConfirmedViewSideRuntimeFactory.Create(ctx, authWorldId);
                handles.BindViewSideRuntime(viewSide);
                AttachFeature(flow, viewSide.Feature);
            }

            ConfirmedAuthorityDebugStatsPublisher.Initialize(authWorldId);
        }

        private static bool ShouldInstall(
            GameFlowDomain flow,
            BattleSessionConfirmedWorldRuntime handles,
            bool enabled)
        {
            return flow != null && handles != null && !handles.HasViewFeature() && enabled;
        }

        private static void AttachFeature(GameFlowDomain flow, ConfirmedBattleViewFeature feature)
        {
            if (flow == null || feature == null) return;

            flow.Attach(feature);
        }
    }
}
