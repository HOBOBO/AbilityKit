using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Battle.Shared.Assets;
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
            _featureLifecycle.Attach();
            try
            {
                _startupTasks.Attach();

                _phaseCtx = ctx;
                _ctx = ctx.RequireFeature<BattleContext>(nameof(BattleSessionFeature));
                _runtime.BindContext(_ctx);
                _runtime.Diagnostics.BindScope(_plan.World.WorldId);
                ctx.Features.Set<IBattleAssetLoadSessionPort>(this);
                _flow = ctx.Entry != null ? ctx.Entry.Get<IBattleViewFeatureHost>() : null;

                _eventsCtrl.OnAttach(this);
                Battle.Replay.BattleReplayControlProvider.Publish(
                    _plan.World.WorldId,
                    this);
                Battle.Replay.BattleReplayControlProvider.Current = this;

                EnsureSubFeaturesCreated();
                _subFeatureHost?.Attach(new FeatureModuleContext<ISessionRuntimeFeature>(ctx, _moduleRuntime));
                _runtime.Diagnostics.PublishDebugControls(_ctx);
            }
            catch
            {
                _ = ObserveDetachFailureAsync(DetachAsync(ctx));
                throw;
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            var detachTask = DetachAsync(ctx);
            if (!detachTask.IsCompleted)
            {
                _ = ObserveDetachFailureAsync(detachTask);
                return;
            }

            if (detachTask.IsCanceled)
            {
                throw new TaskCanceledException(detachTask);
            }

            if (detachTask.IsFaulted)
            {
                var exception = detachTask.Exception;
                if (exception.InnerExceptions.Count == 1)
                {
                    ExceptionDispatchInfo.Capture(exception.InnerExceptions[0]).Throw();
                }

                throw exception;
            }
        }

        public Task DetachAsync(GamePhaseContext detachContext) =>
            _featureLifecycle.DetachAsync(detachContext);

        private AsyncSessionTeardownStep[] CreateTeardownSteps(
            GamePhaseContext detachContext)
        {
            return new[]
            {
                new AsyncSessionTeardownStep("session startup", _startupTasks.BeginDetachAsync),
                new AsyncSessionTeardownStep("replay control", WithdrawReplayControl),
                new AsyncSessionTeardownStep(
                    "sub-features",
                    () =>
                    {
                        if (_subFeatureHost?.IsAttached == true)
                            _subFeatureHost.Detach(new FeatureModuleContext<ISessionRuntimeFeature>(detachContext, _moduleRuntime));
                    }),
                new AsyncSessionTeardownStep("gateway room", StopGatewayRoomPreparationAsync),
                new AsyncSessionTeardownStep("network condition", () => NetworkCondition.Detach()),
                new AsyncSessionTeardownStep("spectator view", HideSpectatorView),
                new AsyncSessionTeardownStep("replay view", HideReplayView),
                new AsyncSessionTeardownStep("spectator session", StopSpectatingAsync),
                new AsyncSessionTeardownStep("replay session", _runtime.Replay.Stop),
                new AsyncSessionTeardownStep("battle session", _clientDriver.StopAsync),
                new AsyncSessionTeardownStep("session flags", _state.ResetSessionFlags),
                new AsyncSessionTeardownStep("session events", () => _eventsCtrl.OnDetach(this)),
                new AsyncSessionTeardownStep("session context", () => SessionContextBinder.ClearSession(_ctx)),
                new AsyncSessionTeardownStep("input context", () => _runtime.UnbindContext(_ctx)),
                new AsyncSessionTeardownStep("asset load port", () => UnpublishAssetLoadPort(detachContext)),
                new AsyncSessionTeardownStep("session diagnostics", _runtime.Diagnostics.Dispose),
                new AsyncSessionTeardownStep("asset lease", _runtime.Assets.Dispose),
                new AsyncSessionTeardownStep("session handles", ResetHandles),
                new AsyncSessionTeardownStep("feature references", ClearFeatureReferences),
            };
        }

        private void WithdrawReplayControl()
        {
            Battle.Replay.BattleReplayControlProvider.Withdraw(
                _plan.World.WorldId,
                this);
            if (ReferenceEquals(Battle.Replay.BattleReplayControlProvider.Current, this))
            {
                Battle.Replay.BattleReplayControlProvider.Current = null;
            }
        }

        private void ClearFeatureReferences()
        {
            _ctx = null;
            _flow = null;
            _phaseCtx = default;
        }

        private static async Task ObserveDetachFailureAsync(Task detachTask)
        {
            try
            {
                await (detachTask ?? Task.CompletedTask);
            }
            catch (Exception exception)
            {
                Log.Exception(exception, "[BattleSessionFeature] Asynchronous detach failed");
            }
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
            _runtime.Spectator.Tick(10);

            if (_session == null) return;

            // Lockstep recovery is a production battle lifecycle responsibility. It must keep
            // progressing in release/headless builds even when the debug GUI feature is absent.
            if (_runtime.LockstepRecovery.IsRecovering && _ctx != null)
            {
                _runtime.LockstepRecovery.TickRecovery(_ctx);
            }

            InvokeMainTickSubFeatures(ctx, deltaTime);

            if (_ctx != null)
            {
                var projection = _tickLoop.CreateProjection();
                SessionContextBinder.BindTickProjection(_ctx, in projection);
                _runtime.Diagnostics.RecordFrameMetrics(_ctx, in projection);
            }

            _subFeatureHost?.Tick(new FeatureModuleContext<ISessionRuntimeFeature>(ctx, _moduleRuntime), deltaTime);
            Hooks?.PostTick.Invoke(deltaTime);
        }

        private void InvokeMainTickSubFeatures(in GamePhaseContext ctx, float deltaTime)
        {
            if (_subFeatureHost == null) return;
            var fctx = new FeatureModuleContext<ISessionRuntimeFeature>(ctx, _moduleRuntime);
            _subFeatureHost.ForEach<ISessionMainTickSubFeature<ISessionRuntimeFeature>>(m => m.MainTick(fctx, deltaTime));
        }
    }
}
