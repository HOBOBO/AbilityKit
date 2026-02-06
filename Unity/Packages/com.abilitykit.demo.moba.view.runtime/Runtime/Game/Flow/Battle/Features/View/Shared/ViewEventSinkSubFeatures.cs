using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleViewFeature
    {
        private sealed class EventSinkSubFeature : IViewSubFeature<BattleViewFeature>
        {
            public void OnAttach(in FeatureModuleContext<BattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._eventSink = new BattleViewEventSink(
                    f._ctx,
                    f._query,
                    f._binder,
                    f._vfx,
                    f._vfxNode,
                    f._floatingTexts,
                    f._areaViews);
            }

            public void OnDetach(in FeatureModuleContext<BattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;
                f._eventSink = null;
            }

            public void Tick(in FeatureModuleContext<BattleViewFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleViewFeature> ctx) { }
        }
    }

    public sealed partial class ConfirmedBattleViewFeature
    {
        private sealed class EventSinkSubFeature : IViewSubFeature<ConfirmedBattleViewFeature>
        {
            public void OnAttach(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._eventSink = new BattleViewEventSink(
                    f._confirmedCtx,
                    f._query,
                    f._binder,
                    f._vfx,
                    f._vfxNode,
                    f._floatingTexts,
                    f._areaViews);
            }

            public void OnDetach(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;
                f._eventSink = null;
            }

            public void Tick(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx) { }
        }
    }
}
