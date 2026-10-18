using System;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Flow;

namespace AbilityKit.Game.Battle.Presentation.Features.Loading
{
    /// <summary>
    /// Battle.LoadAssets phase adapter. Runtime ownership, lease transfer and
    /// presentation are delegated to focused collaborators.
    /// </summary>
    public sealed class BattleLoadingScreenFeature :
        IGamePhaseFeature,
        IOnGUIFeature,
        IBattleAssetLoadProgressObserver,
        IBattleLoadingCommandSink
    {
        private readonly BattleLoadingRuntime _runtime = new BattleLoadingRuntime();
        private readonly BattleLoadingScreenRenderer _renderer =
            new BattleLoadingScreenRenderer();
        private readonly IBattleAssetLoadService _loadService;
        private readonly IBattleAssetDependencyProvider _dependencyProvider;

        private IFlowCommandSink _flowSink;

        public BattleLoadingScreenFeature(
            IBattleAssetLoadService loadService,
            IBattleAssetDependencyProvider dependencyProvider)
        {
            _loadService = loadService ?? throw new ArgumentNullException(nameof(loadService));
            _dependencyProvider = dependencyProvider ?? throw new ArgumentNullException(nameof(dependencyProvider));
        }

        internal BattleLoadingScreenFeature(IBattleAssetLoadCoordinator coordinator)
        {
            _runtime.SetCoordinator(coordinator);
        }

        public BattleAssetLoadProgressSnapshot CurrentSnapshot =>
            _runtime.Snapshot.ToProgressSnapshot();

        public void OnAttach(in GamePhaseContext ctx)
        {
            try
            {
                _flowSink = ctx.Entry.Get<IFlowCommandSink>();
                ctx.Features.TryGet(out IBattleAssetLoadSessionPort sessionPort);
                _runtime.Attach(sessionPort, null);

                if (!_runtime.HasCoordinator &&
                    TryTakePreloadedLease(in ctx, out var preloadedLease) &&
                    _runtime.TryAdoptPreloadedLease(preloadedLease))
                    return;

                if (!_runtime.HasCoordinator && sessionPort != null)
                {
                    var manifest = BattleAssetManifestResolver.Resolve(
                        new BattlePlanManifestSource(sessionPort.Plan),
                        _dependencyProvider);
                    _runtime.SetCoordinator(new BattleAssetLoadCoordinator(
                        _loadService,
                        () => manifest,
                        new InlineProgress<BattleAssetLoadProgress>(_runtime.OnProgress)));
                }

                if (_runtime.HasCoordinator) _runtime.Start();
                else _runtime.MarkUnavailable();
            }
            catch (Exception attachFailure)
            {
                try { OnDetach(ctx); }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException("Loading feature attach and rollback failed.",
                        attachFailure, cleanupFailure);
                }
                throw;
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            try { _runtime.Dispose(); }
            finally { _flowSink = null; }
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            _runtime.Tick();
        }

        public void OnGUI(in GamePhaseContext ctx)
        {
            var snapshot = _runtime.Snapshot;
            _renderer.Draw(in snapshot, this);
        }

        internal void InjectCoordinator(IBattleAssetLoadCoordinator coordinator)
        {
            _runtime.SetCoordinator(coordinator);
            _runtime.Start();
        }

        void IBattleAssetLoadProgressObserver.OnLoadStarted(
            BattleAssetLoadProgressSnapshot snapshot)
        {
            ((IBattleAssetLoadProgressObserver)_runtime).OnLoadStarted(snapshot);
        }

        void IBattleAssetLoadProgressObserver.OnLoadProgressed(
            BattleAssetLoadProgressSnapshot snapshot)
        {
            ((IBattleAssetLoadProgressObserver)_runtime).OnLoadProgressed(snapshot);
        }

        void IBattleAssetLoadProgressObserver.OnLoadCompleted(
            BattleAssetLoadProgressSnapshot snapshot)
        {
            ((IBattleAssetLoadProgressObserver)_runtime).OnLoadCompleted(snapshot);
        }

        void IBattleAssetLoadProgressObserver.OnLoadCancelled(
            BattleAssetLoadProgressSnapshot snapshot)
        {
            ((IBattleAssetLoadProgressObserver)_runtime).OnLoadCancelled(snapshot);
        }

        void IBattleLoadingCommandSink.RequestCancel()
        {
            _runtime.Cancel();
        }

        void IBattleLoadingCommandSink.RequestRetry()
        {
            _runtime.Retry();
        }

        void IBattleLoadingCommandSink.RequestReturnLobby()
        {
            _flowSink?.RequestReturnLobby();
        }

        private static bool TryTakePreloadedLease(
            in GamePhaseContext ctx,
            out IBattleAssetLease lease)
        {
            lease = null;
            if (ctx.Entry == null ||
                !ctx.Entry.TryGet(out IBattleAssetLeaseTransferSource transferSource) ||
                transferSource == null)
            {
                return false;
            }

            lease = transferSource.TakeLease();
            return lease != null;
        }
    }
}
