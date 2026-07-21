using AbilityKit.Game.Flow.Battle.View;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class ViewFloatingTextSubFeature<TFeature> : IViewSubFeature<TFeature>
        where TFeature : class, IViewFeatureRuntime
    {
        private readonly BattleWorldFloatingTextFactory _factory;

        public ViewFloatingTextSubFeature()
            : this(new BattleWorldFloatingTextFactory())
        {
        }

        public ViewFloatingTextSubFeature(BattleWorldFloatingTextFactory factory)
        {
            _factory = factory ?? new BattleWorldFloatingTextFactory();
        }

        public void OnAttach(in FeatureModuleContext<TFeature> ctx)
        {
            var runtime = ctx.Feature;
            if (runtime == null) return;

            runtime.FloatingTexts?.Clear();
            _factory.SetHierarchyManager(runtime.Hierarchy);
            runtime.FloatingTexts = new BattleFloatingTextSystem(_factory);
        }

        public void OnDetach(in FeatureModuleContext<TFeature> ctx)
        {
            var runtime = ctx.Feature;
            if (runtime == null) return;

            runtime.FloatingTexts?.Clear();
            runtime.FloatingTexts = null;
        }

        public void Tick(in FeatureModuleContext<TFeature> ctx, float deltaTime) { }

        public void RebindAll(in FeatureModuleContext<TFeature> ctx) { }
    }
}
