using AbilityKit.Game.Flow.Battle.View;
using AbilityKit.Game.Flow.Modules;
using AbilityKit.World.ECS;
using System;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleViewFeature
    {
        private static void ApplyInterpolationSettingsIfAny(FeatureModuleContext<BattleViewFeature> ctx, BattleViewBinder binder)
        {
            if (binder == null) return;

            var flow = ctx.Phase.Entry != null ? ctx.Phase.Entry.Get<GameFlowDomain>() : null;
            var settings = flow?.Settings;
            if (settings == null) return;

            if (settings.TryGetBool("View.Interp.Enabled", out var enabled)) binder.InterpolationEnabled = enabled;
            if (settings.TryGetFloat("View.Interp.BackTimeTicks", out var backTicks)) binder.BackTimeTicks = backTicks;
            if (settings.TryGetFloat("View.Interp.MaxLagTicks", out var maxLagTicks)) binder.MaxLagTicks = maxLagTicks;
        }

        private IDisposable _entityDestroyedSub;

        private sealed class BindingSubFeature : IViewSubFeature<BattleViewFeature>
        {
            public void OnAttach(in FeatureModuleContext<BattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._binder?.Clear();
                f._binder = new BattleViewBinder(f._vfx, f._vfxNode);
                ApplyInterpolationSettingsIfAny(ctx, f._binder);

                f._entityDestroyedSub?.Dispose();
                if (f._ctx?.EntityWorld != null)
                {
                    f._entityDestroyedSub = f._ctx.EntityWorld.EntityDestroyed(f.OnEntityDestroyed);
                }
            }

            public void OnDetach(in FeatureModuleContext<BattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._entityDestroyedSub?.Dispose();
                f._entityDestroyedSub = null;

                f._binder?.Clear();
                f._binder = null;
            }

            public void Tick(in FeatureModuleContext<BattleViewFeature> ctx, float deltaTime)
            {
                var f = ctx.Feature;
                if (f == null) return;
                if (f._binder == null) return;
                ApplyInterpolationSettingsIfAny(ctx, f._binder);
            }

            public void RebindAll(in FeatureModuleContext<BattleViewFeature> ctx) { }
        }
    }

    public sealed partial class ConfirmedBattleViewFeature
    {
        private static void ApplyInterpolationSettingsIfAny(FeatureModuleContext<ConfirmedBattleViewFeature> ctx, BattleViewBinder binder)
        {
            if (binder == null) return;

            var flow = ctx.Phase.Entry != null ? ctx.Phase.Entry.Get<GameFlowDomain>() : null;
            var settings = flow?.Settings;
            if (settings == null) return;

            if (settings.TryGetBool("View.Interp.Enabled", out var enabled)) binder.InterpolationEnabled = enabled;
            if (settings.TryGetFloat("View.Interp.BackTimeTicks", out var backTicks)) binder.BackTimeTicks = backTicks;
            if (settings.TryGetFloat("View.Interp.MaxLagTicks", out var maxLagTicks)) binder.MaxLagTicks = maxLagTicks;
        }

        private IDisposable _entityDestroyedSub;

        private sealed class BindingSubFeature : IViewSubFeature<ConfirmedBattleViewFeature>
        {
            public void OnAttach(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._binder?.Clear();
                f._binder = new BattleViewBinder(f._vfx, f._vfxNode);
                ApplyInterpolationSettingsIfAny(ctx, f._binder);

                f._entityDestroyedSub?.Dispose();
                if (f._confirmedCtx?.EntityWorld != null)
                {
                    f._entityDestroyedSub = f._confirmedCtx.EntityWorld.EntityDestroyed(f.OnEntityDestroyed);
                }
            }

            public void OnDetach(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._entityDestroyedSub?.Dispose();
                f._entityDestroyedSub = null;

                f._binder?.Clear();
                f._binder = null;
            }

            public void Tick(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx, float deltaTime)
            {
                var f = ctx.Feature;
                if (f == null) return;
                if (f._binder == null) return;
                ApplyInterpolationSettingsIfAny(ctx, f._binder);
            }

            public void RebindAll(in FeatureModuleContext<ConfirmedBattleViewFeature> ctx) { }
        }
    }
}
