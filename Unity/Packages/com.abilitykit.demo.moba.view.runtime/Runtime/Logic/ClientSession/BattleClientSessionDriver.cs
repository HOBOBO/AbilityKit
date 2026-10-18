using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Battle.Agent;

namespace AbilityKit.Game.Flow
{
    public interface IBattleClientSessionHost
    {
        Task StartAsync();
        Task StopAsync();
        void Tick(float deltaTime);
    }

    public interface IBattleClientProjection
    {
        void Apply(in GatewayStateSyncSnapshot snapshot);
        void Reset();
    }

    public enum BattleClientSessionStatus
    {
        Stopped,
        Starting,
        Running,
        Recovering,
        Stopping,
        Faulted
    }

    /// <summary>
    /// Shared client lifecycle and snapshot projection boundary. Network callbacks may queue
    /// snapshots from another thread; Tick applies them on the host's presentation thread.
    /// </summary>
    public sealed class BattleClientSessionDriver : IDisposable
    {
        private readonly IBattleClientSessionHost _host;
        private readonly SemaphoreSlim _lifecycleGate = new SemaphoreSlim(1, 1);
        private readonly BattleClientSnapshotStream _snapshots;
        private bool _disposed;

        public BattleClientSessionDriver(
            IBattleClientSessionHost host,
            BattleClientSessionOptions options = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _snapshots = new BattleClientSnapshotStream(
                (options ?? new BattleClientSessionOptions()).MaxPendingSnapshots);
        }

        public BattleClientSessionStatus Status { get; private set; }
        public ulong WorldId => _snapshots.WorldId;
        public int LastSnapshotFrame => _snapshots.LastSnapshotFrame;
        public int ProjectionCount => _snapshots.ProjectionCount;
        public int PendingSnapshotCount => _snapshots.PendingSnapshotCount;
        public long DiscardedPendingSnapshotCount =>
            _snapshots.DiscardedPendingSnapshotCount;
        public bool RequiresFullSnapshot => _snapshots.RequiresFullSnapshot;
        public event Action FullSnapshotRequired;

        public async Task StartAsync()
        {
            ThrowIfDisposed();
            await _lifecycleGate.WaitAsync();
            try
            {
                ThrowIfDisposed();
                if (Status == BattleClientSessionStatus.Running ||
                    Status == BattleClientSessionStatus.Recovering) return;
                if (Status == BattleClientSessionStatus.Faulted)
                {
                    await _host.StopAsync();
                    _snapshots.ResetProjections();
                }

                _snapshots.Reset();
                Status = BattleClientSessionStatus.Starting;
                try
                {
                    await _host.StartAsync();
                    Status = BattleClientSessionStatus.Running;
                }
                catch
                {
                    Status = BattleClientSessionStatus.Faulted;
                    throw;
                }
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }

        public async Task StopAsync()
        {
            if (_disposed) return;
            await _lifecycleGate.WaitAsync();
            try
            {
                if (Status == BattleClientSessionStatus.Stopped) return;
                Status = BattleClientSessionStatus.Stopping;
                try
                {
                    await _host.StopAsync();
                    Status = BattleClientSessionStatus.Stopped;
                }
                catch
                {
                    Status = BattleClientSessionStatus.Faulted;
                    throw;
                }
                finally
                {
                    _snapshots.Reset();
                    _snapshots.ResetProjections();
                }
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }

        public void Tick(float deltaTime)
        {
            ThrowIfDisposed();
            if (Status != BattleClientSessionStatus.Running &&
                Status != BattleClientSessionStatus.Recovering) return;
            if (_snapshots.Drain() && _snapshots.RequiresFullSnapshot)
                EnterRecovery();
            UpdateRecoveryStatus();
            _host.Tick(deltaTime);
            if (_snapshots.Drain() && _snapshots.RequiresFullSnapshot)
                EnterRecovery();
            UpdateRecoveryStatus();
        }

        public void QueueSnapshot(in GatewayStateSyncSnapshot snapshot)
        {
            ThrowIfDisposed();
            _snapshots.Queue(in snapshot);
        }

        public bool PresentSnapshot(in GatewayStateSyncSnapshot snapshot)
        {
            return PresentSnapshotCore(in snapshot, allowSameFrame: false);
        }

        public bool PresentInterpolatedSnapshot(in GatewayStateSyncSnapshot snapshot)
        {
            return PresentSnapshotCore(in snapshot, allowSameFrame: true);
        }

        private bool PresentSnapshotCore(
            in GatewayStateSyncSnapshot snapshot,
            bool allowSameFrame)
        {
            ThrowIfDisposed();
            if (Status != BattleClientSessionStatus.Running &&
                Status != BattleClientSessionStatus.Recovering) return false;
            ProcessRecoverySignal();
            var accepted = _snapshots.Present(in snapshot, allowSameFrame);
            UpdateRecoveryStatus();
            return accepted;
        }

        public void NotifyConnectionLost()
        {
            ThrowIfDisposed();
            _snapshots.NotifyConnectionLost();
        }

        private void ProcessRecoverySignal()
        {
            if (_snapshots.ConsumeRecoverySignal()) EnterRecovery();
        }

        private void EnterRecovery()
        {
            if (Status != BattleClientSessionStatus.Running &&
                Status != BattleClientSessionStatus.Recovering) return;
            Status = BattleClientSessionStatus.Recovering;
            FullSnapshotRequired?.Invoke();
        }

        private void UpdateRecoveryStatus()
        {
            if (Status == BattleClientSessionStatus.Recovering &&
                !_snapshots.RequiresFullSnapshot)
                Status = BattleClientSessionStatus.Running;
        }

        public void AttachProjection(string id, IBattleClientProjection projection)
        {
            ThrowIfDisposed();
            _snapshots.AttachProjection(id, projection);
        }

        public bool DetachProjection(string id)
        {
            ThrowIfDisposed();
            return _snapshots.DetachProjection(id);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BattleClientSessionDriver));
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (Status != BattleClientSessionStatus.Stopped)
                StopAsync().GetAwaiter().GetResult();
            _disposed = true;
            _lifecycleGate.Dispose();
        }
    }

}
