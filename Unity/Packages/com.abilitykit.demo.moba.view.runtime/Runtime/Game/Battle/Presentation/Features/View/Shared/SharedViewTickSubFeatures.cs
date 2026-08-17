using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class SharedVfxTickSubFeature<TFeature> : IViewSubFeature<TFeature>
        where TFeature : class, IViewSharedSubFeatureHost
    {
        public void OnAttach(in FeatureModuleContext<TFeature> ctx) { }
        public void OnDetach(in FeatureModuleContext<TFeature> ctx) { }

        public void Tick(in FeatureModuleContext<TFeature> ctx, float deltaTime)
        {
            ctx.Feature?.TickVfx();
        }

        public void RebindAll(in FeatureModuleContext<TFeature> ctx) { }
    }

    internal sealed class SharedInterpolationSubFeature<TFeature> : IViewSubFeature<TFeature>
        where TFeature : class, IViewSharedSubFeatureHost
    {
        public void OnAttach(in FeatureModuleContext<TFeature> ctx) { }
        public void OnDetach(in FeatureModuleContext<TFeature> ctx) { }

        public void Tick(in FeatureModuleContext<TFeature> ctx, float deltaTime)
        {
            var f = ctx.Feature;
            var binder = f?.Binder;
            if (binder == null) return;

            binder.TickInterpolation(f.RuntimeContext, f.EntityContext, deltaTime);
        }

        public void RebindAll(in FeatureModuleContext<TFeature> ctx) { }
    }

    internal sealed class SharedFloatingTextSubFeature<TFeature> : IViewSubFeature<TFeature>
        where TFeature : class, IViewSharedSubFeatureHost
    {
        public void OnAttach(in FeatureModuleContext<TFeature> ctx) { }
        public void OnDetach(in FeatureModuleContext<TFeature> ctx) { }

        public void Tick(in FeatureModuleContext<TFeature> ctx, float deltaTime)
        {
            ctx.Feature?.TickFloatingTexts(deltaTime);
        }

        public void RebindAll(in FeatureModuleContext<TFeature> ctx) { }
    }

    /// <summary>
    /// Drives <c>IBattleViewEventSink.Tick()</c> each frame so that
    /// projectile shells update their follow-target positions.
    /// This is separate from <see cref="SharedVfxTickSubFeature{TFeature}"/>
    /// which calls <c>BattleVfxManager.Tick</c>.
    /// </summary>
    internal sealed class SharedProjectileTickSubFeature<TFeature> : IViewSubFeature<TFeature>
        where TFeature : class, IViewSharedSubFeatureHost
    {
        public void OnAttach(in FeatureModuleContext<TFeature> ctx) { }
        public void OnDetach(in FeatureModuleContext<TFeature> ctx) { }

        public void Tick(in FeatureModuleContext<TFeature> ctx, float deltaTime)
        {
            var f = ctx.Feature;
            if (f is IViewFeatureRuntime viewRuntime)
            {
                viewRuntime.EventSink?.Tick();
            }
        }

        public void RebindAll(in FeatureModuleContext<TFeature> ctx) { }
    }
}
