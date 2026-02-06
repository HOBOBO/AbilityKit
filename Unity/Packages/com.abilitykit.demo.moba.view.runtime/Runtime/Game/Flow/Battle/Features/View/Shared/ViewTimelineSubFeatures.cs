using AbilityKit.Game.Flow.Battle.View;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleViewFeature
    {
        private sealed class TimelineSubFeature : IViewSubFeature<BattleViewFeature>
        {
            public void OnAttach(in FeatureModuleContext<BattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._timeline?.Clear();
                f._timeline = new ViewTimeline();
            }

            public void OnDetach(in FeatureModuleContext<BattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._timeline?.Clear();
                f._timeline = null;
            }

            public void Tick(in FeatureModuleContext<BattleViewFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleViewFeature> ctx) { }
        }
    }

    public sealed partial class ConfirmedBattleViewFeature
    {
        private sealed class TimelineSubFeature : IViewSubFeature<ConfirmedBattleViewFeature>
        {
            public void OnAttach(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._timeline?.Clear();
                f._timeline = new ViewTimeline();
            }

            public void OnDetach(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._timeline?.Clear();
                f._timeline = null;
            }

            public void Tick(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx) { }
        }
    }
}
