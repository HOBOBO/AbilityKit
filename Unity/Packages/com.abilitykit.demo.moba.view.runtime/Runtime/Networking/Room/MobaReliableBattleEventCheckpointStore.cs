using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;

namespace AbilityKit.Game.Flow
{
    public interface IRoomDepartureObserver
    {
        Task OnRoomLeftAsync(string battleId);
    }

    public interface IMobaReliableEventCheckpointLifecycle : IMobaReliableBattleEventCheckpointStore
    {
        Task<ReliableEventCheckpointFlushResult> FlushAsync(
            ReliableEventCheckpointFlushTrigger trigger,
            CancellationToken cancellationToken = default);
    }

    internal sealed class MobaReliableBattleEventCheckpointStore :
        IMobaReliableEventCheckpointLifecycle,
        IRoomDepartureObserver,
        IDisposable
    {
        private readonly IReliableEventCheckpointStore _store;
        private readonly IDisposable _ownedStore;
        private readonly ReliableEventCheckpointLifecycleCoordinator _lifecycle;

        public MobaReliableBattleEventCheckpointStore(
            IReliableEventCheckpointStore store = null,
            bool ownsStore = false,
            ReliableEventCheckpointLifecycleOptions lifecycleOptions = null)
        {
            _store = store ?? new InMemoryReliableEventCheckpointStore();
            _ownedStore = ownsStore ? _store as IDisposable : null;
            _lifecycle = new ReliableEventCheckpointLifecycleCoordinator(
                _store, lifecycleOptions);
        }

        public bool TryLoad(
            string battleId,
            out MobaReliableBattleEventCheckpoint checkpoint)
        {
            if (_store.TryLoad(battleId, out var value))
            {
                checkpoint = new MobaReliableBattleEventCheckpoint(
                    value.StreamId,
                    value.TimelineId,
                    value.LastAcknowledgedSequence);
                return true;
            }

            checkpoint = default;
            return false;
        }

        public void Save(in MobaReliableBattleEventCheckpoint checkpoint)
        {
            if (!checkpoint.IsValid) return;

            var value = new ReliableEventCheckpoint(
                checkpoint.BattleId,
                checkpoint.Epoch,
                checkpoint.LastAcknowledgedSequence);
            _store.Save(in value);
        }

        public bool Remove(string battleId) => _store.Remove(battleId);

        public Task OnRoomLeftAsync(string battleId)
        {
            if (!string.IsNullOrWhiteSpace(battleId)) Remove(battleId);
            return FlushAsync(ReliableEventCheckpointFlushTrigger.RoomLeave,
                CancellationToken.None);
        }

        public Task FlushAsync(CancellationToken cancellationToken = default) =>
            _lifecycle.FlushAsync(ReliableEventCheckpointFlushTrigger.Manual,
                cancellationToken);

        public Task<ReliableEventCheckpointFlushResult> FlushAsync(
            ReliableEventCheckpointFlushTrigger trigger,
            CancellationToken cancellationToken = default) =>
            _lifecycle.FlushAsync(trigger, cancellationToken);

        public ReliableEventCheckpointLifecycleDiagnostics LifecycleDiagnostics =>
            _lifecycle.GetDiagnostics();

        public event Action<ReliableEventCheckpointLifecycleFailure> LifecycleFailure
        {
            add => _lifecycle.Failure += value;
            remove => _lifecycle.Failure -= value;
        }

        public void Dispose()
        {
            try
            {
                _lifecycle.FlushAsync(
                    ReliableEventCheckpointFlushTrigger.Dispose).GetAwaiter().GetResult();
            }
            finally
            {
                _ownedStore?.Dispose();
            }
        }
    }
}
