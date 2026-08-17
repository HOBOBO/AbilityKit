using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class SharedTimelineSubFeature<TFeature> : IViewSubFeature<TFeature>
        where TFeature : class, IViewSharedSubFeatureHost
    {
        private Action<ViewBinderReadyEvent> _onReadyHandler;
        private Action<ViewsReboundEvent> _onReboundHandler;

        private int _lastSeenFrame = int.MinValue;

        public void OnAttach(in FeatureModuleContext<TFeature> ctx)
        {
            var f = ctx.Feature;
            var hooks = f?.RuntimeContext?.Hooks;

            _lastSeenFrame = int.MinValue;

            _onReadyHandler = e =>
            {
                if (!ShouldHandle(f, e.IsConfirmed, e.WorldId)) return;
                f.RegisterAllSeekables();
                f.SeekAllToCurrentFrame();
            };

            _onReboundHandler = e =>
            {
                if (!ShouldHandle(f, e.IsConfirmed, e.WorldId)) return;
                f.RegisterAllSeekables();
                f.SeekAllToCurrentFrame();
            };

            hooks?.ViewBinderReady.Add(_onReadyHandler);
            hooks?.ViewsRebound.Add(_onReboundHandler);
        }

        public void OnDetach(in FeatureModuleContext<TFeature> ctx)
        {
            var hooks = ctx.Feature?.RuntimeContext?.Hooks;
            if (_onReadyHandler != null && hooks != null)
            {
                hooks.ViewBinderReady.Remove(_onReadyHandler);
            }

            if (_onReboundHandler != null && hooks != null)
            {
                hooks.ViewsRebound.Remove(_onReboundHandler);
            }

            _onReadyHandler = null;
            _onReboundHandler = null;
        }

        public void Tick(in FeatureModuleContext<TFeature> ctx, float deltaTime)
        {
            var f = ctx.Feature;
            var runtimeContext = f?.RuntimeContext;
            if (runtimeContext == null || f.EntityContext?.EntityWorld == null) return;

            var frame = runtimeContext.LastFrame;
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

        private static bool ShouldHandle(TFeature feature, bool isConfirmed, WorldId worldId)
        {
            if (feature == null) return false;
            if (isConfirmed != feature.IsConfirmed) return false;
            return WorldId.Equals(worldId, feature.WorldId);
        }
    }
}
