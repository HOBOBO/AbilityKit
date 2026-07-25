using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class SharedDirtySyncSubFeature<TFeature> : IViewSubFeature<TFeature>
        where TFeature : class, IViewSharedSubFeatureHost
    {
        public void OnAttach(in FeatureModuleContext<TFeature> ctx) { }
        public void OnDetach(in FeatureModuleContext<TFeature> ctx) { }

        public void Tick(in FeatureModuleContext<TFeature> ctx, float deltaTime)
        {
            var f = ctx.Feature;
            var dirty = f?.Context?.DirtyEntities;
            if (dirty == null) return;

            // Clear at the start of the frame so multiple producers (Spawn + Transform)
            // can all append without overwriting each other's entries.
            dirty.Clear();

            if (dirty.Count == 0) return;
            f.RefreshDirtyViews();
        }

        public void RebindAll(in FeatureModuleContext<TFeature> ctx)
        {
            var f = ctx.Feature;
            if (f?.Context?.EntityWorld == null) return;

            f.RebindAllViews();
        }
    }
}
