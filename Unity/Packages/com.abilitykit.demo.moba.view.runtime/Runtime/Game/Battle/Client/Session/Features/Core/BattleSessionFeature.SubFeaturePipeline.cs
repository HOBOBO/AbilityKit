using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void InvokeSubFeaturesPreTick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_subFeatureHost == null) return;
            var fctx = new FeatureModuleContext<ISessionRuntimeFeature>(ctx, _moduleRuntime);
            _subFeatureHost.ForEach<ISessionPreTickSubFeature<ISessionRuntimeFeature>>(m => m.PreTick(fctx, deltaTime));
        }

        private bool InvokeSubFeaturesPlanBuilt()
        {
            if (_subFeatureHost == null) return false;
            var fctx = new FeatureModuleContext<ISessionRuntimeFeature>(_phaseCtx, _moduleRuntime);
            var handled = false;
            _subFeatureHost.ForEach<ISessionPlanSubFeature<ISessionRuntimeFeature>>(m =>
            {
                if (!handled && m.OnPlanBuilt(fctx)) handled = true;
            });
            return handled;
        }

        private void InvokeSessionStartingPipeline()
        {
            if (_subFeatureHost == null) return;
            var fctx = new FeatureModuleContext<ISessionRuntimeFeature>(_phaseCtx, _moduleRuntime);
            _subFeatureHost.ForEach<ISessionLifecycleNotifySubFeature<ISessionRuntimeFeature>>(m => m.NotifySessionStarting(fctx));
            _subFeatureHost.ForEach<ISessionLifecycleSubFeature<ISessionRuntimeFeature>>(m => m.OnSessionStarting(fctx));
        }

        private void InvokeSessionStoppingPipeline()
        {
            if (_subFeatureHost == null) return;
            var fctx = new FeatureModuleContext<ISessionRuntimeFeature>(_phaseCtx, _moduleRuntime);
            _subFeatureHost.ForEach<ISessionLifecycleNotifySubFeature<ISessionRuntimeFeature>>(m => m.NotifySessionStopping(fctx));
            _subFeatureHost.ForEachReverse<ISessionLifecycleSubFeature<ISessionRuntimeFeature>>(m => m.OnSessionStopping(fctx));
        }

        private void InvokeReplaySetupPipeline()
        {
            if (_subFeatureHost == null) return;
            var fctx = new FeatureModuleContext<ISessionRuntimeFeature>(_phaseCtx, _moduleRuntime);
            _subFeatureHost.ForEach<ISessionReplaySetupSubFeature<ISessionRuntimeFeature>>(m => m.SetupReplayOrRecord(fctx));
        }
    }
}
