using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Logging;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionOrchestrator
    {
        private readonly BattleSessionState _state;
        private readonly BattleSessionHandles _handles;
        private readonly ISessionOrchestratorHost _host;
        private readonly object _stopGate = new object();
        private CleanupStep _completedCleanupSteps;
        private bool _cleanupRequired;
        private bool _sessionStartingPipelineEntered;
        private Task _pendingStopTask = Task.CompletedTask;

        [Flags]
        private enum CleanupStep
        {
            None = 0,
            RecordWriter = 1 << 0,
            BattleContext = 1 << 1,
            StoppingPipeline = 1 << 2,
            Recovery = 1 << 3,
            SnapshotRouting = 1 << 4,
            ConfirmedView = 1 << 5,
            BattleWorlds = 1 << 6,
            ConfirmedWorld = 1 << 7,
            RemoteDrivenWorld = 1 << 8,
            RemoteInterpolation = 1 << 9,
            FrameSubscription = 1 << 10,
            LogicSession = 1 << 11,
            SessionHandles = 1 << 12,
        }

        public SessionOrchestrator(BattleSessionState state, BattleSessionHandles handles, ISessionOrchestratorHost host)
        {
            _state = state;
            _handles = handles;
            _host = host;
        }

        public float GetFixedDeltaSeconds()
        {
            var plan = _host.Plan;
            return 1f / ResolveTickRate(plan);
        }

        public void StartSession()
        {
            StopSession();
            _state.BeginStart();
            _cleanupRequired = true;
            _completedCleanupSteps = CleanupStep.None;
            _sessionStartingPipelineEntered = false;

            try
            {
                var plan = _host.Plan;
                StartLogicSession(plan);
                StartAuxiliaryWorlds(plan);
                _sessionStartingPipelineEntered = true;
                _host.InvokeSessionStartingPipeline();
                ResetTickState();
                BindBattleContext();
                _host.InvokeReplaySetupPipeline();
                _state.CompleteStart();
            }
            catch (Exception ex)
            {
                try
                {
                    DisposeSessionResources();
                }
                catch (Exception cleanupEx)
                {
                    var combined = new AggregateException(ex, cleanupEx);
                    var failure = new InvalidOperationException(
                        "Battle session startup failed and cleanup also reported failures.",
                        combined);
                    _state.Fault(failure);
                    throw failure;
                }

                _state.Fault(ex);
                throw;
            }
        }

        public void StopSession()
        {
            StopSessionAsync().GetAwaiter().GetResult();
        }

        internal Task StopSessionAsync()
        {
            lock (_stopGate)
            {
                if (!_pendingStopTask.IsCompleted)
                {
                    return _pendingStopTask;
                }

                _pendingStopTask = StopSessionCoreAsync();
                return _pendingStopTask;
            }
        }

        private async Task StopSessionCoreAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var diagnosticGeneration =
                _state.LifecycleDiagnostics.BeginPendingOperation("battle-session-stop");
            Exception teardownFailure = null;
            try
            {
                if (!_cleanupRequired &&
                    (_state.Lifecycle == BattleSessionLifecycleState.Created ||
                     _state.Lifecycle == BattleSessionLifecycleState.Stopped))
                {
                    if (_state.Lifecycle == BattleSessionLifecycleState.Created)
                    {
                        _state.BeginStop();
                        _state.CompleteStop();
                    }
                    return;
                }

                _state.BeginStop();
                await DisposeSessionResourcesAsync().ConfigureAwait(false);
                _state.CompleteStop();
            }
            catch (Exception ex)
            {
                teardownFailure = ex;
                _state.Fault(ex);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                _state.LifecycleDiagnostics.CompletePendingOperation(
                    diagnosticGeneration,
                    stopwatch.Elapsed,
                    teardownFailure);
            }
        }

        private void StartLogicSession(BattleStartPlan plan)
        {
            _host.StartBattleLogicSession(BuildSessionOptions(plan));
            _host.SubscribeFrameReceived();
        }

        private static BattleLogicSessionOptions BuildSessionOptions(BattleStartPlan plan)
        {
            var world = plan.World;

            return new BattleLogicSessionOptions
            {
                Mode = ResolveLogicMode(plan),
                WorldId = new WorldId(world.WorldId),
                WorldType = world.WorldType,
                ClientId = world.ClientId,
                PlayerId = world.PlayerId,

                ScanAssemblies = new[]
                {
                    typeof(AbilityKit.Ability.World.Services.WorldServiceContainerFactory).Assembly,
                    typeof(BattleLogicSession).Assembly,
                    typeof(AbilityKit.Demo.Moba.Systems.MobaWorldBootstrapModule).Assembly,
                    typeof(SessionOrchestrator).Assembly,
                },
                NamespacePrefixes = new[] { "AbilityKit" },

                AutoConnect = false,
                AutoCreateWorld = false,
                AutoJoin = false,
            };
        }

        private static BattleLogicMode ResolveLogicMode(BattleStartPlan plan)
        {
            return ShouldUseRemoteLogic(plan)
                ? BattleLogicMode.Remote
                : BattleLogicMode.Local;
        }

        private static bool ShouldUseRemoteLogic(BattleStartPlan plan)
        {
            return plan.Sync.SyncMode == BattleSyncMode.SnapshotAuthority || IsGatewayRemoteTransport(plan);
        }

        private void StartAuxiliaryWorlds(BattleStartPlan plan)
        {
            if (IsGatewayRemoteTransport(plan))
            {
                _host.StartRemoteDrivenLocalWorld();
            }

            if (plan.Authority.EnableConfirmedAuthorityWorld)
            {
                _host.StartConfirmedAuthorityWorld();
            }
        }

        private void ResetTickState()
        {
            _state.Tick.Reset();
        }

        private void BindBattleContext()
        {
            SessionContextBinder.BindRuntimeSession(_host.Context, _state, _handles);
        }

        private void DisposeSessionResources()
        {
            DisposeSessionResourcesAsync().GetAwaiter().GetResult();
        }

        private async Task DisposeSessionResourcesAsync()
        {
            if (!_cleanupRequired) return;

            var failures = new List<Exception>();

            async Task DisposeStepAsync(CleanupStep step, Func<Task> action, string name)
            {
                if ((_completedCleanupSteps & step) != 0) return;

                try
                {
                    await (action?.Invoke() ?? Task.CompletedTask).ConfigureAwait(false);
                    _completedCleanupSteps |= step;
                }
                catch (Exception ex)
                {
                    failures.Add(new InvalidOperationException(
                        $"Battle session cleanup step failed: {name}.",
                        ex));
                    Log.Exception(ex, $"[BattleSessionFeature] Session resource cleanup failed: {name}");
                }
            }

            Task DisposeSyncStepAsync(CleanupStep step, Action action, string name) =>
                DisposeStepAsync(
                    step,
                    () =>
                    {
                        action?.Invoke();
                        return Task.CompletedTask;
                    },
                    name);

            // Reverse startup order. Each successful step is remembered so a later Stop can
            // resume only the failed work without repeating already-completed teardown.
            await DisposeSyncStepAsync(CleanupStep.RecordWriter, _host.DisposeReplayRecordWriter, "input record writer").ConfigureAwait(false);
            await DisposeSyncStepAsync(CleanupStep.BattleContext, ClearBattleContext, "battle context").ConfigureAwait(false);

            if (_sessionStartingPipelineEntered)
            {
                await DisposeSyncStepAsync(CleanupStep.StoppingPipeline, _host.InvokeSessionStoppingPipeline, "session stopping pipeline").ConfigureAwait(false);
            }
            else
            {
                _completedCleanupSteps |= CleanupStep.StoppingPipeline;
            }

            await DisposeStepAsync(CleanupStep.Recovery, _host.StopRecoveryAsync, "authoritative recovery").ConfigureAwait(false);
            await DisposeSyncStepAsync(CleanupStep.SnapshotRouting, _host.DisposeSnapshotRouting, "snapshot routing").ConfigureAwait(false);
            await DisposeSyncStepAsync(CleanupStep.ConfirmedView, _host.DisposeConfirmedView, "confirmed view").ConfigureAwait(false);
            await DisposeSyncStepAsync(CleanupStep.BattleWorlds, _host.TryDestroyBattleWorlds, "battle worlds").ConfigureAwait(false);
            await DisposeSyncStepAsync(CleanupStep.ConfirmedWorld, _host.DisposeConfirmedWorld, "confirmed world").ConfigureAwait(false);
            await DisposeSyncStepAsync(CleanupStep.RemoteDrivenWorld, _host.DisposeRemoteDrivenWorld, "remote-driven world").ConfigureAwait(false);
            await DisposeSyncStepAsync(CleanupStep.RemoteInterpolation, _host.DisposeRemoteInterpolation, "remote interpolation").ConfigureAwait(false);
            await DisposeSyncStepAsync(CleanupStep.FrameSubscription, _host.UnsubscribeFrameReceived, "unsubscribe frame receiver").ConfigureAwait(false);
            await DisposeSyncStepAsync(CleanupStep.LogicSession, _host.StopBattleLogicSession, "battle logic session").ConfigureAwait(false);

            if (failures.Count == 0)
            {
                await DisposeSyncStepAsync(CleanupStep.SessionHandles, _host.ResetSessionHandles, "session handles").ConfigureAwait(false);
            }

            if (failures.Count > 0)
            {
                throw new AggregateException("Battle session cleanup completed with one or more failures.", failures);
            }

            _cleanupRequired = false;
            _sessionStartingPipelineEntered = false;
        }

        private void ClearBattleContext()
        {
            SessionContextBinder.ClearSession(_host.Context);
        }

        private static int ResolveTickRate(BattleStartPlan plan)
        {
            if (IsGatewayRemoteTransport(plan)) return 30;

            var tickRate = plan.World.TickRate;
            return tickRate > 0 ? tickRate : 30;
        }

        private static bool IsGatewayRemoteTransport(BattleStartPlan plan)
        {
            return plan.HostMode == BattleHostMode.GatewayRemote && plan.Gateway.UseGatewayTransport;
        }
    }
}
