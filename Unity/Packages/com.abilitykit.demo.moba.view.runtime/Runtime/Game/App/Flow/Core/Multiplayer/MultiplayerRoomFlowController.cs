#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 多人房间流程控制器：编排 登录→建房/入房→选英雄→Ready→BeginLoading→ReportAssets→WaitForBattle。
    /// <para>
    /// 纯 C#，零 Unity 依赖。通过 <see cref="IMultiplayerRoomSession"/> 与 <see cref="IRoomSnapshotProvider"/>
    /// 抽象与外部（RoomGatewaySessionFlow / ClientRoomStore）交互，使其可在无 Unity/host.extension 的测试项目中测试。
    /// </para>
    /// </summary>
    internal sealed class MultiplayerRoomFlowController : IDisposable
    {
        private readonly IMultiplayerRoomSession _session;
        private readonly IRoomSnapshotProvider _snapshotProvider;
        private readonly MultiplayerRoomStageRuntime _stageRuntime =
            new MultiplayerRoomStageRuntime();
        private readonly MultiplayerAssetLoadingRuntime _assetRuntime;
        private bool _disposed;
        private bool _createdRoomOwner;

        /// <summary>状态变更回调。每次 <see cref="CurrentState"/> 变化时触发。</summary>
        public event Action<MultiplayerRoomFlowState>? StateChanged;

        /// <summary>当前状态。</summary>
        public MultiplayerRoomFlowState CurrentState { get; private set; }

        /// <summary>当前房间快照（从 IRoomSnapshotProvider 投影）。</summary>
        public MultiplayerRoomSnapshot? CurrentSnapshot { get; private set; }

        /// <summary>最近一次错误信息（进入 Failed 状态时设置）。</summary>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>当前房间 Id（创建/加入成功后设置）。</summary>
        public string CurrentRoomId { get; private set; } = string.Empty;

        public uint LocalPlayerId { get; private set; }

        public string LocalAccountId { get; private set; } = string.Empty;

        public bool IsLocalRoomOwner
        {
            get
            {
                var snapshot = CurrentSnapshot;
                if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.OwnerAccountId))
                {
                    if (!string.IsNullOrWhiteSpace(LocalAccountId))
                    {
                        return string.Equals(
                            LocalAccountId,
                            snapshot.OwnerAccountId,
                            StringComparison.Ordinal);
                    }

                    if (LocalPlayerId == 0u || snapshot.Players == null)
                    {
                        return _createdRoomOwner;
                    }

                    for (var i = 0; i < snapshot.Players.Count; i++)
                    {
                        var player = snapshot.Players[i];
                        if (player.PlayerId == LocalPlayerId)
                        {
                            return string.Equals(
                                player.AccountId,
                                snapshot.OwnerAccountId,
                                StringComparison.Ordinal);
                        }
                    }
                }

                return _createdRoomOwner;
            }
        }

        public bool CanLeaveCurrentRoom
        {
            get
            {
                if (string.IsNullOrWhiteSpace(CurrentRoomId)) return false;
                var phase = CurrentSnapshot?.Phase;
                return phase == MultiplayerRoomPhase.Lobby ||
                       phase == MultiplayerRoomPhase.Loading;
            }
        }

        public MultiplayerRoomRestoreResult? LastRestoreResult { get; private set; }

        public MultiplayerRoomLaunchSpec? CurrentLaunchSpec { get; private set; }

        public int LocalLoadingProgress => _assetRuntime.Progress;

        public string CurrentLoadingAssetKey => _assetRuntime.CurrentAssetKey;

        public MultiplayerRoomFlowController(
            IMultiplayerRoomSession session,
            IRoomSnapshotProvider snapshotProvider,
            IMultiplayerBattleAssetLoader? assetLoader = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
            _assetRuntime = new MultiplayerAssetLoadingRuntime(assetLoader);
            _snapshotProvider.OnSnapshotChanged += HandleSnapshotChanged;
            CurrentSnapshot = _snapshotProvider.Current;
        }

        /// <summary>
        /// 启动创建房间流程：Idle → LoggingIn → CreatingRoom → InLobby。
        /// </summary>
        public async Task StartCreateRoomAsync(MultiplayerRoomLaunchSpec spec, CancellationToken cancellationToken = default)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            CurrentLaunchSpec = spec;
            LocalAccountId = spec.AccountId?.Trim() ?? string.Empty;
            _createdRoomOwner = true;
            LocalPlayerId = 0u;
            await RunAsync(
                async ct =>
                {
                    Transition(MultiplayerRoomFlowState.LoggingIn);
                    Transition(MultiplayerRoomFlowState.CreatingRoom);
                    var roomId = await _session.CreateRoomAsync(spec, ct).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(roomId))
                    {
                        throw new InvalidOperationException("创建房间成功但未返回 roomId。");
                    }

                    var joined = await _session.JoinRoomAsync(spec, roomId, ct).ConfigureAwait(false);
                    ApplyJoinResult(roomId, in joined);
                    Transition(MultiplayerRoomFlowState.InLobby);
                },
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 启动加入房间流程：Idle → LoggingIn → JoiningRoom → InLobby。
        /// </summary>
        public async Task StartJoinRoomAsync(MultiplayerRoomLaunchSpec spec, string roomId, CancellationToken cancellationToken = default)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId 不能为空。", nameof(roomId));
            CurrentLaunchSpec = spec;
            LocalAccountId = spec.AccountId?.Trim() ?? string.Empty;
            _createdRoomOwner = false;
            LocalPlayerId = 0u;
            await RunAsync(
                async ct =>
                {
                    Transition(MultiplayerRoomFlowState.LoggingIn);
                    Transition(MultiplayerRoomFlowState.JoiningRoom);
                    var joined = await _session.JoinRoomAsync(spec, roomId, ct).ConfigureAwait(false);
                    ApplyJoinResult(roomId, in joined);
                    Transition(MultiplayerRoomFlowState.InLobby);
                },
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<MultiplayerRoomRestoreResult> RestoreAsync(
            MultiplayerRoomLaunchSpec spec,
            uint fallbackPlayerId,
            CancellationToken cancellationToken = default)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (fallbackPlayerId == 0u) throw new ArgumentOutOfRangeException(nameof(fallbackPlayerId));
            CurrentLaunchSpec = spec;
            LocalAccountId = spec.AccountId?.Trim() ?? string.Empty;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                LastError = string.Empty;
                Transition(MultiplayerRoomFlowState.LoggingIn);
                var restored = await _session.RestoreAsync(
                    spec,
                    fallbackPlayerId,
                    cancellationToken).ConfigureAwait(false);
                LastRestoreResult = restored;
                _createdRoomOwner = false;

                if (!restored.HasActiveRoom)
                {
                    CurrentRoomId = string.Empty;
                    LocalPlayerId = 0u;
                    CurrentSnapshot = null;
                    if (restored.Status == MultiplayerRoomRestoreStatus.NoActiveRoom)
                    {
                        Transition(MultiplayerRoomFlowState.Idle);
                    }
                    else
                    {
                        Fail(string.IsNullOrWhiteSpace(restored.Message)
                            ? $"Room restore failed: {restored.Status}/{restored.ErrorCode}."
                            : restored.Message);
                    }

                    return restored;
                }

                if (restored.PlayerId == 0u)
                {
                    throw new InvalidOperationException(
                        "Room restore succeeded without an authoritative player id.");
                }

                CurrentRoomId = restored.RoomId;
                LocalPlayerId = restored.PlayerId;
                var nextState = MapRestoreNextStepToState(restored.NextStep);
                if (nextState == MultiplayerRoomFlowState.Failed)
                {
                    Fail(string.IsNullOrWhiteSpace(restored.Message)
                        ? $"Room restore cannot continue from phase {restored.Phase}."
                        : restored.Message);
                }
                else
                {
                    Transition(nextState);
                    StartPendingStage();
                }
                return restored;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 选英雄/配置出战。仅在 InLobby 状态可用。
        /// </summary>
        public Task PickHeroAsync(MultiplayerLoadoutSpec loadout, CancellationToken cancellationToken = default)
        {
            return RunAsync(
                async ct =>
                {
                    EnsureState(MultiplayerRoomFlowState.InLobby);
                    await _session.ConfigureLoadoutAsync(CurrentRoomId, loadout, ct).ConfigureAwait(false);
                },
                cancellationToken);
        }

        /// <summary>
        /// 设置准备状态。仅在 InLobby 状态可用。
        /// </summary>
        public Task SetReadyAsync(bool ready, CancellationToken cancellationToken = default)
        {
            return RunAsync(
                async ct =>
                {
                    EnsureState(MultiplayerRoomFlowState.InLobby);
                    await _session.SetReadyAsync(CurrentRoomId, ready, ct).ConfigureAwait(false);
                },
                cancellationToken);
        }

        /// <summary>
        /// Owner 发起资源加载：InLobby → LoadingAssets。
        /// </summary>
        public Task BeginLoadingAsync(CancellationToken cancellationToken = default)
        {
            return RunAsync(
                async ct =>
                {
                    EnsureState(MultiplayerRoomFlowState.InLobby);
                    if (!IsLocalRoomOwner)
                    {
                        throw new InvalidOperationException("Only the room owner can begin loading.");
                    }
                    if (CurrentSnapshot?.CanStart != true)
                    {
                        throw new InvalidOperationException("Room is not ready to begin loading.");
                    }
                    await _session.BeginLoadingAsync(CurrentRoomId, ct).ConfigureAwait(false);
                    Transition(MultiplayerRoomFlowState.LoadingAssets);
                    if (_assetRuntime.IsAvailable)
                    {
                        await ResumePendingStageAsync(ct).ConfigureAwait(false);
                    }
                },
                cancellationToken);
        }

        /// <summary>
        /// 成员上报资源加载完成：LoadingAssets → WaitingForBattle。
        /// </summary>
        public Task ReportAssetsLoadedAsync(CancellationToken cancellationToken = default)
        {
            return RunAsync(
                async ct =>
                {
                    EnsureState(MultiplayerRoomFlowState.LoadingAssets);
                    await _session.ReportAssetsLoadedAsync(CurrentRoomId, ct).ConfigureAwait(false);
                    Transition(MultiplayerRoomFlowState.WaitingForBattle);
                },
                cancellationToken);
        }

        public Task CancelLoadingAsync(CancellationToken cancellationToken = default)
        {
            return RunAsync(
                async ct =>
                {
                    if (CurrentState != MultiplayerRoomFlowState.LoadingAssets &&
                        CurrentState != MultiplayerRoomFlowState.WaitingForBattle)
                    {
                        throw new InvalidOperationException(
                            $"Cannot cancel loading while flow is {CurrentState}.");
                    }

                    if (!IsLocalRoomOwner)
                    {
                        throw new InvalidOperationException("Only the room owner can cancel loading.");
                    }

                    CancelPendingStage(releaseAssets: true);
                    await _session.CancelLoadingAsync(CurrentRoomId, ct).ConfigureAwait(false);
                    Transition(MultiplayerRoomFlowState.InLobby);
                },
                cancellationToken);
        }

        public async Task LeaveRoomAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanLeaveCurrentRoom)
            {
                throw new InvalidOperationException(
                    $"Cannot leave the room while flow is {CurrentState} and phase is {CurrentSnapshot?.Phase}.");
            }

            var previousState = CurrentState;
            LastError = string.Empty;
            Transition(MultiplayerRoomFlowState.LeavingRoom);
            try
            {
                await _session.LeaveRoomAsync(CurrentRoomId, cancellationToken).ConfigureAwait(false);
                CancelPendingStage(releaseAssets: true);
                CurrentSnapshot = null;
                CurrentRoomId = string.Empty;
                LocalPlayerId = 0u;
                LocalAccountId = string.Empty;
                _createdRoomOwner = false;
                LastRestoreResult = null;
                CurrentLaunchSpec = null;
                Transition(MultiplayerRoomFlowState.Idle);
            }
            catch (OperationCanceledException)
            {
                Transition(previousState);
                throw;
            }
            catch (Exception ex)
            {
                LastError = ex.Message ?? string.Empty;
                Transition(previousState);
                throw;
            }
        }

        /// <summary>
        /// 等待服务端开战：WaitingForBattle → InBattle。
        /// </summary>
        public Task WaitForBattleStartAsync(CancellationToken cancellationToken = default)
        {
            return RunAsync(
                async ct =>
                {
                    EnsureState(MultiplayerRoomFlowState.WaitingForBattle);
                    await _session.WaitForBattleStartAsync(CurrentRoomId, ct).ConfigureAwait(false);
                    Transition(MultiplayerRoomFlowState.InBattle);
                },
                cancellationToken);
        }

        /// <summary>
        /// 取消当前流程，回到 Idle。
        /// </summary>
        public void Cancel()
        {
            CancelPendingStage(releaseAssets: true);
            CurrentSnapshot = null;
            CurrentRoomId = string.Empty;
            LocalPlayerId = 0u;
            LocalAccountId = string.Empty;
            _createdRoomOwner = false;
            LastError = string.Empty;
            LastRestoreResult = null;
            CurrentLaunchSpec = null;
            Transition(MultiplayerRoomFlowState.Idle);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CancelPendingStage(releaseAssets: true);
            _snapshotProvider.OnSnapshotChanged -= HandleSnapshotChanged;
            _stageRuntime.Dispose();
        }

        public Task ResumePendingStageAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (CurrentState == MultiplayerRoomFlowState.InBattle)
            {
                return Task.CompletedTask;
            }

            var snapshot = CurrentSnapshot;
            if (snapshot == null) return Task.CompletedTask;
            if (snapshot.Phase != MultiplayerRoomPhase.Loading &&
                snapshot.Phase != MultiplayerRoomPhase.Starting)
            {
                return Task.CompletedTask;
            }

            return _stageRuntime.ResumeAsync(
                snapshot.LaunchGeneration,
                ct => ResumePendingStageCoreAsync(snapshot, ct),
                cancellationToken);
        }

        private async Task ResumePendingStageCoreAsync(
            MultiplayerRoomSnapshot initialSnapshot,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Yield();
                if (initialSnapshot.Phase == MultiplayerRoomPhase.Loading)
                {
                    if (initialSnapshot.LaunchGeneration <= 0)
                    {
                        throw new InvalidOperationException("Loading snapshot has no launch generation.");
                    }

                    if (initialSnapshot.LoadingDeadlineUnixMs > 0 &&
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= initialSnapshot.LoadingDeadlineUnixMs)
                    {
                        throw new TimeoutException("The room loading deadline has elapsed.");
                    }

                    Transition(MultiplayerRoomFlowState.LoadingAssets);
                    await _assetRuntime.LoadAsync(
                        initialSnapshot,
                        (value, ct) => _session.ReportLoadingProgressAsync(
                            initialSnapshot.RoomId,
                            value,
                            ct),
                        cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();

                    var current = CurrentSnapshot;
                    if (current == null ||
                        current.Phase != MultiplayerRoomPhase.Loading ||
                        current.LaunchGeneration != initialSnapshot.LaunchGeneration)
                    {
                        return;
                    }

                    await _session.ReportAssetsLoadedAsync(CurrentRoomId, cancellationToken).ConfigureAwait(false);
                }

                var latest = CurrentSnapshot;
                if (latest == null ||
                    latest.LaunchGeneration != initialSnapshot.LaunchGeneration ||
                    (latest.Phase != MultiplayerRoomPhase.Loading &&
                     latest.Phase != MultiplayerRoomPhase.Starting))
                {
                    return;
                }

                Transition(MultiplayerRoomFlowState.WaitingForBattle);
                await _session.WaitForBattleStartAsync(CurrentRoomId, cancellationToken).ConfigureAwait(false);
                Transition(MultiplayerRoomFlowState.InBattle);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
            }
        }

        private void StartPendingStage()
        {
            if (!_assetRuntime.IsAvailable) return;
            _ = ResumePendingStageAsync();
        }

        private void CancelPendingStage(bool releaseAssets)
        {
            _stageRuntime.Cancel();
            _assetRuntime.Cancel(releaseAssets);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MultiplayerRoomFlowController));
        }

        private void ApplyJoinResult(
            string requestedRoomId,
            in MultiplayerRoomJoinResult result)
        {
            if (result.PlayerId == 0u)
            {
                throw new InvalidOperationException(
                    "Room join succeeded without an authoritative player id.");
            }

            if (!string.IsNullOrWhiteSpace(result.RoomId) &&
                !string.Equals(result.RoomId, requestedRoomId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Room join returned unexpected room id '{result.RoomId}' for '{requestedRoomId}'.");
            }

            CurrentRoomId = string.IsNullOrWhiteSpace(result.RoomId)
                ? requestedRoomId
                : result.RoomId;
            LocalPlayerId = result.PlayerId;
        }

        /// <summary>
        /// 从快照恢复：根据当前快照 Phase 推断控制器状态。
        /// </summary>
        public void RestoreFromSnapshot()
        {
            var snapshot = _snapshotProvider.Current;
            if (snapshot == null)
            {
                Cancel();
                return;
            }

            CurrentSnapshot = snapshot;
            CurrentRoomId = snapshot.RoomId;
            Transition(MapPhaseToState(snapshot.Phase));
        }

        private async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await action(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
                throw;
            }
        }

        private void HandleSnapshotChanged(MultiplayerRoomSnapshot snapshot)
        {
            var previous = CurrentSnapshot;
            CurrentSnapshot = snapshot;
            if (!string.IsNullOrEmpty(snapshot.RoomId) && string.IsNullOrEmpty(CurrentRoomId))
            {
                CurrentRoomId = snapshot.RoomId;
            }

            var loadingTimedOut = previous != null &&
                                  (previous.Phase == MultiplayerRoomPhase.Loading ||
                                   previous.Phase == MultiplayerRoomPhase.Starting) &&
                                  snapshot.Phase == MultiplayerRoomPhase.Lobby &&
                                  string.Equals(
                                      snapshot.PhaseReason,
                                      "LoadingTimeout",
                                      StringComparison.Ordinal);
            if (loadingTimedOut)
            {
                LastError = "Room loading timed out before all players finished loading.";
            }
            else if (snapshot.Phase == MultiplayerRoomPhase.Loading)
            {
                LastError = string.Empty;
            }

            // 仅在活跃流程中根据服务端 Phase 同步状态，避免覆盖用户驱动的中间态（LoggingIn/CreatingRoom 等）。
            if (IsActiveFlowState(CurrentState))
            {
                var mapped = MapPhaseToState(snapshot.Phase);
                if (mapped != CurrentState)
                {
                    Transition(mapped);
                }
            }

            if (previous != null &&
                previous.LaunchGeneration != snapshot.LaunchGeneration)
            {
                CancelPendingStage(releaseAssets: true);
            }

            if (snapshot.Phase == MultiplayerRoomPhase.Loading ||
                snapshot.Phase == MultiplayerRoomPhase.Starting)
            {
                StartPendingStage();
            }
            else if (snapshot.Phase == MultiplayerRoomPhase.Lobby)
            {
                CancelPendingStage(releaseAssets: true);
            }
        }

        private void Transition(MultiplayerRoomFlowState next)
        {
            if (CurrentState == next) return;
            CurrentState = next;
            StateChanged?.Invoke(next);
        }

        private void Fail(string message)
        {
            LastError = message ?? string.Empty;
            Transition(MultiplayerRoomFlowState.Failed);
        }

        private void EnsureState(MultiplayerRoomFlowState expected)
        {
            if (CurrentState != expected)
            {
                throw new InvalidOperationException(
                    $"当前状态不支持该操作：期望 {expected}，实际 {CurrentState}。");
            }
        }

        private static bool IsActiveFlowState(MultiplayerRoomFlowState state)
        {
            return state == MultiplayerRoomFlowState.InLobby ||
                   state == MultiplayerRoomFlowState.LoadingAssets ||
                   state == MultiplayerRoomFlowState.WaitingForBattle ||
                   state == MultiplayerRoomFlowState.InBattle;
        }

        private static MultiplayerRoomFlowState MapPhaseToState(MultiplayerRoomPhase phase)
        {
            switch (phase)
            {
                case MultiplayerRoomPhase.Lobby:
                    return MultiplayerRoomFlowState.InLobby;
                case MultiplayerRoomPhase.Loading:
                    return MultiplayerRoomFlowState.LoadingAssets;
                case MultiplayerRoomPhase.Starting:
                    return MultiplayerRoomFlowState.WaitingForBattle;
                case MultiplayerRoomPhase.InBattle:
                    return MultiplayerRoomFlowState.InBattle;
                case MultiplayerRoomPhase.Closed:
                case MultiplayerRoomPhase.Expired:
                case MultiplayerRoomPhase.Closing:
                    return MultiplayerRoomFlowState.Failed;
                default:
                    return MultiplayerRoomFlowState.Idle;
            }
        }

        private static MultiplayerRoomFlowState MapRestoreNextStepToState(
            MultiplayerRoomRestoreNextStep nextStep)
        {
            switch (nextStep)
            {
                case MultiplayerRoomRestoreNextStep.SetReadyAndBeginLoading:
                    return MultiplayerRoomFlowState.InLobby;
                case MultiplayerRoomRestoreNextStep.ReportAssetsLoaded:
                    return MultiplayerRoomFlowState.LoadingAssets;
                case MultiplayerRoomRestoreNextStep.WaitForBattleStart:
                    return MultiplayerRoomFlowState.WaitingForBattle;
                case MultiplayerRoomRestoreNextStep.EnterBattle:
                    return MultiplayerRoomFlowState.InBattle;
                default:
                    return MultiplayerRoomFlowState.Failed;
            }
        }
    }
}
