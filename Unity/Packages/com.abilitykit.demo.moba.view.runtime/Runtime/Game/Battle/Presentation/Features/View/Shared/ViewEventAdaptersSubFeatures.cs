using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class ViewEventAdaptersSubFeature<TFeature> : IViewSubFeature<TFeature>
        where TFeature : class, IViewFeatureRuntime
    {
        private readonly ViewEventAdapterLifecycle _lifecycle;

        public ViewEventAdaptersSubFeature(ViewEventAdapterLifecycle lifecycle = null)
        {
            _lifecycle = lifecycle ?? new ViewEventAdapterLifecycle();
        }

        public void OnAttach(in FeatureModuleContext<TFeature> ctx)
        {
            _lifecycle.Attach(ctx.Feature);
        }

        public void OnDetach(in FeatureModuleContext<TFeature> ctx)
        {
            _lifecycle.Detach(ctx.Feature);
        }

        public void Tick(in FeatureModuleContext<TFeature> ctx, float deltaTime)
        {
            _lifecycle.Refresh(ctx.Feature);
        }

        public void RebindAll(in FeatureModuleContext<TFeature> ctx)
        {
            _lifecycle.Refresh(ctx.Feature);
        }
    }
}
