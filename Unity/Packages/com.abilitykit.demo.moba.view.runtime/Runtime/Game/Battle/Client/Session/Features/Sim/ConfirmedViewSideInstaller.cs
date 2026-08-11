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
    }
}
