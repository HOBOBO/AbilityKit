using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class ViewEventSinkSubFeature<TFeature> : IViewSubFeature<TFeature>
        where TFeature : class, IViewFeatureRuntime
    {
        private readonly ViewEventSinkFactory _factory;

        public ViewEventSinkSubFeature()
            : this(new ViewEventSinkFactory())
        {
        }

        public ViewEventSinkSubFeature(ViewEventSinkFactory factory)
        {
            _factory = factory ?? new ViewEventSinkFactory();
        }

        public void OnAttach(in FeatureModuleContext<TFeature> ctx)
        {
            var runtime = ctx.Feature;
            if (runtime == null) return;

            runtime.EventSink = _factory.Create(runtime);
        }

        public void OnDetach(in FeatureModuleContext<TFeature> ctx)
        {
            var runtime = ctx.Feature;
            if (runtime == null) return;

            runtime.EventSink?.Clear();
            runtime.EventSink = null;
        }

        public void Tick(in FeatureModuleContext<TFeature> ctx, float deltaTime) { }

        public void RebindAll(in FeatureModuleContext<TFeature> ctx) { }
    }
}
