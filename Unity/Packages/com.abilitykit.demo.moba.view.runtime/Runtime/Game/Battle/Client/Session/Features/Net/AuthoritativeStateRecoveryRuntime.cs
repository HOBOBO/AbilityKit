using System;
using System.Threading.Tasks;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Services.StateImport;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.HFSM.Definition;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Protocol.Room;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;

namespace AbilityKit.Game.Flow
{
    internal interface IBattleAuthoritativeWorldRecoveryPort
    {
        void Configure(
            in BattleStartPlan plan,
            BattleContext context,
            float fixedDeltaSeconds,
            Func<WorldId, int> resolveIdealFrameLimit,
            Func<bool> shouldForceHashMismatch);

        bool TryImport(in GatewayStateSyncSnapshot snapshot);
        bool TryApplyAuthoritativeState(in GatewayStateSyncSnapshot snapshot);
        void ResetAfterReconnect();
    }

    internal sealed class BattleAuthoritativeWorldRecoveryPort
        : IBattleAuthoritativeWorldRecoveryPort
    {
        private readonly BattleSimulationRuntime _simulation;
        private readonly BattleSessionHandles _handles;
        private BattleStartPlan _plan;
        private BattleContext _context;
        private float _fixedDeltaSeconds;
        private Func<WorldId, int> _resolveIdealFrameLimit;
        private Func<bool> _shouldForceHashMismatch;

        internal BattleAuthoritativeWorldRecoveryPort(
            BattleSimulationRuntime simulation,
            BattleSessionHandles handles)
        {
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _handles = handles ?? throw new ArgumentNullException(nameof(handles));
        }

        public void Configure(
            in BattleStartPlan plan,
            BattleContext context,
            float fixedDeltaSeconds,
            Func<WorldId, int> resolveIdealFrameLimit,
            Func<bool> shouldForceHashMismatch)
        {
            _plan = plan;
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _fixedDeltaSeconds = fixedDeltaSeconds;
            _resolveIdealFrameLimit = resolveIdealFrameLimit ?? throw new ArgumentNullException(nameof(resolveIdealFrameLimit));
            _shouldForceHashMismatch = shouldForceHashMismatch ?? throw new ArgumentNullException(nameof(shouldForceHashMismatch));
        }

        public bool TryImport(in GatewayStateSyncSnapshot snapshot)
        {
            if (_context == null || _handles.Session == null) return false;

            _simulation.StartRemoteDriven(
                _plan,
                _context,
                _fixedDeltaSeconds,
                _resolveIdealFrameLimit,
                _shouldForceHashMismatch);

            var world = _handles.RemoteDriven.World;
            var importer = _handles.RemoteDriven.Capabilities.StateImporter;
            if (world == null || importer == null)
            {
                Log.Warning(
                    "[BattleAuthoritativeWorldRecoveryPort] Remote-driven importer is unavailable.");
                return false;
            }

            var result = ImportAuthoritativeState(importer, world, in snapshot);
            if (result.Failed > 0)
            {
                Log.Warning(
                    $"[BattleAuthoritativeWorldRecoveryPort] Import incomplete. " +
                    $"frame={snapshot.Frame} failed={result.Failed}");
                ResetAfterReconnect();
                return false;
            }

            var runtime = _handles.RemoteDriven.Runtime;
            if (runtime == null ||
                !runtime.Features.TryGetFeature<IClientPredictionBaselineControl>(out var baseline) ||
                baseline == null ||
                !baseline.TryRebase(world.Id, new FrameIndex(snapshot.Frame)))
            {
                Log.Warning(
                    $"[BattleAuthoritativeWorldRecoveryPort] Prediction rebase failed. " +
                    $"frame={snapshot.Frame} worldId={world.Id.Value}");
                ResetAfterReconnect();
                return false;
            }

            _simulation.RemoteDrivenLastTickedFrame = snapshot.Frame;
            return true;
        }

        public bool TryApplyAuthoritativeState(in GatewayStateSyncSnapshot snapshot)
        {
            var importer = _handles.RemoteDriven.Capabilities.StateImporter;
            if (importer == null)
            {
                Log.Warning(
                    $"[BattleAuthoritativeWorldRecoveryPort] Authoritative state apply skipped. " +
                    $"frame={snapshot.Frame}");
                return false;
            }

            var result = ImportAuthoritativeState(importer, _handles.RemoteDriven.World, in snapshot);
            if (result.Failed == 0) return true;

            Log.Warning(
                $"[BattleAuthoritativeWorldRecoveryPort] Authoritative state apply incomplete. " +
                $"frame={snapshot.Frame} failed={result.Failed}");
            ResetAfterReconnect();
            return false;
        }

        public void ResetAfterReconnect()
        {
            _simulation.DisposeRemoteDrivenWorld();
            var reconcile = _context?.PredictionReconcileControl;
            if (reconcile == null) return;

            var worldId = _context.HasRuntimeWorldId
                ? _context.RuntimeWorldId
                : new WorldId(_plan.World.WorldId);
            if (string.IsNullOrWhiteSpace(worldId.Value)) return;

            reconcile.ResetReconcile(worldId);
            reconcile.SetReconcileEnabled(worldId, true);
        }

        private static MobaStateImportResult ImportAuthoritativeState(
            MobaLogicWorldStateImporter importer,
            IWorld world,
            in GatewayStateSyncSnapshot snapshot)
        {
            var actors = snapshot.Actors ?? Array.Empty<GatewayStateSyncActorSnapshot>();
            var imports = new MobaActorStateImport[actors.Length];
            for (var i = 0; i < actors.Length; i++)
            {
                var actor = actors[i];
                imports[i] = new MobaActorStateImport(
                    actor.ActorId,
                    actor.X,
                    actor.Y,
                    actor.Z,
                    actor.Rotation,
                    actor.Hp,
                    actor.HpMax,
                    actor.TeamId,
                    actor.Kind,
                    actor.Code,
                    actor.OwnerNetId);
            }

            var requiresHfsm = snapshot.SchemaVersion >= 2 ||
                snapshot.PayloadOpCode == MobaCharacterHfsmRollbackProvider.DefaultKey ||
                snapshot.PayloadOpCode == MobaCharacterHfsmWireCodec.CompressedOpCode;
            MobaActorRegistry registry = null;
            StateMachineDefinition definition = null;
            if (world?.Services != null)
            {
                world.Services.TryResolve(out registry);
                world.Services.TryResolve(out definition);
            }
            var result = new MobaStateImportResult();
            if (requiresHfsm && registry == null)
            {
                Log.Warning("[BattleAuthoritativeWorldRecoveryPort] Character HFSM baseline is missing.");
                result.Failed++;
                return result;
            }

            try
            {
                if (requiresHfsm && !CharacterHfsmGatewayStateImporter.TryApply(
                        in snapshot, new MobaActorRegistry(), definition))
                {
                    result.Failed++;
                    return result;
                }
                result = importer.Import(imports, snapshot.Frame, isFullSnapshot: true);
                if (result.Failed == 0 && registry != null &&
                    !CharacterHfsmGatewayStateImporter.TryApply(in snapshot, registry, definition))
                    result.Failed++;
            }
            catch (Exception ex)
            {
                Log.Warning("[BattleAuthoritativeWorldRecoveryPort] State import failed: " + ex.Message);
                result.Failed++;
            }
            return result;
        }
    }

    /// <summary>
    /// 负责单个远端复制代际的权威快照准入、断线恢复与全量状态请求。
    /// 项目恢复动作通过通用协调器和动作路由执行，避免各信号来源重复维护策略分支。
    /// </summary>
    internal sealed class AuthoritativeStateRecoveryRuntime :
        IDisposable,
        INetworkSessionRecoverySignalSink
    {
        private const string RecoveryCorrelationPrefix = "moba-recovery-generation:";
        private readonly RecoveryGenerationOperation _operation = new RecoveryGenerationOperation();
        private readonly BattleReplicationRuntime _replication;
        private readonly ReliableBattleEventDeliveryRuntime _reliableEvents;
        private readonly IBattleAuthoritativeWorldRecoveryPort _worldRecovery;
        private readonly NetworkSessionRecoveryCoordinator _sessionRecovery;
        private readonly NetworkSessionRecoveryActionRouter<bool> _recoveryActions;
        private IBattleRecoveryTransportOperations _transport;
        private BattleContext _context;
        private long _remoteInterpolationGeneration;
        private Action _firstFrameReceived;

        internal AuthoritativeStateRecoveryRuntime(
            BattleReplicationRuntime replication,
            ReliableBattleEventDeliveryRuntime reliableEvents,
            IBattleAuthoritativeWorldRecoveryPort worldRecovery)
        {
            _replication = replication ?? throw new ArgumentNullException(nameof(replication));
            _reliableEvents = reliableEvents ?? throw new ArgumentNullException(nameof(reliableEvents));
            _worldRecovery = worldRecovery ?? throw new ArgumentNullException(nameof(worldRecovery));
            _sessionRecovery = new NetworkSessionRecoveryCoordinator(
                new NetworkSessionRecoveryOptions
                {
                    // MOBA 的恢复信号已经由状态准入和可靠事件游标去重，保留每次失败的重试机会。
                    DuplicateSignalWindow = TimeSpan.Zero,
                    AllowEqualPriorityReplacement = true
                });
            _recoveryActions = new NetworkSessionRecoveryActionRouter<bool>(
                    new NetworkSessionRecoveryActionRouterOptions<bool>
                    {
                        UnhandledActionPolicy = NetworkSessionRecoveryUnhandledActionPolicy.ReturnUnhandled,
                        HandlerFailurePolicy = NetworkSessionRecoveryHandlerFailurePolicy.CaptureAndReturn
                    })
                .Register(
                    NetworkSessionRecoveryAction.WaitForReconnect,
                    (_, _) => Task.FromResult(true))
                .Register(
                    NetworkSessionRecoveryAction.RequestFullSnapshot,
                    ExecuteFullStateRecoveryAsync)
                .Register(
                    NetworkSessionRecoveryAction.RestoreReliableEventBaseline,
                    ExecuteFullStateRecoveryAsync);
        }

        internal bool PendingStateImport => _replication.PendingStateImport;
        internal Task PendingExecution => _operation.PendingExecution;
        internal NetworkSessionRecoveryDecision RecoveryDecision =>
            _sessionRecovery.CurrentDecision;
        internal NetworkSessionRecoveryDiagnostics RecoveryDiagnostics =>
            _sessionRecovery.GetDiagnostics();

        public bool TryReport(
            in NetworkSessionRecoverySignal signal,
            out NetworkSessionRecoveryDecision decision)
        {
            return _sessionRecovery.TryReport(in signal, out decision);
        }

        internal Task<NetworkSessionRecoveryExecutionResult<bool>> ExecuteRecoveryDecisionAsync(
            NetworkSessionRecoveryDecision decision)
        {
            return _recoveryActions.ExecuteAsync(decision);
        }

        internal void BeginGeneration(
            IBattleRecoveryTransportOperations transport,
            IMobaReliableBattleEventCheckpointStore checkpointStore,
            BattleContext context,
            in BattleStartPlan plan,
            float fixedDeltaSeconds,
            Func<WorldId, int> resolveIdealFrameLimit,
            Func<bool> shouldForceHashMismatch,
            Action<WireReliableBattleEvent> eventSink,
            Action firstFrameReceived)
        {
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (_replication.ReliableEventCursor == null)
                throw new InvalidOperationException("Replication runtime must be built before recovery.");

            EndGeneration();
            _operation.Begin();

            try
            {
                _transport = transport;
                _context = context;
                _firstFrameReceived = firstFrameReceived;
                _worldRecovery.Configure(
                    in plan,
                    context,
                    fixedDeltaSeconds,
                    resolveIdealFrameLimit,
                    shouldForceHashMismatch);
                _reliableEvents.BeginGeneration(
                    _replication.SyncSession,
                    _replication.ReliableEventCursor,
                    transport,
                    checkpointStore,
                    eventSink,
                    HandleReliableTimelineInvalidated);
                _replication.PendingStateImport = true;
                _operation.ConnectionRecoveryPending = false;
                context.SetGameplayInputBlocked(BattleGameplayInputBlockReason.AuthoritativeRecovery, true);
                _remoteInterpolationGeneration = context.BeginRemoteInterpolation();
            }
            catch
            {
                try
                {
                    EndGeneration();
                }
                catch
                {
                    // 保留最初的初始化异常；下方仍会把当前持有者恢复到不可推进状态。
                }

                _replication.PendingStateImport = true;
                context.SetGameplayInputBlocked(BattleGameplayInputBlockReason.AuthoritativeRecovery, true);
                throw;
            }
        }

        internal void HandleSnapshot(object rawSnapshot)
        {
            if (rawSnapshot is not GatewayStateSyncSnapshot snapshot ||
                _replication.SnapshotAdmission == null)
            {
                return;
            }

            var admission = _replication.SnapshotAdmission.Admit(
                snapshot.WorldId,
                snapshot.Frame,
                snapshot.IsFullSnapshot,
                snapshot.SchemaVersion);
            if (!admission.Accepted)
            {
                Log.Warning(
                    $"[AuthoritativeStateRecoveryRuntime] Snapshot rejected. " +
                    $"status={admission.Status} worldId={snapshot.WorldId} frame={snapshot.Frame}");
                if (admission.ShouldRequestFullResync)
                {
                    ReportAndExecute(
                        NetworkSessionRecoverySignalKind.SnapshotResyncRequired,
                        SyncHealthSeverity.Error,
                        $"snapshot-admission:{admission.Status}",
                        admission.LastAcceptedFrame);
                }
                return;
            }

            var pendingStateImport = _replication.PendingStateImport;
            if (pendingStateImport)
            {
                if (!snapshot.IsFullSnapshot ||
                    !_worldRecovery.TryImport(in snapshot) ||
                    !_reliableEvents.AdoptAuthoritativeBaseline(
                        snapshot.EventEpoch,
                        snapshot.EventWatermark))
                {
                    _worldRecovery.ResetAfterReconnect();
                    _replication.PendingStateImport = true;
                    _context?.SetGameplayInputBlocked(BattleGameplayInputBlockReason.AuthoritativeRecovery, true);
                    _replication.SnapshotAdmission?.RequireFullBaseline();
                    ReportAndExecute(
                        NetworkSessionRecoverySignalKind.SnapshotResyncRequired,
                        SyncHealthSeverity.Error,
                        "state-import-failed",
                        snapshot.Frame);
                    return;
                }

                _replication.PendingStateImport = false;
                _context?.SetGameplayInputBlocked(BattleGameplayInputBlockReason.AuthoritativeRecovery, false);
                var recovered = new NetworkSessionRecoverySignal(
                    NetworkSessionRecoverySignalKind.Recovered,
                    SyncHealthSeverity.Info,
                    snapshot.Frame,
                    correlationContext: CreateRecoveryCorrelationContext(_operation.Generation),
                    detail: "authoritative-baseline-adopted");
                _sessionRecovery.TryReport(in recovered, out _);
            }

            var materialized = _replication.AuthoritativeSnapshotState?.Apply(in snapshot) ?? snapshot;
            if (!pendingStateImport && !_worldRecovery.TryApplyAuthoritativeState(in materialized))
            {
                _replication.PendingStateImport = true;
                _context?.SetGameplayInputBlocked(BattleGameplayInputBlockReason.AuthoritativeRecovery, true);
                _replication.SnapshotAdmission?.RequireFullBaseline();
                ReportAndExecute(
                    NetworkSessionRecoverySignalKind.SnapshotResyncRequired,
                    SyncHealthSeverity.Error,
                    "state-apply-failed",
                    snapshot.Frame);
                return;
            }

            _firstFrameReceived?.Invoke();
            var sample = new MobaRemoteSnapshotSample(
                materialized.WorldId,
                materialized.Frame,
                materialized.Actors);
            _replication.ReplicationPipeline?.ObserveRemote(in sample);
        }

        internal void HandleReliableEvents(object rawPush)
        {
            if (rawPush is WireReliableBattleEventPush push)
            {
                _reliableEvents.Handle(in push);
            }
        }

        internal void HandleConnectionClosed()
        {
            if (!_replication.IsBuilt) return;
            _operation.ConnectionRecoveryPending = true;
            Log.Warning(
                "[AuthoritativeStateRecoveryRuntime] Battle connection lost; " +
                "transport recovery is pending.");
            ReportAndExecute(
                NetworkSessionRecoverySignalKind.ConnectionLost,
                SyncHealthSeverity.Warning,
                "battle-connection-closed",
                _replication.SnapshotAdmission?.LastAcceptedFrame ?? 0);
        }

        internal void HandleConnectionEstablished()
        {
            if (!_replication.IsBuilt || !_operation.ConnectionRecoveryPending) return;
            _operation.ConnectionRecoveryPending = false;
            ReportAndExecute(
                NetworkSessionRecoverySignalKind.ConnectionRestored,
                SyncHealthSeverity.Info,
                "battle-connection-established",
                _replication.SnapshotAdmission?.LastAcceptedFrame ?? 0);
            _worldRecovery.ResetAfterReconnect();
            _replication.LastServerAckFrame = 0;
            ReportAndExecute(
                NetworkSessionRecoverySignalKind.SnapshotResyncRequired,
                SyncHealthSeverity.Error,
                "connection-re-established",
                _replication.SnapshotAdmission?.LastAcceptedFrame ?? 0);
        }

        internal void HandleAuthenticationFailed(Exception exception)
        {
            if (!_replication.IsBuilt || _transport == null) return;
            Log.Warning(
                $"[AuthoritativeStateRecoveryRuntime] Authentication failed: {exception?.Message}. " +
                "Disconnecting for transport re-authentication.");
            _operation.ConnectionRecoveryPending = true;
            ReportAndExecute(
                NetworkSessionRecoverySignalKind.ConnectionError,
                SyncHealthSeverity.Error,
                "battle-authentication-failed",
                _replication.SnapshotAdmission?.LastAcceptedFrame ?? 0,
                exception);
            try
            {
                _transport.Disconnect();
            }
            catch (Exception disconnectException)
            {
                Log.Exception(
                    disconnectException,
                    "[AuthoritativeStateRecoveryRuntime] Disconnect after authentication failure failed.");
            }
        }

        public void Dispose()
        {
            EndGeneration();
        }

        internal Task StopAsync()
        {
            return EndGenerationAsync();
        }

        private void HandleReliableTimelineInvalidated(string reason)
        {
            ReportAndExecute(
                NetworkSessionRecoverySignalKind.ReliableEventResyncRequired,
                SyncHealthSeverity.Error,
                reason,
                _replication.SnapshotAdmission?.LastAcceptedFrame ?? 0);
        }

        private async Task<bool> ExecuteFullStateRecoveryAsync(
            NetworkSessionRecoveryExecutionContext execution,
            System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var generation = _operation.Generation;
            var transport = _transport;
            if (transport == null || !IsCurrentRecoveryDecision(execution.Decision, generation))
            {
                return false;
            }

            _replication.PendingStateImport = true;
            _context?.SetGameplayInputBlocked(BattleGameplayInputBlockReason.AuthoritativeRecovery, true);
            _replication.SnapshotAdmission?.RequireFullBaseline();
            _replication.AuthoritativeSnapshotState?.Reset();
            _replication.InterpolationController?.Reset();
            _reliableEvents.RequireAuthoritativeBaseline();

            var signal = execution.Decision.Signal;
            var reason = string.IsNullOrWhiteSpace(signal.Detail)
                ? execution.Decision.Reason
                : signal.Detail;
            return await RequestFullStateSyncAsync(
                generation,
                transport,
                reason,
                signal.Frame);
        }

        private async Task<bool> RequestFullStateSyncAsync(
            int generation,
            IBattleRecoveryTransportOperations transport,
            string reason,
            int lastAuthoritativeFrame)
        {
            bool accepted;
            try
            {
                accepted = await transport.RequestFullStateSyncAsync(
                    reason,
                    lastAuthoritativeFrame);
            }
            catch (Exception ex)
            {
                if (!_operation.IsCurrent(generation) || !ReferenceEquals(transport, _transport)) return false;
                Log.Exception(
                    ex,
                    $"[AuthoritativeStateRecoveryRuntime] Full-state request failed. " +
                    $"reason={reason} frame={lastAuthoritativeFrame}");
                return false;
            }

            if (!_operation.IsCurrent(generation) || !ReferenceEquals(transport, _transport)) return false;
            if (!accepted)
            {
                Log.Warning(
                    $"[AuthoritativeStateRecoveryRuntime] Full-state request not accepted. " +
                    $"reason={reason} frame={lastAuthoritativeFrame}");
            }

            return accepted;
        }

        private void ReportAndExecute(
            NetworkSessionRecoverySignalKind kind,
            SyncHealthSeverity severity,
            string detail,
            int frame,
            Exception exception = null)
        {
            var signal = new NetworkSessionRecoverySignal(
                kind,
                severity,
                frame,
                exception,
                CreateRecoveryCorrelationContext(_operation.Generation),
                detail: detail);
            if (!_sessionRecovery.TryReport(in signal, out var decision)) return;

            if (!_operation.TryReserve(out var token, out var reservation)) return;

            _ = ExecuteAndCompleteReservationAsync(decision, token, reservation);
        }

        private static string CreateRecoveryCorrelationContext(int generation)
        {
            return $"{RecoveryCorrelationPrefix}{generation}";
        }

        private static bool IsCurrentRecoveryDecision(
            NetworkSessionRecoveryDecision decision,
            int generation)
        {
            var correlation = decision.Signal.CorrelationContext;
            if (string.IsNullOrWhiteSpace(correlation) ||
                !correlation.StartsWith(RecoveryCorrelationPrefix, StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(
                correlation,
                CreateRecoveryCorrelationContext(generation),
                StringComparison.Ordinal);
        }

        private async Task ExecuteAndCompleteReservationAsync(
            NetworkSessionRecoveryDecision decision,
            System.Threading.CancellationToken token,
            TaskCompletionSource<bool> reservation)
        {
            try
            {
                var result = await _recoveryActions.ExecuteAsync(
                        decision,
                        cancellationToken: token)
                    .ConfigureAwait(false);
                if (result.Status == NetworkSessionRecoveryExecutionStatus.Unhandled)
                {
                    Log.Warning(
                        $"[AuthoritativeStateRecoveryRuntime] Recovery action is not registered. " +
                        $"action={result.Decision.Action} signal={result.Decision.Signal.Kind}");
                }
                else if (result.Status == NetworkSessionRecoveryExecutionStatus.Failed &&
                         result.Exception != null)
                {
                    Log.Exception(
                        result.Exception,
                        $"[AuthoritativeStateRecoveryRuntime] Recovery action failed. " +
                        $"action={result.Decision.Action} signal={result.Decision.Signal.Kind}");
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Log.Exception(
                    exception,
                    "[AuthoritativeStateRecoveryRuntime] Failed to observe recovery action.");
            }
            finally
            {
                reservation.TrySetResult(true);
            }
        }

        private Task _pendingTeardown = Task.CompletedTask;

        private void EndGeneration()
        {
            _ = ObserveEndGenerationAsync(EndGenerationAsync());
        }

        private static async Task ObserveEndGenerationAsync(Task completion)
        {
            try { await completion.ConfigureAwait(false); }
            catch (Exception exception)
            {
                Log.Exception(exception,
                    "[AuthoritativeStateRecoveryRuntime] Recovery teardown failed.");
            }
        }

        private Task EndGenerationAsync()
        {
            var pendingExecution = _operation.EndAsync();

            var context = _context;
            var remoteInterpolationGeneration = _remoteInterpolationGeneration;
            _transport = null;
            _context = null;
            _remoteInterpolationGeneration = 0;
            _firstFrameReceived = null;
            _sessionRecovery.Reset();

            Exception cleanupFailure = null;
            try
            {
                _reliableEvents.Dispose();
            }
            catch (Exception exception)
            {
                cleanupFailure = exception;
            }

            try
            {
                if (context != null && remoteInterpolationGeneration != 0)
                {
                    context.EndRemoteInterpolation(remoteInterpolationGeneration);
                }
            }
            catch (Exception exception)
            {
                cleanupFailure = cleanupFailure == null
                    ? exception
                    : new AggregateException(cleanupFailure, exception);
            }

            return _pendingTeardown = Task.WhenAll(
                _pendingTeardown,
                CompleteEndGenerationAsync(pendingExecution, cleanupFailure));
        }

        private static async Task CompleteEndGenerationAsync(
            Task pendingExecution,
            Exception cleanupFailure)
        {
            Exception pendingFailure = null;
            try
            {
                await (pendingExecution ?? Task.CompletedTask).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                pendingFailure = exception;
            }

            if (cleanupFailure != null && pendingFailure != null)
            {
                throw new AggregateException(cleanupFailure, pendingFailure);
            }

            if (cleanupFailure != null) throw cleanupFailure;
            if (pendingFailure != null) throw pendingFailure;
        }

    }

    internal sealed class RecoveryGenerationOperation
    {
        private readonly object _gate = new object();
        private System.Threading.CancellationTokenSource _cancellation;
        private Task _pending = Task.CompletedTask;
        private int _generation;
        private bool _connectionRecoveryPending;

        public int Generation { get { lock (_gate) return _generation; } }
        public Task PendingExecution { get { lock (_gate) return _pending; } }
        public bool ConnectionRecoveryPending
        {
            get { lock (_gate) return _connectionRecoveryPending; }
            set { lock (_gate) _connectionRecoveryPending = value; }
        }

        public void Begin()
        {
            lock (_gate)
            {
                if (_cancellation != null)
                    throw new InvalidOperationException("Recovery generation is already active.");
                _cancellation = new System.Threading.CancellationTokenSource();
            }
        }

        public bool IsCurrent(int generation)
        {
            lock (_gate) return _cancellation != null && _generation == generation;
        }

        public bool TryReserve(out System.Threading.CancellationToken token,
            out TaskCompletionSource<bool> reservation)
        {
            lock (_gate)
            {
                if (_cancellation == null)
                {
                    token = default;
                    reservation = null;
                    return false;
                }

                token = _cancellation.Token;
                reservation = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _pending = Task.WhenAll(_pending, reservation.Task);
                return true;
            }
        }

        public Task EndAsync()
        {
            System.Threading.CancellationTokenSource cancellation;
            Task pending;
            lock (_gate)
            {
                _generation++;
                cancellation = _cancellation;
                _cancellation = null;
                pending = _pending;
                _pending = Task.CompletedTask;
                _connectionRecoveryPending = false;
            }

            if (cancellation != null && !cancellation.IsCancellationRequested)
                cancellation.Cancel();
            return CompleteAsync(pending, cancellation);
        }

        private static async Task CompleteAsync(Task pending,
            System.Threading.CancellationTokenSource cancellation)
        {
            try { await pending.ConfigureAwait(false); }
            finally { cancellation?.Dispose(); }
        }
    }
}
