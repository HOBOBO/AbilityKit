namespace AbilityKit.Game.Flow
{
    internal sealed class ProjectedBattleViewFeature : ConfirmedBattleViewFeature
    {
        internal ProjectedBattleViewFeature(BattleContext context, string viewRole,
            BattleProjectionViewCapabilities capabilities = BattleProjectionViewCapabilities.Actors)
            : base(context, viewRole, isPredictionView: true, capabilities: capabilities)
        {
        }

        internal AbilityKit.Game.Flow.Battle.ViewEvents.IBattleViewEventSink EventSink =>
            ((IViewFeatureRuntime)this).EventSink;

        internal void ClearTransientPresentation()
        {
            var runtime = (IViewFeatureRuntime)this;
            runtime.EventSink?.Clear();
            var vfxNode = runtime.VfxNode;
            runtime.Vfx?.Clear(in vfxNode);
            runtime.AreaViews?.Clear();
            runtime.FloatingTexts?.Clear();
            runtime.Timeline?.Clear();
        }
    }
}
