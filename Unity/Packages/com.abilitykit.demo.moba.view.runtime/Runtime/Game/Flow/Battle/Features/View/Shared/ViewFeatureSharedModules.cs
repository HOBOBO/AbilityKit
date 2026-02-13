using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Battle.View;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal interface IViewFeatureModulesHost
    {
        BattleContext Context { get; }
        BattleViewBinder Binder { get; }
        BattleEventBus Events { get; }

        bool IsConfirmed { get; }
        WorldId WorldId { get; }

        void RefreshDirtyViews();
        void RegisterAllSeekables();
        void SeekAllToCurrentFrame();

        void RebindAllViews();

        void TickVfx();
        void TickFloatingTexts(float deltaTime);
    }

    internal interface IViewFeatureModule<TFeature> :
        IViewSubFeature<TFeature>
        where TFeature : class, IViewFeatureModulesHost
    {
    }

    internal interface IViewSubFeature<TFeature> :
        IGameModule<FeatureModuleContext<TFeature>>,
        IGameModuleTick<FeatureModuleContext<TFeature>>,
        IGameModuleRebind<FeatureModuleContext<TFeature>>
        where TFeature : class, IViewFeatureModulesHost
    {
    }

    internal sealed class SharedDirtySyncModule<TFeature> : IViewFeatureModule<TFeature>
        where TFeature : class, IViewFeatureModulesHost
    {
        public void OnAttach(in FeatureModuleContext<TFeature> ctx) { }
        public void OnDetach(in FeatureModuleContext<TFeature> ctx) { }

        public void Tick(in FeatureModuleContext<TFeature> ctx, float deltaTime)
        {
            var f = ctx.Feature;
            if (f?.Context?.DirtyEntities == null) return;
            if (f.Context.DirtyEntities.Count == 0) return;
            f.RefreshDirtyViews();
        }

        public void RebindAll(in FeatureModuleContext<TFeature> ctx)
        {
            var f = ctx.Feature;
            if (f?.Context?.EntityWorld == null) return;
            f.RebindAllViews();
        }
    }

    internal sealed class SharedTimelineModule<TFeature> : IViewFeatureModule<TFeature>
        where TFeature : class, IViewFeatureModulesHost
    {
        private IDisposable _readySub;
        private IDisposable _reboundSub;

        private int _lastSeenFrame = int.MinValue;

        public void OnAttach(in FeatureModuleContext<TFeature> ctx)
        {
            var f = ctx.Feature;
            var events = f?.Events;

            _lastSeenFrame = int.MinValue;

            _readySub = events?.Subscribe<ViewBinderReadyEvent>(e =>
            {
                if (f == null) return;
                if (e.IsConfirmed != f.IsConfirmed) return;
                if (!WorldId.Equals(e.WorldId, f.WorldId)) return;
                f.RegisterAllSeekables();
                f.SeekAllToCurrentFrame();
            });

            _reboundSub = events?.Subscribe<ViewsReboundEvent>(e =>
            {
                if (f == null) return;
                if (e.IsConfirmed != f.IsConfirmed) return;
                if (!WorldId.Equals(e.WorldId, f.WorldId)) return;
                f.RegisterAllSeekables();
                f.SeekAllToCurrentFrame();
            });
        }

        public void OnDetach(in FeatureModuleContext<TFeature> ctx)
        {
            _readySub?.Dispose();
            _readySub = null;

            _reboundSub?.Dispose();
            _reboundSub = null;
        }

        public void Tick(in FeatureModuleContext<TFeature> ctx, float deltaTime)
        {
            var f = ctx.Feature;
            var battleCtx = f?.Context;
            if (battleCtx?.EntityWorld == null) return;

            var frame = battleCtx.LastFrame;
            if (frame == _lastSeenFrame) return;
            _lastSeenFrame = frame;

            f.SeekAllToCurrentFrame();
        }

        public void RebindAll(in FeatureModuleContext<TFeature> ctx)
        {
            var f = ctx.Feature;
            if (f == null) return;
            _lastSeenFrame = int.MinValue;
            f.RegisterAllSeekables();
            f.SeekAllToCurrentFrame();
        }
    }

    internal sealed class SharedVfxTickModule<TFeature> : IViewFeatureModule<TFeature>
        where TFeature : class, IViewFeatureModulesHost
    {
        public void OnAttach(in FeatureModuleContext<TFeature> ctx) { }
        public void OnDetach(in FeatureModuleContext<TFeature> ctx) { }

        public void Tick(in FeatureModuleContext<TFeature> ctx, float deltaTime)
        {
            ctx.Feature?.TickVfx();
        }

        public void RebindAll(in FeatureModuleContext<TFeature> ctx) { }
    }

    internal sealed class SharedInterpolationModule<TFeature> : IViewFeatureModule<TFeature>
        where TFeature : class, IViewFeatureModulesHost
    {
        public void OnAttach(in FeatureModuleContext<TFeature> ctx) { }
        public void OnDetach(in FeatureModuleContext<TFeature> ctx) { }

        public void Tick(in FeatureModuleContext<TFeature> ctx, float deltaTime)
        {
            var f = ctx.Feature;
            var binder = f?.Binder;
            if (binder == null) return;
            binder.TickInterpolation(f.Context, deltaTime);
        }

        public void RebindAll(in FeatureModuleContext<TFeature> ctx) { }
    }

    internal sealed class SharedFloatingTextModule<TFeature> : IViewFeatureModule<TFeature>
        where TFeature : class, IViewFeatureModulesHost
    {
        public void OnAttach(in FeatureModuleContext<TFeature> ctx) { }
        public void OnDetach(in FeatureModuleContext<TFeature> ctx) { }

        public void Tick(in FeatureModuleContext<TFeature> ctx, float deltaTime)
        {
            ctx.Feature?.TickFloatingTexts(deltaTime);
        }

        public void RebindAll(in FeatureModuleContext<TFeature> ctx) { }
    }
}
