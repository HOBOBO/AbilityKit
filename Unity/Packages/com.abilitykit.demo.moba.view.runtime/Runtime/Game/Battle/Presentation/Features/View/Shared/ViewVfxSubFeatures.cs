using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle.Hierarchy;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow.Battle.View;
using AbilityKit.Game.Flow.Modules;
using AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal sealed class ViewVfxSubFeature<TFeature> : IViewSubFeature<TFeature>
        where TFeature : class, IViewFeatureRuntime
    {
        private readonly ViewVfxRuntimeFactory _factory;

        public ViewVfxSubFeature(ViewVfxRuntimeFactory factory = null)
        {
            _factory = factory ?? new ViewVfxRuntimeFactory();
        }

        public void OnAttach(in FeatureModuleContext<TFeature> ctx)
        {
            var runtime = ctx.Feature;
            if (runtime == null) return;

            var hierarchy = runtime.Hierarchy;
            runtime.Vfx = _factory.CreateManager(runtime.Resources, hierarchy);
            runtime.VfxNode = _factory.CreateNode(runtime.Context, runtime.IsConfirmed);
            if (runtime.Context != null && !runtime.IsConfirmed)
            {
                runtime.ContextVfxBindingGeneration =
                    runtime.Context.BindViewVfx(runtime.Vfx, runtime.VfxNode);
            }

            // If a stats overlay exists, register the VFX pool as a provider so the
            // inspector surfaces VFX reuse counts alongside shell/area counts.
            var overlay = hierarchy?.Root != null ? hierarchy.Root.GetComponent<BattleViewPoolStatsOverlay>() : null;
            if (overlay != null && runtime.Vfx != null)
            {
                overlay.RegisterProvider(new BattleVfxPoolStatsProvider(runtime.Vfx.PoolForStats));
            }
        }

        public void OnDetach(in FeatureModuleContext<TFeature> ctx)
        {
            var runtime = ctx.Feature;
            if (runtime == null) return;

            if (runtime.Context != null &&
                !runtime.IsConfirmed &&
                runtime.ContextVfxBindingGeneration != 0)
            {
                runtime.Context.ClearViewVfx(
                    runtime.ContextVfxBindingGeneration);
            }

            runtime.ContextVfxBindingGeneration = 0;

            var vfxNode = runtime.VfxNode;
            runtime.Vfx?.Clear(in vfxNode);
            if (vfxNode.IsValid)
            {
                vfxNode.Destroy();
            }

            runtime.Vfx = null;
            runtime.VfxNode = default;
            runtime.Hierarchy = null;
        }

        public void Tick(in FeatureModuleContext<TFeature> ctx, float deltaTime) { }

        public void RebindAll(in FeatureModuleContext<TFeature> ctx) { }
    }

    internal sealed class ViewVfxRuntimeFactory
    {
        public BattleVfxManager CreateManager(BattleViewResourceProvider resources)
        {
            return CreateManager(resources, hierarchy: null);
        }

        public BattleVfxManager CreateManager(BattleViewResourceProvider resources, BattleViewHierarchyManager hierarchy)
        {
            if (resources == null)
            {
                return hierarchy != null
                    ? new BattleVfxManager(null, new BattleVfxManagerComponentFactory(), hierarchy)
                    : new BattleVfxManager(null, new BattleVfxManagerComponentFactory());
            }

            var db = resources.GetOrLoadVfxDb();
            return hierarchy != null
                ? new BattleVfxManager(db, new BattleVfxManagerComponentFactory(), hierarchy)
                : new BattleVfxManager(db, new BattleVfxManagerComponentFactory());
        }

        public IEntity CreateNode(BattleContext ctx, bool isConfirmed)
        {
            if (ctx == null || !ctx.EntityNode.IsValid)
            {
                return default;
            }

            var vfxNode = ctx.EntityNode.World.CreateChild(ctx.EntityNode);
            vfxNode.SetName(isConfirmed ? "BattleVfx_confirmed" : "BattleVfx");
            return vfxNode;
        }
    }
}
