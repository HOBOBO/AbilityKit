using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share.Config;
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
        private BattleVfxPoolStatsProvider _statsProvider;

        public ViewVfxSubFeature(ViewVfxRuntimeFactory factory = null)
        {
            _factory = factory ?? new ViewVfxRuntimeFactory();
        }

        public void OnAttach(in FeatureModuleContext<TFeature> ctx)
        {
            var runtime = ctx.Feature;
            if (runtime == null) return;

            try
            {
                var hierarchy = runtime.Hierarchy;
                runtime.Vfx = _factory.CreateManager(runtime.Resources, hierarchy);
                runtime.VfxNode = _factory.CreateNode(runtime.Context, runtime.IsConfirmed);
                if (runtime.Context != null && !runtime.IsConfirmed)
                {
                    runtime.ContextVfxBindingGeneration =
                        runtime.Context.BindViewVfx(runtime.Vfx, runtime.VfxNode);
                }

                var overlay = hierarchy?.Root != null ? hierarchy.Root.GetComponent<BattleViewPoolStatsOverlay>() : null;
                if (overlay != null && runtime.Vfx != null)
                {
                    _statsProvider = new BattleVfxPoolStatsProvider(runtime.Vfx.PoolForStats);
                    overlay.RegisterProvider(_statsProvider);
                }
            }
            catch (Exception attachError)
            {
                try
                {
                    OnDetach(ctx);
                }
                catch (Exception cleanupError)
                {
                    throw new AggregateException("VFX attach and rollback failed.", attachError, cleanupError);
                }
                throw;
            }
        }

        public void OnDetach(in FeatureModuleContext<TFeature> ctx)
        {
            var runtime = ctx.Feature;
            if (runtime == null) return;

            var overlay = runtime.Hierarchy?.Root != null
                ? runtime.Hierarchy.Root.GetComponent<BattleViewPoolStatsOverlay>()
                : null;
            if (_statsProvider != null) overlay?.UnregisterProvider(_statsProvider);
            _statsProvider = null;

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
                var emptyDb = new VfxDatabase(new Dictionary<int, VfxDTO>());
                return hierarchy != null
                    ? new BattleVfxManager(emptyDb, new BattleVfxManagerComponentFactory(), hierarchy)
                    : new BattleVfxManager(emptyDb, new BattleVfxManagerComponentFactory());
            }

            var db = resources.GetOrLoadVfxDb();
            var assets = resources.AssetLookup;
            return hierarchy != null
                ? new BattleVfxManager(db, new BattleVfxManagerComponentFactory(), hierarchy, assets)
                : new BattleVfxManager(db, new BattleVfxManagerComponentFactory(), null, assets);
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
