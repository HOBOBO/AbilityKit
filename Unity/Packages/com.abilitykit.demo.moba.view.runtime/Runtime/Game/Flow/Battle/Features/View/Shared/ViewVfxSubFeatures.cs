using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow.Battle.View;
using AbilityKit.Game.Flow.Modules;
using AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleViewFeature
    {
        private sealed class VfxSubFeature : IViewSubFeature<BattleViewFeature>
        {
            public void OnAttach(in FeatureModuleContext<BattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                if (BattleViewFactory.VfxDb == null) BattleViewFactory.VfxDb = VfxDatabase.LoadFromResources("vfx/vfx");
                f._vfx = new BattleVfxManager(BattleViewFactory.VfxDb);

                f._vfxNode = default;
                if (f._ctx != null && f._ctx.EntityNode.IsValid)
                {
                    f._vfxNode = f._ctx.EntityNode.World.CreateChild(f._ctx.EntityNode);
                    f._vfxNode.SetName("BattleVfx");
                }
            }

            public void OnDetach(in FeatureModuleContext<BattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._vfx = null;
                f._vfxNode = default;
            }

            public void Tick(in FeatureModuleContext<BattleViewFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleViewFeature> ctx) { }
        }
    }

    public sealed partial class ConfirmedBattleViewFeature
    {
        private sealed class VfxSubFeature : IViewSubFeature<ConfirmedBattleViewFeature>
        {
            public void OnAttach(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                if (BattleViewFactory.VfxDb == null) BattleViewFactory.VfxDb = VfxDatabase.LoadFromResources("vfx/vfx");
                f._vfx = new BattleVfxManager(BattleViewFactory.VfxDb);

                f._vfxNode = default;
                if (f._confirmedCtx != null && f._confirmedCtx.EntityNode.IsValid)
                {
                    f._vfxNode = f._confirmedCtx.EntityNode.World.CreateChild(f._confirmedCtx.EntityNode);
                    f._vfxNode.SetName("BattleVfx_confirmed");
                }
            }

            public void OnDetach(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._vfx = null;
                f._vfxNode = default;
            }

            public void Tick(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx) { }
        }
    }
}
