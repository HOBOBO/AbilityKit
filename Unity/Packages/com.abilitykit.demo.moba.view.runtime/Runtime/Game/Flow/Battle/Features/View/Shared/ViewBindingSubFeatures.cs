using AbilityKit.Game.Flow.Battle.View;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleViewFeature
    {
        private sealed class BindingSubFeature : IViewSubFeature<BattleViewFeature>
        {
            public void OnAttach(in FeatureModuleContext<BattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._binder?.Clear();
                f._binder = new BattleViewBinder(f._vfx, f._vfxNode);

                if (f._ctx?.EntityWorld != null)
                {
                    f._ctx.EntityWorld.EntityDestroyed += f.OnEntityDestroyed;
                }
            }

            public void OnDetach(in FeatureModuleContext<BattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                if (f._ctx?.EntityWorld != null)
                {
                    f._ctx.EntityWorld.EntityDestroyed -= f.OnEntityDestroyed;
                }

                f._binder?.Clear();
                f._binder = null;
            }

            public void Tick(in FeatureModuleContext<BattleViewFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleViewFeature> ctx) { }
        }
    }

    public sealed partial class ConfirmedBattleViewFeature
    {
        private sealed class BindingSubFeature : IViewSubFeature<ConfirmedBattleViewFeature>
        {
            public void OnAttach(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._binder?.Clear();
                f._binder = new BattleViewBinder(f._vfx, f._vfxNode);

                if (f._confirmedCtx?.EntityWorld != null)
                {
                    f._confirmedCtx.EntityWorld.EntityDestroyed += f.OnEntityDestroyed;
                }
            }

            public void OnDetach(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                if (f._confirmedCtx?.EntityWorld != null)
                {
                    f._confirmedCtx.EntityWorld.EntityDestroyed -= f.OnEntityDestroyed;
                }

                f._binder?.Clear();
                f._binder = null;
            }

            public void Tick(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx) { }
        }
    }
}
