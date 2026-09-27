using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal sealed class BattlePresentationCueViewEventHandler
    {
        private readonly BattlePresentationCueResolver _resolver;
        private readonly PresentationCueReconciliationController<
            BattlePresentationCueRequestKey,
            BattlePresentationCueSpawnRequest> _reconciliation = new();
        private readonly PresentationCueViewRegistry<
            BattlePresentationCueRequestKey,
            BattlePresentationCueSpawnRequest,
            EC.IEntityId> _views;

        public long ConfirmedPredictionCount => _reconciliation.ConfirmedCount;
        public long CorrectedPredictionCount => _reconciliation.CorrectedCount;
        public long RejectedPredictionCount => _reconciliation.RejectedCount;
        public long StalePredictionUpdateCount => _reconciliation.StaleUpdateCount;
        public long DuplicatePredictionUpdateCount => _reconciliation.DuplicateUpdateCount;
        public long PredictionGenerationMismatchCount => _reconciliation.GenerationMismatchCount;
        internal long ReconciliationGeneration => _reconciliation.CurrentGeneration;

        public BattlePresentationCueViewEventHandler(
            EC.IECWorld world,
            IBattleEntityQuery query,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode)
            : this(world, query, vfx, in vfxNode, null)
        {
        }

        internal BattlePresentationCueViewEventHandler(
            EC.IECWorld world,
            IBattleEntityQuery query,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode,
            BattlePresentationCueViewEventHandlerFactory handlers)
        {
            handlers ??= new BattlePresentationCueViewEventHandlerFactory();
            _resolver = handlers.CreateResolver();
            _views = new PresentationCueViewRegistry<
                BattlePresentationCueRequestKey,
                BattlePresentationCueSpawnRequest,
                EC.IEntityId>(
                handlers.CreateViewPort(world, query, vfx, in vfxNode));
        }

        public void HandleSnapshot(PresentationCueData[] entries)
        {
            if (entries == null || entries.Length == 0) return;

            for (int i = 0; i < entries.Length; i++)
            {
                HandleSnapshotEntry(entries[i]);
            }
        }

        private void HandleSnapshotEntry(in PresentationCueData data)
        {
            var decision = _resolver.Resolve(in data);
            var update = BattlePresentationCueReconciliationMapper.CreateUpdate(
                in data,
                in decision,
                _reconciliation.CurrentGeneration);
            var reconciliation = _reconciliation.Process(in update);
            _views.Apply(in reconciliation);
        }

        internal bool BeginReconciliationGeneration(long generation)
        {
            if (generation <= _reconciliation.CurrentGeneration) return false;

            _views.Clear();
            return _reconciliation.BeginGeneration(generation).Applied;
        }

        public void Clear()
        {
            BeginReconciliationGeneration(_reconciliation.CurrentGeneration + 1);
        }

        internal bool TryGetActiveEntityId(
            BattlePresentationCueRequestKey requestKey,
            out EC.IEntityId entityId)
        {
            return _views.TryGetHandle(requestKey, out entityId);
        }
    }

    internal sealed class BattlePresentationCueViewEventHandlerFactory
    {
        public BattlePresentationCueResolver CreateResolver()
        {
            return new BattlePresentationCueResolver();
        }

        public BattlePresentationCueVfxSpawner CreateSpawner(
            EC.IECWorld world,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode)
        {
            return new BattlePresentationCueVfxSpawner(world, vfx, in vfxNode);
        }

        public BattlePresentationCueUnityPort CreateViewPort(
            EC.IECWorld world,
            IBattleEntityQuery query,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode)
        {
            return new BattlePresentationCueUnityPort(
                query,
                CreateSpawner(world, vfx, in vfxNode));
        }
    }
}
