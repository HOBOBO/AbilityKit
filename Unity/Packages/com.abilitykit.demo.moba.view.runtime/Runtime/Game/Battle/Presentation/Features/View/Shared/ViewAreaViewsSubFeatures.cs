using AbilityKit.Game.Flow.Battle.View;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class ViewAreaViewsSubFeature<TFeature> : IViewSubFeature<TFeature>
        where TFeature : class, IViewFeatureRuntime
    {
        private readonly ViewAreaViewsRuntimeFactory _factory;

        public ViewAreaViewsSubFeature(ViewAreaViewsRuntimeFactory factory = null)
        {
            _factory = factory ?? new ViewAreaViewsRuntimeFactory();
        }

        public void OnAttach(in FeatureModuleContext<TFeature> ctx)
        {
            var runtime = ctx.Feature;
            if (runtime == null) return;

            runtime.AreaViews?.Clear();
            runtime.AreaViews = _factory.Create(
                runtime.Resources,
                runtime.AreaVfxPool,
                runtime.Hierarchy);
        }

        public void OnDetach(in FeatureModuleContext<TFeature> ctx)
        {
            var runtime = ctx.Feature;
            if (runtime == null) return;

            runtime.AreaViews?.Clear();
            runtime.AreaViews = null;
        }

        public void Tick(in FeatureModuleContext<TFeature> ctx, float deltaTime) { }

        public void RebindAll(in FeatureModuleContext<TFeature> ctx) { }
    }

    internal sealed class ViewAreaViewsRuntimeFactory
    {
        public BattleAreaViewSystem Create(
            BattleViewResourceProvider resources,
            BattleAreaVfxPool areaVfxPool,
            AbilityKit.Game.Battle.Hierarchy.BattleViewHierarchyManager hierarchy = null)
        {
            return new BattleAreaViewSystem(resources, areaVfxPool, hierarchy);
        }
    }
}
