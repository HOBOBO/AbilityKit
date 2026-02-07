using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        public void OnAttach(in GamePhaseContext ctx)
        {
            _phaseCtx = ctx;
            BattleContext battleCtx;
            ctx.Root.TryGetComponent(out battleCtx);
            _ctx = battleCtx;
            _root = ctx.Root;
            _flow = ctx.Entry != null ? ctx.Entry.Get<GameFlowDomain>() : null;

            EnsureModulesCreated();
            _subFeatureHost?.Attach(new FeatureModuleContext<BattleSessionFeature>(ctx, this));
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
