using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleViewFeature
    {
        private sealed class ContextBindingSubFeature : IViewSubFeature<BattleViewFeature>
        {
            public void OnAttach(in FeatureModuleContext<BattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._query = f._ctx != null ? f._ctx.EntityQuery : null;
                f._events = f._ctx != null ? f._ctx.Events : null;
            }

            public void OnDetach(in FeatureModuleContext<BattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._events = null;
                f._query = null;
            }

            public void Tick(in FeatureModuleContext<BattleViewFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleViewFeature> ctx) { }
        }
    }

    public sealed partial class ConfirmedBattleViewFeature
    {
        private sealed class ContextBindingSubFeature : IViewSubFeature<ConfirmedBattleViewFeature>
        {
            public void OnAttach(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._query = f._confirmedCtx != null ? f._confirmedCtx.EntityQuery : null;
                f._events = f._confirmedCtx != null ? f._confirmedCtx.Events : null;
            }

            public void OnDetach(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._events = null;
                f._query = null;
            }

            public void Tick(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx) { }
        }
    }
}
