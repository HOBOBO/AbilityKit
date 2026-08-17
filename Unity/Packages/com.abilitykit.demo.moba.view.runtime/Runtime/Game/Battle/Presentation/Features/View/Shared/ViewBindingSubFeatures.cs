using AbilityKit.Game.Flow.Battle.View;
using AbilityKit.Game.Flow.Modules;
using AbilityKit.World.ECS;
using System;

namespace AbilityKit.Game.Flow
{
    internal sealed class ViewBindingSubFeature<TFeature> : IViewSubFeature<TFeature>
        where TFeature : class, IViewFeatureRuntime
    {
        private readonly ViewBindingControllerFactory _controllers;
        private readonly ViewInterpolationSettingsApplier _interpolationSettings;

        public ViewBindingSubFeature(
            ViewBindingControllerFactory controllers = null,
            ViewInterpolationSettingsApplier interpolationSettings = null)
        {
            _controllers = controllers ?? new ViewBindingControllerFactory();
            _interpolationSettings = interpolationSettings ?? new ViewInterpolationSettingsApplier();
        }

        public void OnAttach(in FeatureModuleContext<TFeature> ctx)
        {
            var runtime = ctx.Feature;
            if (runtime == null) return;

            runtime.Binder?.Clear();
            runtime.Binder = _controllers.CreateBinder(runtime);
            _interpolationSettings.Apply(ctx.Phase, runtime.Binder);

            runtime.EntityDestroyedSubscription?.Dispose();
            var world = runtime.EntityContext?.EntityWorld;
            if (world != null)
            {
                runtime.EntityDestroyedSubscription = world.EntityDestroyed(runtime.OnEntityDestroyed);
            }
        }

        public void OnDetach(in FeatureModuleContext<TFeature> ctx)
        {
            var runtime = ctx.Feature;
            if (runtime == null) return;

            runtime.EntityDestroyedSubscription?.Dispose();
            runtime.EntityDestroyedSubscription = null;

            runtime.Binder?.Clear();
            runtime.Binder = null;
        }

        public void Tick(in FeatureModuleContext<TFeature> ctx, float deltaTime)
        {
            var runtime = ctx.Feature;
            if (runtime == null) return;
            if (runtime.Binder == null) return;
            _interpolationSettings.Apply(ctx.Phase, runtime.Binder);
        }

        public void RebindAll(in FeatureModuleContext<TFeature> ctx) { }
    }

    internal sealed class ViewBindingControllerFactory
    {
        public BattleViewBinder CreateBinder(IViewFeatureRuntime runtime)
        {
            return new BattleViewBinder(
                runtime.Vfx,
                runtime.VfxNode,
                resources: runtime.Resources,
                pool: runtime.ShellPool,
                controllers: null,
                hierarchy: runtime.Hierarchy);
        }
    }

    internal sealed class ViewInterpolationSettingsApplier
    {
        public void Apply(in GamePhaseContext phase, BattleViewBinder binder)
        {
            if (binder == null) return;

            var flow = phase.Entry != null ? phase.Entry.Get<GameFlowDomain>() : null;
            var settings = flow?.Settings;
            if (settings == null) return;

            if (settings.TryGetBool("View.Interp.Enabled", out var enabled)) binder.InterpolationEnabled = enabled;
            if (settings.TryGetFloat("View.Interp.BackTimeTicks", out var backTicks)) binder.BackTimeTicks = backTicks;
            if (settings.TryGetFloat("View.Interp.MaxLagTicks", out var maxLagTicks)) binder.MaxLagTicks = maxLagTicks;
            if (settings.TryGetFloat("View.Interp.SmoothingHz", out var smoothingHz)) binder.SmoothingHz = smoothingHz;
        }
    }
}
