using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Flow
{
    internal static class ConfirmedViewSideInstaller
    {
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
