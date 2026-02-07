using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void OnStartSessionRequested()
        {
            try
            {
                StartSession();
                Events?.Publish(new SessionStartedEvent(_plan));
                Events?.Flush();
                ApplyAutoPlanActions();
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSessionFeature] StartSession failed after gateway room preparation");
                StopSession();
                Events?.Publish(new SessionFailedEvent(ex));
                Events?.Flush();
                return;
            }

            if (_ctx != null)
            {
                _ctx.Plan = _plan;
                _ctx.Session = _session;
                _ctx.LastFrame = _lastFrame;
            }
        }

        private void InvokeModulesPreTick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_subFeatureHost == null) return;
            var fctx = new FeatureModuleContext<BattleSessionFeature>(ctx, this);
            _subFeatureHost.ForEach<ISessionPreTickSubFeature<BattleSessionFeature>>(m => m.PreTick(fctx, deltaTime));
        }

        private bool InvokeModulesPlanBuilt()
        {
            if (_subFeatureHost == null) return false;
            var fctx = new FeatureModuleContext<BattleSessionFeature>(_phaseCtx, this);
            var handled = false;
            _subFeatureHost.ForEach<ISessionPlanSubFeature<BattleSessionFeature>>(m =>
            {
                if (!handled && m.OnPlanBuilt(fctx)) handled = true;
            });
            return handled;
        }

        private void InvokeSessionStartingPipeline()
        {
            if (_subFeatureHost == null) return;
            var fctx = new FeatureModuleContext<BattleSessionFeature>(_phaseCtx, this);
            _subFeatureHost.ForEach<ISessionLifecycleNotifySubFeature<BattleSessionFeature>>(m => m.NotifySessionStarting(fctx));
            _subFeatureHost.ForEach<ISessionLifecycleSubFeature<BattleSessionFeature>>(m => m.OnSessionStarting(fctx));
        }

        private void InvokeSessionStoppingPipeline()
        {
            if (_subFeatureHost == null) return;
            var fctx = new FeatureModuleContext<BattleSessionFeature>(_phaseCtx, this);
            _subFeatureHost.ForEach<ISessionLifecycleNotifySubFeature<BattleSessionFeature>>(m => m.NotifySessionStopping(fctx));
            _subFeatureHost.ForEachReverse<ISessionLifecycleSubFeature<BattleSessionFeature>>(m => m.OnSessionStopping(fctx));
        }

        private void InvokeReplaySetupPipeline()
        {
            if (_subFeatureHost == null) return;
            var fctx = new FeatureModuleContext<BattleSessionFeature>(_phaseCtx, this);
            _subFeatureHost.ForEach<ISessionReplaySetupSubFeature<BattleSessionFeature>>(m => m.SetupReplayOrRecord(fctx));
        }
    }
}
