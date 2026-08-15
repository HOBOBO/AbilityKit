#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterRoomSessionController : IDisposable
    {
        private readonly IShooterRoomSession _session;
        private readonly ShooterRoomSessionStore _store;
        private readonly SemaphoreSlim _operationGate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly object _stageGate = new object();
        private CancellationTokenSource? _stageCancellation;
        private ShooterRoomSessionLaunchSpec _launchSpec;
        private bool _hasLaunchSpec;
        private bool _createdRoomOwner;
        private bool _disposed;

        public ShooterRoomSessionController(IShooterRoomSession session, ShooterRoomSessionStore store)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _store.SnapshotChanged += HandleSnapshotChanged;
            _store.RoomChanged += HandleRoomChanged;
            CurrentSnapshot = _store.Current;
        }

        public event Action<ShooterRoomSessionState>? StateChanged;
        public event Action<ShooterRoomSessionSnapshot?>? SnapshotChanged;
        public event Action<ShooterRoomSessionChange>? RoomChanged;

        public ShooterRoomSessionState CurrentState { get; private set; } = ShooterRoomSessionState.Idle;
        public ShooterRoomSessionSnapshot? CurrentSnapshot { get; private set; }
        public string CurrentRoomId { get; private set; } = string.Empty;
        public uint LocalPlayerId { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public bool HasActiveRoom => !string.IsNullOrWhiteSpace(CurrentRoomId);
        public bool IsLocalRoomOwner => CurrentSnapshot?.IsOwner(LocalPlayerId) ?? _createdRoomOwner;
        public bool CanLeaveCurrentRoom => HasActiveRoom && CurrentState != ShooterRoomSessionState.InBattle;

        public Task StartCreateRoomAsync(
            ShooterRoomSessionLaunchSpec spec,
            CancellationToken cancellationToken = default)
        {
            return StartRoomAsync(spec, string.Empty, create: true, cancellationToken);
        }

        public Task StartJoinRoomAsync(
            ShooterRoomSessionLaunchSpec spec,
            string roomId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));
            return StartRoomAsync(spec, roomId, create: false, cancellationToken);
        }

        public Task SetReadyAsync(bool ready, CancellationToken cancellationToken = default)
        {
            return RunPreservingStateAsync(
                async ct =>
                {
                    EnsureState(ShooterRoomSessionState.InLobby);
                    await _session.SetReadyAsync(CurrentRoomId, ready, ct).ConfigureAwait(false);
                },
                cancellationToken);
        }

        public Task BeginLoadingAsync(CancellationToken cancellationToken = default)
        {
            return RunPreservingStateAsync(
                async ct =>
                {
                    EnsureState(ShooterRoomSessionState.InLobby);
                    if (!IsLocalRoomOwner) throw new InvalidOperationException("Only the room owner can begin loading.");
                    if (CurrentSnapshot?.CanStart != true) throw new InvalidOperationException("Room is not ready to begin loading.");
                    await _session.BeginLoadingAsync(CurrentRoomId, CurrentSnapshot.RoomRevision, ct).ConfigureAwait(false);
                    Transition(ShooterRoomSessionState.LoadingAssets);
                },
                cancellationToken);
        }

        public async Task PrepareAssetsAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var stageCancellation = ReplaceStageCancellation(cancellationToken);
            await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_hasLaunchSpec) throw new InvalidOperationException("No active Shooter room launch specification.");
                if (CurrentState != ShooterRoomSessionState.LoadingAssets)
                {
                    throw new InvalidOperationException($"Cannot prepare assets while room flow is {CurrentState}.");
                }

                var snapshot = CurrentSnapshot ?? throw new InvalidOperationException("Loading requires an authoritative room snapshot.");
                await _session.PrepareAndReportAssetsLoadedAsync(
                    _launchSpec,
                    snapshot,
                    stageCancellation.Token).ConfigureAwait(false);
                if (CurrentState == ShooterRoomSessionState.LoadingAssets)
                {
                    Transition(ShooterRoomSessionState.WaitingForBattle);
                }
            }
            catch (OperationCanceledException) when (stageCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                LastError = ex.Message ?? string.Empty;
                throw;
            }
            finally
            {
                ClearStageCancellation(stageCancellation);
                _operationGate.Release();
            }
        }

        public Task WaitForBattleStartAsync(CancellationToken cancellationToken = default)
        {
            return RunPreservingStateAsync(
                async ct =>
                {
                    if (CurrentState != ShooterRoomSessionState.WaitingForBattle &&
                        CurrentState != ShooterRoomSessionState.LoadingAssets)
                    {
                        throw new InvalidOperationException($"Cannot wait for battle while room flow is {CurrentState}.");
                    }

                    var snapshot = await _session.WaitForBattleStartAsync(CurrentRoomId, ct).ConfigureAwait(false);
                    CurrentSnapshot = snapshot;
                    Transition(ShooterRoomSessionState.InBattle);
                },
                cancellationToken);
        }

        public async Task CancelLoadingAsync(CancellationToken cancellationToken = default)
        {
            CancelPendingStage();
            await RunPreservingStateAsync(
                async ct =>
                {
                    if (CurrentState != ShooterRoomSessionState.LoadingAssets &&
                        CurrentState != ShooterRoomSessionState.WaitingForBattle)
                    {
                        throw new InvalidOperationException($"Cannot cancel loading while room flow is {CurrentState}.");
                    }

                    if (!IsLocalRoomOwner) throw new InvalidOperationException("Only the room owner can cancel loading.");
                    await _session.CancelLoadingAsync(CurrentRoomId, CurrentSnapshot?.RoomRevision, ct).ConfigureAwait(false);
                    await _session.RefreshAsync(CurrentRoomId, ct).ConfigureAwait(false);
                    ShooterMultiplayerLoadingStatus.Reset();
                    Transition(ShooterRoomSessionState.InLobby);
                },
                cancellationToken).ConfigureAwait(false);
        }

        public async Task LeaveRoomAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (!CanLeaveCurrentRoom)
            {
                throw new InvalidOperationException($"Cannot leave the room while flow is {CurrentState}.");
            }

            CancelPendingStage();
            await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            var previousState = CurrentState;
            try
            {
                Transition(ShooterRoomSessionState.LeavingRoom);
                await _session.LeaveRoomAsync(CurrentRoomId, CurrentSnapshot?.RoomRevision, cancellationToken).ConfigureAwait(false);
                _store.IgnoreAndReset(CurrentRoomId);
                ShooterMultiplayerLoadingStatus.Reset();
                CurrentRoomId = string.Empty;
                LocalPlayerId = 0u;
                CurrentSnapshot = null;
                LastError = string.Empty;
                _createdRoomOwner = false;
                _hasLaunchSpec = false;
                Transition(ShooterRoomSessionState.Idle);
            }
            catch
            {
                Transition(previousState);
                throw;
            }
            finally
            {
                _operationGate.Release();
            }
        }

        private async Task StartRoomAsync(
            ShooterRoomSessionLaunchSpec spec,
            string roomId,
            bool create,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (CurrentState != ShooterRoomSessionState.Idle && CurrentState != ShooterRoomSessionState.Failed)
                {
                    throw new InvalidOperationException($"Cannot start a room while flow is {CurrentState}.");
                }

                ResetForStart(in spec, create);
                Transition(create ? ShooterRoomSessionState.CreatingRoom : ShooterRoomSessionState.JoiningRoom);
                var joined = create
                    ? await _session.CreateAndJoinAsync(spec, cancellationToken).ConfigureAwait(false)
                    : await _session.JoinAsync(spec, roomId, cancellationToken).ConfigureAwait(false);
                CurrentRoomId = joined.RoomId;
                LocalPlayerId = joined.PlayerId;
                CurrentSnapshot = _store.Current;
                Transition(joined.JoinedRunningBattle || CurrentSnapshot?.Phase == ShooterRoomSessionPhase.InBattle
                    ? ShooterRoomSessionState.InBattle
                    : MapSnapshotState(CurrentSnapshot));
            }
            catch (OperationCanceledException)
            {
                Transition(ShooterRoomSessionState.Idle);
                throw;
            }
            catch (Exception ex)
            {
                LastError = ex.Message ?? string.Empty;
                Transition(ShooterRoomSessionState.Failed);
                throw;
            }
            finally
            {
                _operationGate.Release();
            }
        }

        private async Task RunPreservingStateAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
            await _operationGate.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                LastError = string.Empty;
                await action(linked.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                LastError = ex.Message ?? string.Empty;
                throw;
            }
            finally
            {
                _operationGate.Release();
            }
        }

        private void ResetForStart(in ShooterRoomSessionLaunchSpec spec, bool createdRoomOwner)
        {
            CancelPendingStage();
            _store.Reset();
            ShooterMultiplayerLoadingStatus.Reset();
            _launchSpec = spec;
            _hasLaunchSpec = true;
            _createdRoomOwner = createdRoomOwner;
            CurrentRoomId = string.Empty;
            LocalPlayerId = 0u;
            CurrentSnapshot = null;
            LastError = string.Empty;
        }

        private void HandleSnapshotChanged(ShooterRoomSessionSnapshot? snapshot)
        {
            if (_disposed) return;
            if (snapshot != null && !HasActiveRoom &&
                CurrentState != ShooterRoomSessionState.CreatingRoom &&
                CurrentState != ShooterRoomSessionState.JoiningRoom)
            {
                return;
            }

            if (snapshot != null && HasActiveRoom &&
                !string.Equals(snapshot.RoomId, CurrentRoomId, StringComparison.Ordinal))
            {
                return;
            }

            CurrentSnapshot = snapshot;
            SnapshotChanged?.Invoke(snapshot);
            if (snapshot == null || CurrentState == ShooterRoomSessionState.CreatingRoom ||
                CurrentState == ShooterRoomSessionState.JoiningRoom || CurrentState == ShooterRoomSessionState.LeavingRoom)
            {
                return;
            }

            if (snapshot.Phase == ShooterRoomSessionPhase.Lobby &&
                CurrentState is ShooterRoomSessionState.LoadingAssets or ShooterRoomSessionState.WaitingForBattle)
            {
                CancelPendingStage();
                ShooterMultiplayerLoadingStatus.Reset();
            }

            Transition(MapSnapshotState(snapshot));
        }

        private void HandleRoomChanged(ShooterRoomSessionChange change)
        {
            if (_disposed) return;
            RoomChanged?.Invoke(change);
        }

        private ShooterRoomSessionState MapSnapshotState(ShooterRoomSessionSnapshot? snapshot)
        {
            if (snapshot == null) return ShooterRoomSessionState.InLobby;
            return snapshot.Phase switch
            {
                ShooterRoomSessionPhase.Lobby => ShooterRoomSessionState.InLobby,
                ShooterRoomSessionPhase.Loading => snapshot.FindMember(LocalPlayerId)?.AssetsLoaded == true
                    ? ShooterRoomSessionState.WaitingForBattle
                    : ShooterRoomSessionState.LoadingAssets,
                ShooterRoomSessionPhase.Starting => ShooterRoomSessionState.WaitingForBattle,
                ShooterRoomSessionPhase.InBattle => ShooterRoomSessionState.InBattle,
                _ => ShooterRoomSessionState.Failed
            };
        }

        private CancellationTokenSource ReplaceStageCancellation(CancellationToken cancellationToken)
        {
            lock (_stageGate)
            {
                _stageCancellation?.Cancel();
                _stageCancellation?.Dispose();
                _stageCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    _lifetime.Token,
                    cancellationToken);
                return _stageCancellation;
            }
        }

        private void ClearStageCancellation(CancellationTokenSource stageCancellation)
        {
            lock (_stageGate)
            {
                if (!ReferenceEquals(_stageCancellation, stageCancellation)) return;
                _stageCancellation = null;
            }

            stageCancellation.Dispose();
        }

        private void CancelPendingStage()
        {
            lock (_stageGate) _stageCancellation?.Cancel();
        }

        private void EnsureState(ShooterRoomSessionState expected)
        {
            if (CurrentState != expected)
            {
                throw new InvalidOperationException($"Expected room flow {expected}, current state is {CurrentState}.");
            }
        }

        private void Transition(ShooterRoomSessionState next)
        {
            if (CurrentState == next) return;
            CurrentState = next;
            StateChanged?.Invoke(next);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShooterRoomSessionController));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _store.SnapshotChanged -= HandleSnapshotChanged;
            _store.RoomChanged -= HandleRoomChanged;
            _lifetime.Cancel();
            CancelPendingStage();
            _session.Dispose();
            _store.Dispose();
            _lifetime.Dispose();
            StateChanged = null;
            SnapshotChanged = null;
            RoomChanged = null;
        }
    }
}
