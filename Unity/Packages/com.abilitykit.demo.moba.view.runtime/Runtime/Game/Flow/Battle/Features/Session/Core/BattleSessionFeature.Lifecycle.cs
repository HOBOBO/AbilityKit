using System;
using AbilityKit.Core.Common.Log;
using AbilityKit.World.ECS;
using UnityEngine;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        public void OnAttach(in GamePhaseContext ctx)
        {
            TryInstallUnityLogSinkIfNeeded();

            _phaseCtx = ctx;
            BattleContext battleCtx;
            ctx.Root.TryGetRef(out battleCtx);
            _ctx = battleCtx;
            _root = ctx.Root;
            _flow = ctx.Entry != null ? ctx.Entry.Get<GameFlowDomain>() : null;

            EnsureModulesCreated();
            _subFeatureHost?.Attach(new FeatureModuleContext<BattleSessionFeature>(ctx, this));
        }

        private static void TryInstallUnityLogSinkIfNeeded()
        {
            if (!(Log.Sink is NullLogSink)) return;

            try
            {
                var type = Type.GetType("AbilityKit.Ability.Impl.Common.Log.UnityLogSink, AbilityKit.Ability.Unity");
                if (type == null) return;
                if (!typeof(ILogSink).IsAssignableFrom(type)) return;

                var sink = Activator.CreateInstance(type) as ILogSink;
                if (sink == null) return;
                Log.SetSink(sink);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSessionFeature] TryInstallUnityLogSinkIfNeeded failed");
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            _subFeatureHost?.Detach(new FeatureModuleContext<BattleSessionFeature>(ctx, this));

            StopSession();

            ResetHandles();

            _state.ResetSessionFlags();

            if (_ctx != null)
            {
                _ctx.Session = null;
                _ctx.Events = null;
            }

            _ctx = null;
            _phaseCtx = default;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            Hooks?.PreTick.Invoke(deltaTime);
            InvokeModulesPreTick(ctx, deltaTime);
            Events?.Flush();

            if (_session == null) return;

            InvokeMainTickSubFeatures(ctx, deltaTime);

            if (_ctx != null)
            {
                _ctx.LastFrame = _lastFrame;
                var fixedDelta = GetFixedDeltaSeconds();
                if (fixedDelta > 0f)
                {
                    _ctx.LogicTimeSeconds = _lastFrame * (double)fixedDelta + (double)_tickAcc;
                }
                else
                {
                    _ctx.LogicTimeSeconds = 0d;
                }
            }

            _subFeatureHost?.Tick(new FeatureModuleContext<BattleSessionFeature>(ctx, this), deltaTime);
            Hooks?.PostTick.Invoke(deltaTime);
            Events?.Flush();
        }

        private void InvokeMainTickSubFeatures(in GamePhaseContext ctx, float deltaTime)
        {
            if (_subFeatureHost == null) return;
            var fctx = new FeatureModuleContext<BattleSessionFeature>(ctx, this);
            _subFeatureHost.ForEach<ISessionMainTickSubFeature<BattleSessionFeature>>(m => m.MainTick(fctx, deltaTime));
        }
    }
}
