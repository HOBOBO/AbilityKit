using System;
using System.Threading.Tasks;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Battle.Shared.Assets;
using UnityEngine;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        internal IBattleAssetLease AssetLease => _runtime.Assets.Lease;

        IBattleAssetLookup IBattleAssetLoadSessionPort.AssetLookup =>
            _runtime.Assets.AssetLookup;

        public void OnAttach(in GamePhaseContext ctx)
        {
            TryInstallUnityLogSinkIfNeeded();

            _phaseCtx = ctx;
            BattleContext battleCtx;
            ctx.Features.TryGet(out battleCtx);
            _ctx = battleCtx;
            _runtime.BindContext(_ctx);
            _runtime.Diagnostics.BindScope(_plan.World.WorldId);
            ctx.Features.Set<IBattleAssetLoadSessionPort>(this);
            _flow = ctx.Entry != null ? ctx.Entry.Get<GameFlowDomain>() : null;

            _eventsCtrl.OnAttach(this);
            Battle.Replay.BattleReplayControlProvider.Publish(
                _plan.World.WorldId,
                this);
            Battle.Replay.BattleReplayControlProvider.Current = this;

            EnsureSubFeaturesCreated();
            _subFeatureHost?.Attach(new FeatureModuleContext<BattleSessionFeature>(ctx, this));
            _runtime.Diagnostics.PublishDebugControls();
        }

        private static void TryInstallUnityLogSinkIfNeeded()
        {
            if (!(Log.Sink is NullLogSink)) return;

            try
            {
                var type = Type.GetType("AbilityKit.Examples.Common.Log.UnityLogSink, AbilityKit.Demo.Moba.View.Runtime");
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
            var detachContext = ctx;
            Battle.Replay.BattleReplayControlProvider.Withdraw(
                _plan.World.WorldId,
                this);
            if (ReferenceEquals(Battle.Replay.BattleReplayControlProvider.Current, this))
            {
                Battle.Replay.BattleReplayControlProvider.Current = null;
            }

            try
            {
                DetachAsync(detachContext).GetAwaiter().GetResult();
            }
            finally
            {
                _ctx = null;
                _flow = null;
                _phaseCtx = default;
            }
        }

        private Task DetachAsync(GamePhaseContext detachContext)
        {
            return SessionTeardownPolicy.ExecuteAsync(
                new AsyncSessionTeardownStep(
                    "sub-features",
                    () => _subFeatureHost?.Detach(new FeatureModuleContext<BattleSessionFeature>(detachContext, this))),
                new AsyncSessionTeardownStep("gateway room", StopGatewayRoomPreparationAsync),
                new AsyncSessionTeardownStep("spectator session", _runtime.Spectator.StopAsync),
                new AsyncSessionTeardownStep("replay session", _runtime.Replay.Stop),
                new AsyncSessionTeardownStep("battle session", _orchestrator.StopSessionAsync),
                new AsyncSessionTeardownStep("session handles", ResetHandles),
                new AsyncSessionTeardownStep("session flags", _state.ResetSessionFlags),
                new AsyncSessionTeardownStep("session events", () => _eventsCtrl.OnDetach(this)),
                new AsyncSessionTeardownStep("session context", () => SessionContextBinder.ClearSession(_ctx)),
                new AsyncSessionTeardownStep("input context", () => _runtime.UnbindContext(_ctx)),
                new AsyncSessionTeardownStep("asset load port", () => UnpublishAssetLoadPort(detachContext)),
                new AsyncSessionTeardownStep("session diagnostics", _runtime.Diagnostics.Dispose),
                new AsyncSessionTeardownStep("asset lease", _runtime.Assets.Dispose));
        }

        internal void AdoptAssetLease(IBattleAssetLease lease) =>
            _runtime.Assets.Adopt(lease);

        void IBattleAssetLoadSessionPort.AdoptAssetLease(IBattleAssetLease lease) =>
            AdoptAssetLease(lease);

        void IBattleAssetLoadSessionPort.NotifyAssetsLoadCompleted() =>
            NotifyAssetsLoadCompleted();

        private void UnpublishAssetLoadPort(in GamePhaseContext ctx)
        {
            if (!ctx.Features.TryGet(out IBattleAssetLoadSessionPort current) ||
                !ReferenceEquals(current, this))
            {
                return;
            }

            ctx.Features.Remove<IBattleAssetLoadSessionPort>();
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            Hooks?.PreTick.Invoke(deltaTime);
            InvokeSubFeaturesPreTick(ctx, deltaTime);
            _runtime.Replay.Tick(deltaTime);

            if (_session == null) return;

            // Lockstep recovery is a production battle lifecycle responsibility. It must keep
            // progressing in release/headless builds even when the debug GUI feature is absent.
            if (MobaBattlePauseController.IsRecovering && _ctx != null)
            {
                MobaBattlePauseController.TickRecovery(_ctx);
            }

            InvokeMainTickSubFeatures(ctx, deltaTime);

            if (_ctx != null)
            {
                var projection = _tickLoop.CreateProjection();
                SessionContextBinder.BindTickProjection(_ctx, in projection);
                _runtime.Diagnostics.RecordFrameMetrics(_ctx, in projection);
            }

            _subFeatureHost?.Tick(new FeatureModuleContext<BattleSessionFeature>(ctx, this), deltaTime);
            Hooks?.PostTick.Invoke(deltaTime);
        }

        private void InvokeMainTickSubFeatures(in GamePhaseContext ctx, float deltaTime)
        {
            if (_subFeatureHost == null) return;
            var fctx = new FeatureModuleContext<BattleSessionFeature>(ctx, this);
            _subFeatureHost.ForEach<ISessionMainTickSubFeature<BattleSessionFeature>>(m => m.MainTick(fctx, deltaTime));
        }
    }
}
