using System;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Battle.Shared.Assets;
using UnityEngine;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private IBattleAssetLease _assetLease;

        internal IBattleAssetLease AssetLease => _assetLease;

        public void OnAttach(in GamePhaseContext ctx)
        {
            TryInstallUnityLogSinkIfNeeded();

            _phaseCtx = ctx;
            BattleContext battleCtx;
            ctx.Features.TryGet(out battleCtx);
            _ctx = battleCtx;
            _flow = ctx.Entry != null ? ctx.Entry.Get<GameFlowDomain>() : null;

            _eventsCtrl.OnAttach(this);
            Battle.Replay.BattleReplayControlProvider.Current = this;

            EnsureSubFeaturesCreated();
            _subFeatureHost?.Attach(new FeatureModuleContext<BattleSessionFeature>(ctx, this));
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
            if (ReferenceEquals(Battle.Replay.BattleReplayControlProvider.Current, this))
            {
                Battle.Replay.BattleReplayControlProvider.Current = null;
            }

            _subFeatureHost?.Detach(new FeatureModuleContext<BattleSessionFeature>(ctx, this));

            _replayOwner.Stop();
            StopSession();

            DisposeRemoteInterpolation();

            ResetHandles();

            _state.ResetSessionFlags();

            _eventsCtrl.OnDetach(this);

            SessionContextBinder.ClearSession(_ctx);
            ReleaseAssetLease();

            _ctx = null;
            _flow = null;
            _phaseCtx = default;
        }

        internal void AdoptAssetLease(IBattleAssetLease lease)
        {
            if (lease == null) throw new ArgumentNullException(nameof(lease));

            var previous = _assetLease;
            _assetLease = lease;
            if (previous == null || ReferenceEquals(previous, lease)) return;

            try
            {
                previous.Dispose();
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSessionFeature] Failed to release replaced battle asset lease");
            }
        }

        private void ReleaseAssetLease()
        {
            var lease = _assetLease;
            _assetLease = null;
            if (lease == null) return;

            try
            {
                lease.Dispose();
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSessionFeature] Failed to release battle asset lease");
            }
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            Hooks?.PreTick.Invoke(deltaTime);
            InvokeSubFeaturesPreTick(ctx, deltaTime);
            _replayOwner.Tick(deltaTime);

            if (_session == null) return;

            InvokeMainTickSubFeatures(ctx, deltaTime);

            if (_ctx != null)
            {
                SessionContextBinder.BindLastFrame(_ctx, _state);
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
        }

        private void InvokeMainTickSubFeatures(in GamePhaseContext ctx, float deltaTime)
        {
            if (_subFeatureHost == null) return;
            var fctx = new FeatureModuleContext<BattleSessionFeature>(ctx, this);
            _subFeatureHost.ForEach<ISessionMainTickSubFeature<BattleSessionFeature>>(m => m.MainTick(fctx, deltaTime));
        }
    }
}
