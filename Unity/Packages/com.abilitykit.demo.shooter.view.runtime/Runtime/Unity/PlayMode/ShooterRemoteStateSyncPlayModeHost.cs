#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View.Hosting;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Shooter;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace AbilityKit.Demo.Shooter.View.PlayMode
{
    /// <summary>
    /// 远程 Shooter 状态同步演示的 Unity Play-mode host。
    /// <para>
    /// 框架包提供世界、会话与状态同步原语；该类型把示例特定的 Unity PlayerLoop、重连流程、输入泵和
    /// GameObject 渲染组合保留在 Shooter 示例层。
    /// </para>
    /// </summary>
    public static class ShooterRemoteStateSyncPlayModeHost
    {
        private static readonly UnityShooterPlayInputSource InputSource = new();
        private static readonly UnityShooterSwitchableViewSink ViewSink = new();
        private static readonly ShooterRemoteInputPump InputPump = new(InputSource);
        private static readonly ReconnectAttemptScheduler SessionReconnectScheduler = new();
        private static ShooterRemoteStateSyncRuntimeState? _state;
        private static ShooterRemoteInputSubmitStrategy? _inputSubmitStrategy;
        private static ShooterRemoteStateSyncLaunchOptions _options;
        private static int _effectiveControlledPlayerId;
        private static ShooterRemoteStateSyncLaunchOptions _pausedResumeOptions;
        private static ShooterRemoteStateSyncConnectionResult? _lastConnectionResult;
        private static ShooterHostFrameInput _lastInput;
        private static ShooterClientInputSubmitResult _lastSubmitResult;
        private static ShooterClientFrameTickResult _lastTickResult;
        private static bool _playerLoopInstalled;
        private static bool _isStarting;
        private static bool _isPaused;
        private static bool _isAutoReconnecting;
        private static bool _isAutoReconnectAttemptRunning;
        private static bool _isWaitingForInitialFullStateSync;
        private static ShooterSnapshotApplyResult _lastInitialFullStateSyncApplyResult;
        private static Exception? _lastError;
        private static long _lifecycleGeneration;
        private static long _stepCount;
        private static long _renderCount;
        private static ShooterTimeAnchorCoordinator? _timeAnchors;
        private static SyncTimeAnchor _lastRemoteTimeAnchor;
        private static ShooterRemoteLatencyCompensationDiagnostics _lastRemoteLatencyCompensationDiagnostics;

        public static event Action? StateChanged;

        public static bool IsInstalled => _playerLoopInstalled;
        public static bool IsRunning => _state != null;
        public static bool IsStarting => _isStarting;
        public static bool IsPaused => _isPaused;
        public static bool IsAutoReconnecting => _isAutoReconnecting;
        public static bool IsWaitingForInitialFullStateSync => _isWaitingForInitialFullStateSync;
        public static ShooterSnapshotApplyResult LastInitialFullStateSyncApplyResult => _lastInitialFullStateSyncApplyResult;
        public static Exception? LastError => _lastError;
        public static ShooterClientNetworkLaunchResult? Launch => _state?.Launch;
        public static ShooterRemoteStateSyncConnectionResult? LastConnectionResult => _lastConnectionResult;
        public static ShooterClientSession? Session => _state?.Launch.Session;
        public static ShooterClientBattleHandle? Battle => _state?.Launch.Battle;
        public static ShooterRoomGatewayFlowResult? Flow => _state?.Launch.Flow;
        public static ShooterPlayModeSessionOptions Options => IsRunning || IsStarting || IsPaused ? _options.SessionOptions : ShooterPlayModeSessionOptions.Default;
        public static ShooterHostFrameInput LastInput => _lastInput;
        public static ShooterClientInputSubmitResult LastSubmitResult => _lastSubmitResult;
        public static ShooterClientFrameTickResult LastTickResult => _lastTickResult;
        public static ShooterClientGatewayInputSubmitResult LastGatewaySubmitResult => _inputSubmitStrategy != null ? _inputSubmitStrategy.LastResult : default;
        public static Exception? LastGatewayInputError => _inputSubmitStrategy?.LastError;
        public static bool HasPendingGatewayInput => _inputSubmitStrategy?.HasPending == true;
        public static bool HasQueuedGatewayInput => _inputSubmitStrategy?.HasQueued == true;
        public static long GatewayInputSubmittedCount => _inputSubmitStrategy?.SubmittedCount ?? 0L;
        public static long GatewayInputQueuedCount => _inputSubmitStrategy?.QueuedCount ?? 0L;
        public static long GatewayInputReplacedCount => _inputSubmitStrategy?.ReplacedCount ?? 0L;
        public static long GatewayInputCompletedCount => _inputSubmitStrategy?.CompletedCount ?? 0L;
        public static long GatewayInputFailedCount => _inputSubmitStrategy?.FailedCount ?? 0L;
        public static long GatewayInputResyncRequestedCount => _inputSubmitStrategy?.ResyncRequestedCount ?? 0L;
        public static long StepCount => _stepCount;
        public static long RenderCount => _renderCount;
        public static ShooterUnityViewRenderBackend ViewBackend => ViewSink.Backend;
        public static SyncTimeAnchor LastLocalTimeAnchor => _timeAnchors?.LastLocalAnchor ?? default;
        public static SyncTimeAnchor LastRemoteTimeAnchor => _lastRemoteTimeAnchor;
        public static ShooterRemoteLatencyCompensationDiagnostics LastRemoteLatencyCompensationDiagnostics => _lastRemoteLatencyCompensationDiagnostics;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Uninstall();
            StateChanged = null;
        }

        public static Task<ShooterClientNetworkLaunchResult> StartOrReconnectAsync(
            ShooterPlayModeSessionOptions options,
            ShooterClientNetworkEndpoint endpoint,
            string sessionToken = ShooterRemoteStateSyncDefaults.DefaultSessionToken,
            string region = ShooterRemoteStateSyncDefaults.DefaultRegion,
            string serverId = ShooterRemoteStateSyncDefaults.DefaultServerId)
        {
            return StartAsync(ShooterRemoteStateSyncLaunchOptions.RestoreFirst(options, endpoint, sessionToken, region, serverId));
        }

        public static Task<ShooterClientNetworkLaunchResult> StartAsync(ShooterRemoteStateSyncLaunchOptions launchOptions)
        {
            return StartAsync(launchOptions, launcher: null);
        }

        /// <summary>
        /// Starts the battle by adopting an already connected launcher from the formal room flow.
        /// Ownership transfers to the host as soon as this method is called.
        /// </summary>
        public static async Task<ShooterClientNetworkLaunchResult> StartAsync(
            ShooterRemoteStateSyncLaunchOptions launchOptions,
            ShooterClientNetworkLauncher? launcher)
        {
            Install();
            var generation = AdvanceLifecycleGeneration();
            StopRunningSession(advanceLifecycle: false);
            _lastError = null;
            _isStarting = true;
            _options = launchOptions;
            _pausedResumeOptions = default;
            _isPaused = false;
            _isAutoReconnecting = false;
            _isAutoReconnectAttemptRunning = false;
            SessionReconnectScheduler.Reset();
            _isWaitingForInitialFullStateSync = false;
            _lastInitialFullStateSyncApplyResult = default;
            NotifyStateChanged();

            try
            {
                var state = await StartSessionAsync(launchOptions, generation, launcher).ConfigureAwait(false);
                if (!IsCurrentLifecycle(generation))
                {
                    state.Dispose();
                    throw new OperationCanceledException("Shooter remote state-sync start was superseded by a newer lifecycle.");
                }

                ActivateRunningState(state, launchOptions);
                return state.Launch;
            }
            catch (Exception ex)
            {
                if (IsCurrentLifecycle(generation))
                {
                    StopRunningSession(advanceLifecycle: false);
                    _lastConnectionResult = null;
                    _lastError = ex;
                }

                throw;
            }
            finally
            {
                if (IsCurrentLifecycle(generation))
                {
                    _isStarting = false;
                    NotifyStateChanged();
                }
            }
        }

        public static void PauseForReconnectValidation()
        {
            var state = _state;
            if (state == null || _isPaused)
            {
                return;
            }

            _pausedResumeOptions = ShooterReconnectLaunchOptionsBuilder.RestoreOnly(_options, state.Launch.Flow.RoomId);
            _inputSubmitStrategy?.Reset();
            _inputSubmitStrategy = null;
            state.Launcher.Close();
            _isPaused = true;
            NotifyStateChanged();
        }

        public static Task<ShooterClientNetworkLaunchResult> ResumeFromPauseAsync()
        {
            if (!_isPaused)
            {
                var runningLaunch = _state?.Launch;
                if (runningLaunch != null)
                {
                    return Task.FromResult(runningLaunch);
                }

                throw new InvalidOperationException("Shooter remote state-sync host is not paused.");
            }

            var resumeOptions = _pausedResumeOptions;
            if (string.IsNullOrWhiteSpace(resumeOptions.SessionToken))
            {
                resumeOptions = ShooterReconnectLaunchOptionsBuilder.RestoreOnly(_options, _state?.Launch.Flow.RoomId ?? _options.RoomId);
            }

            return StartAsync(resumeOptions);
        }

        public static void Stop()
        {
            StopRunningSession();
            _lastConnectionResult = null;
            _lastError = null;
            ViewSink.Clear();
            NotifyStateChanged();
        }

        public static void Uninstall()
        {
            StopRunningSession();
            UninstallPlayerLoop();
            Application.quitting -= OnApplicationQuitting;
            Application.focusChanged -= OnApplicationFocusChanged;
            _lastConnectionResult = null;
            _lastError = null;
            ViewSink.Clear();
            NotifyStateChanged();
        }

        public static void Tick(float deltaSeconds)
        {
            try
            {
                TickRunningSession(deltaSeconds);
            }
            catch (Exception ex)
            {
                _lastError = ex;
                Debug.LogException(ex);
                Stop();
            }
        }

        public static void RebuildViews()
        {
            ViewSink.RebuildAll();
        }

        public static void SetViewBackend(ShooterUnityViewRenderBackend backend)
        {
            ViewSink.SetBackend(backend);
            if (_state != null)
            {
                ViewSink.RebuildAll();
            }
        }

        private static void Install()
        {
            InstallPlayerLoop();
            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;
            Application.focusChanged -= OnApplicationFocusChanged;
            Application.focusChanged += OnApplicationFocusChanged;
        }

        /// <summary>立即等待当前远程会话的可靠事件检查点完成持久化。</summary>
        public static void FlushReliableEventCheckpoints()
        {
            _state?.Launcher.FlushReliableEventCheckpointsAsync(
                ReliableEventCheckpointFlushTrigger.Manual).GetAwaiter().GetResult();
        }

        private static void FlushReliableEventCheckpoints(
            ReliableEventCheckpointFlushTrigger trigger)
        {
            _state?.Launcher.FlushReliableEventCheckpointsAsync(trigger).GetAwaiter().GetResult();
        }

        private static async Task<ShooterRemoteStateSyncRuntimeState> StartSessionAsync(
            ShooterRemoteStateSyncLaunchOptions launchOptions,
            long generation,
            ShooterClientNetworkLauncher? existingLauncher = null)
        {
            var runtimeWorld = ShooterGameplayScenarioWorldHostFactory.CreateBattleWorld(
                $"remote-{launchOptions.SessionToken}-client",
                launchOptions.SessionOptions);
            var launcher = existingLauncher ?? ShooterClientNetworkLauncher.Create(ShooterClientConnectionFactory.TcpForUnityMainThread());

            try
            {
                var start = BuildStartPayload(launchOptions.SessionOptions);
                var launchSpec = launchOptions.RoomLaunchSpec
                    ?? ShooterRoomLaunchSpec.CreateDefault($"unity-{launchOptions.SessionOptions.ControlledPlayerId}");
                var connectionResult = await new ShooterRemoteStateSyncConnectionFlow().ConnectAsync(
                    launcher,
                    runtimeWorld.Runtime,
                    ShooterPresentationSessionContext.CreateDefault(),
                    launchOptions,
                    start,
                    launchSpec,
                    (uint)launchOptions.SessionOptions.ControlledPlayerId).ConfigureAwait(false);

                ThrowIfStaleLifecycle(generation);
                await new ShooterInitialFullStateSyncCoordinator(
                    waiting => SetWaitingForInitialFullStateSync(generation, waiting),
                    result => SetLastInitialFullStateSyncApplyResult(generation, result),
                    () => NotifyStateChangedIfCurrent(generation)).RequestIfNeededAsync(
                        connectionResult,
                        launcher,
                        launchOptions.Timeout,
                        launchOptions.SessionOptions.TickRate).ConfigureAwait(false);

                ThrowIfStaleLifecycle(generation);
                _lastConnectionResult = connectionResult;
                _effectiveControlledPlayerId = ResolveEffectiveControlledPlayerId(connectionResult.Launch.Flow, launchOptions.SessionOptions.ControlledPlayerId);
                connectionResult.Launch.Session.Presentation.ControlledPlayerId = _effectiveControlledPlayerId;

                return new ShooterRemoteStateSyncRuntimeState(runtimeWorld, launcher, connectionResult.Launch);
            }
            catch
            {
                launcher.Dispose();
                runtimeWorld.Dispose();
                if (IsCurrentLifecycle(generation))
                {
                    _lastConnectionResult = null;
                }

                throw;
            }
        }

        private static void TickRunningSession(float deltaSeconds)
        {
            var state = _state;
            if (state == null || _isPaused)
            {
                return;
            }

            if (_isAutoReconnecting)
            {
                TickAutoReconnect(deltaSeconds);
                return;
            }

            state.Launcher.Tick(deltaSeconds);
            if (TryBeginAutoReconnectAfterSocketLoss(state))
            {
                return;
            }

            _inputSubmitStrategy?.CompleteIfFinished();

            var localTimeAnchor = (_timeAnchors ??= ShooterTimeAnchorCoordinator.CreateLocal(_options.SessionOptions.TickRate)).AdvanceLocal();
            _lastRemoteTimeAnchor = state.Launch.Flow.RemoteTimeAnchorProjection.TimeAnchor;
            var inputResult = InputPump.SubmitFrameInput(
                state.Launch.Session,
                ResolveEffectiveControlledPlayerId(state.Launch.Flow, _options.SessionOptions.ControlledPlayerId),
                _inputSubmitStrategy);
            _lastInput = inputResult.Input;
            _lastSubmitResult = inputResult.SubmitResult;
            _stepCount++;

            _lastTickResult = state.Launch.Session.Tick(deltaSeconds);
            _inputSubmitStrategy?.CompleteIfFinished();
            _lastRemoteLatencyCompensationDiagnostics = CreateRemoteLatencyCompensationDiagnostics();

            var frame = ShooterRemotePresentationFrameBuilder.Build(
                state.Launch,
                _options.SessionOptions,
                ResolveEffectiveControlledPlayerId(state.Launch.Flow, _options.SessionOptions.ControlledPlayerId),
                _lastRemoteTimeAnchor,
                localTimeAnchor,
                _lastRemoteLatencyCompensationDiagnostics);
            ViewSink.Render(in frame);
            _renderCount++;
        }

        private static ShooterRemoteLatencyCompensationDiagnostics CreateRemoteLatencyCompensationDiagnostics()
        {
            return _inputSubmitStrategy?.CreateLatencyDiagnostics() ?? default;
        }

        private static int ResolveEffectiveControlledPlayerId(ShooterRoomGatewayFlowResult flow, int fallbackPlayerId)
        {
            return flow.PlayerId > 0u && flow.PlayerId <= int.MaxValue
                ? (int)flow.PlayerId
                : fallbackPlayerId;
        }

        private static void StopRunningSession(bool advanceLifecycle = true)
        {
            if (advanceLifecycle)
            {
                AdvanceLifecycleGeneration();
            }

            _inputSubmitStrategy?.Reset();
            _inputSubmitStrategy = null;
            _state?.Dispose();
            _state = null;
            _options = default;
            _effectiveControlledPlayerId = 0;
            _pausedResumeOptions = default;
            _lastInput = default;
            _lastSubmitResult = default;
            _lastTickResult = default;
            _stepCount = 0;
            _renderCount = 0;
            _timeAnchors = null;
            _lastRemoteTimeAnchor = default;
            _lastRemoteLatencyCompensationDiagnostics = default;
            _isStarting = false;
            _isPaused = false;
            _isAutoReconnecting = false;
            _isAutoReconnectAttemptRunning = false;
            SessionReconnectScheduler.Reset();
            _isWaitingForInitialFullStateSync = false;
            _lastInitialFullStateSyncApplyResult = default;
        }

        private static bool TryBeginAutoReconnectAfterSocketLoss(ShooterRemoteStateSyncRuntimeState state)
        {
            if (_isStarting || _isPaused || _isAutoReconnecting)
            {
                return false;
            }

            var connection = state.Launcher.Connection;
            if (connection.IsConnected && connection.State == ConnectionState.Connected)
            {
                return false;
            }

            if (connection.State == ConnectionState.Connecting)
            {
                return false;
            }

            if (SessionReconnectScheduler.IsExhausted)
            {
                return true;
            }

            if (!SessionReconnectScheduler.Request())
            {
                return true;
            }

            _isAutoReconnecting = true;
            _pausedResumeOptions = ShooterReconnectLaunchOptionsBuilder.RestoreOnly(_options, state.Launch.Flow.RoomId);
            _inputSubmitStrategy?.Reset();
            _inputSubmitStrategy = null;
            state.Launcher.Close();
            NotifyStateChanged();
            return true;
        }

        private static void TickAutoReconnect(float deltaSeconds)
        {
            if (_isAutoReconnectAttemptRunning ||
                !SessionReconnectScheduler.TryTakeAttempt(deltaSeconds, out var attempt))
            {
                return;
            }

            _isAutoReconnectAttemptRunning = true;
            Debug.Log(
                $"[ShooterRemoteStateSync] Session restore attempt " +
                $"{attempt}/{SessionReconnectScheduler.MaxAttempts}.");
            _ = ResumeAfterSocketLossAsync(_pausedResumeOptions, _lifecycleGeneration);
        }

        private static async Task ResumeAfterSocketLossAsync(
            ShooterRemoteStateSyncLaunchOptions resumeOptions,
            long sourceGeneration)
        {
            try
            {
                if (!IsCurrentLifecycle(sourceGeneration))
                {
                    return;
                }

                var restoredState = await StartSessionAsync(
                    resumeOptions,
                    sourceGeneration).ConfigureAwait(false);
                if (!IsCurrentLifecycle(sourceGeneration))
                {
                    restoredState.Dispose();
                    return;
                }

                var previousState = _state;
                ActivateRunningState(restoredState, resumeOptions);
                previousState?.Dispose();
                SessionReconnectScheduler.Reset();
                _isAutoReconnectAttemptRunning = false;
                _isAutoReconnecting = false;
                NotifyStateChanged();
            }
            catch (Exception ex)
            {
                if (!IsCurrentLifecycle(sourceGeneration))
                {
                    return;
                }

                _lastError = ex;
                _isAutoReconnectAttemptRunning = false;
                if (SessionReconnectScheduler.IsExhausted)
                {
                    _isAutoReconnecting = false;
                }
                Debug.LogException(ex);
                NotifyStateChanged();
            }
        }

        private static void ActivateRunningState(
            ShooterRemoteStateSyncRuntimeState state,
            ShooterRemoteStateSyncLaunchOptions launchOptions)
        {
            _state = state;
            _options = launchOptions;
            _pausedResumeOptions = default;
            _inputSubmitStrategy = ShooterRemoteInputSubmitStrategy.Create(
                state.Launch.Battle,
                launchOptions.Timeout);
            _lastInput = default;
            _lastSubmitResult = default;
            _lastTickResult = default;
            _stepCount = 0;
            _renderCount = 0;
            _timeAnchors = ShooterTimeAnchorCoordinator.CreateLocal(
                launchOptions.SessionOptions.TickRate);
            _lastRemoteTimeAnchor = state.Launch.Flow.RemoteTimeAnchorProjection.TimeAnchor;
            _lastRemoteLatencyCompensationDiagnostics = default;
            _lastError = null;
        }

        private static long AdvanceLifecycleGeneration()
        {
            unchecked
            {
                return ++_lifecycleGeneration;
            }
        }

        private static bool IsCurrentLifecycle(long generation)
        {
            return generation == _lifecycleGeneration;
        }

        private static void ThrowIfStaleLifecycle(long generation)
        {
            if (!IsCurrentLifecycle(generation))
            {
                throw new OperationCanceledException("Shooter remote state-sync lifecycle was superseded.");
            }
        }

        private static void SetWaitingForInitialFullStateSync(long generation, bool waiting)
        {
            if (!IsCurrentLifecycle(generation))
            {
                return;
            }

            _isWaitingForInitialFullStateSync = waiting;
        }

        private static void SetLastInitialFullStateSyncApplyResult(long generation, ShooterSnapshotApplyResult result)
        {
            if (!IsCurrentLifecycle(generation))
            {
                return;
            }

            _lastInitialFullStateSyncApplyResult = result;
        }

        private static void NotifyStateChangedIfCurrent(long generation)
        {
            if (IsCurrentLifecycle(generation))
            {
                NotifyStateChanged();
            }
        }

        private static ShooterStartGamePayload BuildStartPayload(ShooterPlayModeSessionOptions options)
        {
            var players = new List<ShooterStartPlayer>(options.PlayerCount);
            for (var i = 0; i < options.PlayerCount; i++)
            {
                players.Add(new ShooterStartPlayer(i + 1, $"P{i + 1}", i * 4f, 0f));
            }

            return new ShooterStartGamePayload(
                $"unity-remote-state-sync-{options.RandomSeed}",
                options.TickRate,
                options.RandomSeed,
                players.ToArray());
        }

        private static void TickFromPlayerLoop()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Tick(Time.deltaTime);
        }

        private static void OnApplicationQuitting()
        {
            FlushReliableEventCheckpoints(ReliableEventCheckpointFlushTrigger.ApplicationQuit);
            Uninstall();
        }

        private static void OnApplicationFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
            {
                FlushReliableEventCheckpoints(
                    ReliableEventCheckpointFlushTrigger.ApplicationPause);
            }
        }

        private static void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }

        private static void InstallPlayerLoop()
        {
            if (_playerLoopInstalled)
            {
                return;
            }

            var loop = PlayerLoop.GetCurrentPlayerLoop();
            if (!InsertIntoUpdate(ref loop))
            {
                Debug.LogWarning("[ShooterRemoteStateSyncPlayModeHost] Failed to install PlayerLoop update node.");
                return;
            }

            PlayerLoop.SetPlayerLoop(loop);
            _playerLoopInstalled = true;
        }

        private static void UninstallPlayerLoop()
        {
            if (!_playerLoopInstalled)
            {
                return;
            }

            var loop = PlayerLoop.GetCurrentPlayerLoop();
            if (RemoveFromPlayerLoop(ref loop))
            {
                PlayerLoop.SetPlayerLoop(loop);
            }

            _playerLoopInstalled = false;
        }

        private static bool InsertIntoUpdate(ref PlayerLoopSystem root)
        {
            if (root.subSystemList == null)
            {
                return false;
            }

            for (var i = 0; i < root.subSystemList.Length; i++)
            {
                ref var system = ref root.subSystemList[i];
                if (system.type == typeof(Update))
                {
                    system.subSystemList = AppendOrReplace(system.subSystemList, new PlayerLoopSystem
                    {
                        type = typeof(ShooterRemoteStateSyncPlayerLoopNode),
                        updateDelegate = TickFromPlayerLoop
                    });
                    return true;
                }

                if (InsertIntoUpdate(ref system))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RemoveFromPlayerLoop(ref PlayerLoopSystem root)
        {
            if (root.subSystemList == null || root.subSystemList.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < root.subSystemList.Length; i++)
            {
                if (root.subSystemList[i].type == typeof(ShooterRemoteStateSyncPlayerLoopNode))
                {
                    root.subSystemList = RemoveAt(root.subSystemList, i);
                    return true;
                }

                var child = root.subSystemList[i];
                if (RemoveFromPlayerLoop(ref child))
                {
                    root.subSystemList[i] = child;
                    return true;
                }
            }

            return false;
        }

        private static PlayerLoopSystem[] AppendOrReplace(PlayerLoopSystem[]? systems, PlayerLoopSystem node)
        {
            if (systems == null || systems.Length == 0)
            {
                return new[] { node };
            }

            for (var i = 0; i < systems.Length; i++)
            {
                if (systems[i].type == node.type)
                {
                    systems[i] = node;
                    return systems;
                }
            }

            var result = new PlayerLoopSystem[systems.Length + 1];
            Array.Copy(systems, result, systems.Length);
            result[result.Length - 1] = node;
            return result;
        }

        private static PlayerLoopSystem[] RemoveAt(PlayerLoopSystem[] systems, int index)
        {
            if (systems.Length == 1)
            {
                return Array.Empty<PlayerLoopSystem>();
            }

            var result = new PlayerLoopSystem[systems.Length - 1];
            if (index > 0)
            {
                Array.Copy(systems, 0, result, 0, index);
            }

            if (index < systems.Length - 1)
            {
                Array.Copy(systems, index + 1, result, index, systems.Length - index - 1);
            }

            return result;
        }

        private sealed class ShooterRemoteStateSyncRuntimeState : IDisposable
        {
            private bool _disposed;

            public ShooterRemoteStateSyncRuntimeState(
                ShooterBattleWorldSession runtimeWorld,
                ShooterClientNetworkLauncher launcher,
                ShooterClientNetworkLaunchResult launch)
            {
                RuntimeWorld = runtimeWorld ?? throw new ArgumentNullException(nameof(runtimeWorld));
                Launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
                Launch = launch ?? throw new ArgumentNullException(nameof(launch));
            }

            public ShooterBattleWorldSession RuntimeWorld { get; }
            public ShooterClientNetworkLauncher Launcher { get; }
            public ShooterClientNetworkLaunchResult Launch { get; }
            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                Launcher.Dispose();
                RuntimeWorld.Dispose();
            }
        }

        private struct ShooterRemoteStateSyncPlayerLoopNode
        {
        }
    }
}
