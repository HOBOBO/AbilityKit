using System;
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
            ctx.Features.Set<IBattleAssetLoadSessionPort>(this);
            _flow = ctx.Entry != null ? ctx.Entry.Get<GameFlowDomain>() : null;

            _eventsCtrl.OnAttach(this);
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
            if (ReferenceEquals(Battle.Replay.BattleReplayControlProvider.Current, this))
            {
                Battle.Replay.BattleReplayControlProvider.Current = null;
            }

            SessionTeardownPolicy.Execute(
                OnDetachCleanupFailed,
                new SessionTeardownStep(
                    "sub-features",
                    () => _subFeatureHost?.Detach(new FeatureModuleContext<BattleSessionFeature>(detachContext, this))),
                new SessionTeardownStep("spectator session", _runtime.Spectator.Stop),
                new SessionTeardownStep("replay session", _runtime.Replay.Stop),
                new SessionTeardownStep("battle session", StopSession),
                new SessionTeardownStep("remote interpolation", DisposeRemoteInterpolation),
                new SessionTeardownStep("session handles", ResetHandles),
                new SessionTeardownStep("session flags", _state.ResetSessionFlags),
                new SessionTeardownStep("session events", () => _eventsCtrl.OnDetach(this)),
                new SessionTeardownStep("session context", () => SessionContextBinder.ClearSession(_ctx)),
                new SessionTeardownStep("input context", () => _runtime.UnbindContext(_ctx)),
                new SessionTeardownStep("asset load port", () => UnpublishAssetLoadPort(detachContext)),
                new SessionTeardownStep("session diagnostics", _runtime.Diagnostics.Dispose),
                new SessionTeardownStep("asset lease", _runtime.Assets.Dispose));

            _ctx = null;
            _flow = null;
            _phaseCtx = default;
        }

        private static void OnDetachCleanupFailed(string resourceName, Exception exception)
        {
            Log.Exception(
                exception,
                $"[BattleSessionFeature] Failed to release {resourceName} during detach");
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

            InvokeMainTickSubFeatures(ctx, deltaTime);

            if (_ctx != null)
            {
                var projection = _tickLoop.CreateProjection();
                SessionContextBinder.BindTickProjection(_ctx, in projection);
                if (_ctx.TryGetRuntimeWorld(out var runtimeWorld))
                    _runtime.Diagnostics.TryBindMetricSink(runtimeWorld);
                _runtime.Diagnostics.RecordFrameMetrics(_ctx);
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
