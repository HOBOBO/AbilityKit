#nullable enable

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterClientBattleHandle
    {
        private const long AutomaticFullStateSyncRetrySeconds = 5L;

        private readonly ShooterClientSession _session;
        private readonly IShooterRoomGatewayRoomClient? _roomClient;
        private readonly ShooterRoomGatewayFlowResult _flow;
        private readonly NetworkSessionRecoveryActionRouter<ShooterGatewayFullStateSyncRequestResult> _recoveryActions;
        private readonly NetworkSessionRecoveryRuntime<ShooterGatewayFullStateSyncRequestResult> _recoveryRuntime;
        private readonly object _fullStateSyncGate = new object();
        private ShooterClientFullStateSyncRequestKey _lastFullStateSyncRequestKey;
        private Task<ShooterGatewayFullStateSyncRequestResult>? _fullStateSyncInFlight;
        private bool _automaticFullStateSyncAwaitingRecovery;
        private long _automaticFullStateSyncAcceptedTimestamp;
        private long _automaticFullStateSyncCoalescedRequestCount;

        public ShooterClientBattleHandle(ShooterClientSession session, ShooterRoomGatewayFlowResult flow)
            : this(session, flow, null)
        {
        }

        public ShooterClientBattleHandle(ShooterClientSession session, ShooterRoomGatewayFlowResult flow, IShooterRoomGatewayRoomClient? roomClient)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _roomClient = roomClient;
            if (string.IsNullOrWhiteSpace(flow.SessionToken))
            {
                throw new ArgumentException("sessionToken is required.", nameof(flow));
            }

            if (string.IsNullOrWhiteSpace(flow.BattleId))
            {
                throw new ArgumentException("battleId is required.", nameof(flow));
            }

            if (flow.PlayerId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(flow));
            }

            _flow = flow;
            _recoveryActions = new NetworkSessionRecoveryActionRouter<ShooterGatewayFullStateSyncRequestResult>(
                    new NetworkSessionRecoveryActionRouterOptions<ShooterGatewayFullStateSyncRequestResult>
                    {
                        // 保留 Shooter 既有请求异常语义，由上层决定重试或终止流程。
                        HandlerFailurePolicy = NetworkSessionRecoveryHandlerFailurePolicy.Throw,
                        CancellationPolicy = NetworkSessionRecoveryCancellationPolicy.Throw
                    })
                .Register(
                    NetworkSessionRecoveryAction.RequestFullSnapshot,
                    ExecuteFullSnapshotRecoveryAsync)
                .Register(
                    NetworkSessionRecoveryAction.RestoreReliableEventBaseline,
                    ExecuteFullSnapshotRecoveryAsync);
            _recoveryRuntime = new NetworkSessionRecoveryRuntime<ShooterGatewayFullStateSyncRequestResult>(
                _recoveryActions,
                _session.SessionRecoveryCoordinator,
                new NetworkSessionRecoveryRuntimeOptions
                {
                    // Shooter 仍在输入提交或显式恢复入口执行请求，避免改变既有网络调用时机。
                    ExecutionMode = NetworkSessionRecoveryExecutionMode.Manual,
                    CancelSupersededExecution = true,
                    CancelExecutionOnReset = true,
                    SuppressStaleExecutionCompletion = true
                });
        }

        public ShooterClientSession Session => _session;

        public ShooterRoomGatewayFlowResult Flow => _flow;

        public string RoomId => _flow.RoomId;

        public ulong NumericRoomId => _flow.NumericRoomId;

        public string BattleId => _flow.BattleId;

        public ulong WorldId => _flow.WorldId;

        public uint PlayerId => _flow.PlayerId;

        public int CurrentFrame => _session.CurrentFrame;

        /// <summary>当前 Shooter 恢复动作生命周期诊断。</summary>
        public NetworkSessionRecoveryRuntimeDiagnostics RecoveryRuntimeDiagnostics =>
            _recoveryRuntime.GetRuntimeDiagnostics();

        public long AutomaticFullStateSyncCoalescedRequestCount =>
            Interlocked.Read(ref _automaticFullStateSyncCoalescedRequestCount);

        public ShooterGatewayBattleInputContext CreateCurrentFrameInputContext()
        {
            return _flow.CreateBattleInputContext(_session.GatewayInputFrame);
        }

        public async Task<ShooterClientGatewayInputSubmitResult> SubmitLocalInputToGatewayAsync(
            ShooterPlayerCommand command,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _session.SubmitLocalInputToGatewayAsync(CreateCurrentFrameInputContext(), command, timeout, cancellationToken).ConfigureAwait(false);
            await RequestFullSnapshotResyncIfNeededAsync(timeout, cancellationToken).ConfigureAwait(false);
            return result;
        }

        public async Task<ShooterClientGatewayInputSubmitResult> SubmitAcceptedInputToGatewayAsync(
            ShooterClientInputSubmitResult local,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var context = _flow.CreateBattleInputContext(local.RequestedFrame);
            var result = await _session.SubmitAcceptedInputToGatewayAsync(context, local, timeout, cancellationToken).ConfigureAwait(false);
            await RequestFullSnapshotResyncIfNeededAsync(timeout, cancellationToken).ConfigureAwait(false);
            return result;
        }

        public async Task<ShooterSnapshotApplyResult> ApplyGatewayPushAndRequestFullSnapshotResyncIfNeededAsync(
            uint opCode,
            ArraySegment<byte> payload,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var result = _session.ApplyGatewayPush(opCode, payload);
            ClearAutomaticFullStateSyncIfRecovered();
            await RequestFullSnapshotResyncIfNeededAsync(timeout, cancellationToken).ConfigureAwait(false);
            return result;
        }

        public async Task<ShooterGatewayFullStateSyncRequestResult> RequestFullSnapshotResyncIfNeededAsync(
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var decision = _session.EvaluateRecoveryDecision();
            ClearAutomaticFullStateSyncIfRecovered();
            if (_recoveryActions.CanExecute(decision.Action))
            {
                var execution = await _recoveryRuntime.ExecuteCurrentAsync(
                    timeout,
                    cancellationToken).ConfigureAwait(false);
                return execution.HasValue
                    ? execution.Value
                    : ShooterGatewayFullStateSyncRequestResult.NotRequested;
            }

            // 更高优先级动作应交给 Launcher 或产品流程处理，不能再退化为低优先级快照请求。
            if (decision.HasAction)
            {
                return ShooterGatewayFullStateSyncRequestResult.NotRequested;
            }

            return ShouldRequestFullStateSync()
                ? await RequestFullSnapshotAsync(
                    CreateFullStateSyncRequest(),
                    timeout,
                    cancellationToken,
                    coalesceAutomaticRecovery: true).ConfigureAwait(false)
                : ShooterGatewayFullStateSyncRequestResult.NotRequested;
        }

        /// <summary>执行当前统一恢复决策，并返回结构化路由结果。</summary>
        public Task<NetworkSessionRecoveryExecutionResult<ShooterGatewayFullStateSyncRequestResult>> ExecuteRecoveryDecisionAsync(
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            _session.EvaluateRecoveryDecision();
            return _recoveryRuntime.ExecuteCurrentAsync(timeout, cancellationToken);
        }

        public Task<ShooterGatewayStateSyncSubscriptionResult> SubscribeStateSyncAsync(
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (_roomClient == null)
            {
                return Task.FromResult(new ShooterGatewayStateSyncSubscriptionResult(false, "room client unavailable"));
            }

            return _roomClient.SubscribeStateSyncAsync(
                new ShooterGatewayStateSyncSubscriptionRequest(
                    _flow.SessionToken,
                    _flow.BattleId,
                    _flow.RoomId,
                    _session.ReliableEventEpoch,
                    _session.LastReliableEventAck),
                timeout,
                cancellationToken);
        }

        public Task<ShooterGatewayReliableBattleEventAckResult> AcknowledgeReliableBattleEventsAsync(
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (_roomClient == null || string.IsNullOrWhiteSpace(_session.ReliableEventEpoch))
            {
                return Task.FromResult(new ShooterGatewayReliableBattleEventAckResult(false, 0L, "reliable event cursor unavailable"));
            }

            return _roomClient.AcknowledgeReliableBattleEventsAsync(
                new ShooterGatewayReliableBattleEventAckRequest(
                    _flow.SessionToken,
                    _flow.BattleId,
                    _flow.RoomId,
                    _session.ReliableEventEpoch,
                    _session.LastReliableEventAck),
                timeout,
                cancellationToken);
        }

        public Task<ShooterGatewayFullStateSyncRequestResult> RequestFullSnapshotBaselineAsync(
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return RequestFullSnapshotBaselineAsync(
                ShooterClientResyncReason.None.ToString(),
                timeout,
                cancellationToken);
        }

        public Task<ShooterGatewayFullStateSyncRequestResult> RequestFullSnapshotBaselineAsync(
            string reason,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return RequestFullSnapshotAsync(CreateBaselineFullStateSyncRequest(reason), timeout, cancellationToken);
        }

        public ShooterGatewayFullStateSyncRequest CreateFullStateSyncRequest()
        {
            if (_session.NeedsReliableEventResync)
            {
                return new ShooterGatewayFullStateSyncRequest(
                    _flow.SessionToken,
                    _flow.BattleId,
                    _flow.RoomId,
                    _flow.WorldId,
                    _session.CurrentFrame,
                    _session.CurrentFrame,
                    0u,
                    0u,
                    "ReliableEventGap");
            }

            if (_session.NeedsFullSnapshotResync)
            {
                return new ShooterGatewayFullStateSyncRequest(
                    _flow.SessionToken,
                    _flow.BattleId,
                    _flow.RoomId,
                    _flow.WorldId,
                    _session.LastResyncClientFrame,
                    _session.LastResyncAuthoritativeFrame,
                    _session.LastResyncClientStateHash,
                    _session.LastResyncAuthoritativeStateHash,
                    _session.LastResyncReason.ToString());
            }

            if (_session.Presentation.NeedsPureStateFullBaselineResync)
            {
                var reason = $"PureState{_session.Presentation.LastPureStateResyncReason}";
                return new ShooterGatewayFullStateSyncRequest(
                    _flow.SessionToken,
                    _flow.BattleId,
                    _flow.RoomId,
                    _flow.WorldId,
                    _session.Presentation.LastPureStateAppliedFrame,
                    _session.Presentation.LastPureStateResyncFrame,
                    _session.Presentation.LastPureStateAppliedStateHash,
                    _session.Presentation.LastPureStateResyncStateHash,
                    reason);
            }

            return CreateBaselineFullStateSyncRequest(ShooterClientResyncReason.None.ToString());
        }

        private ShooterGatewayFullStateSyncRequest CreateBaselineFullStateSyncRequest(string reason)
        {
            return new ShooterGatewayFullStateSyncRequest(
                _flow.SessionToken,
                _flow.BattleId,
                _flow.RoomId,
                _flow.WorldId,
                _session.CurrentFrame,
                _session.CurrentFrame,
                0u,
                0u,
                string.IsNullOrWhiteSpace(reason) ? ShooterClientResyncReason.None.ToString() : reason);
        }

        private Task<ShooterGatewayFullStateSyncRequestResult> RequestFullSnapshotAsync(
            ShooterGatewayFullStateSyncRequest request,
            TimeSpan? timeout,
            CancellationToken cancellationToken,
            bool coalesceAutomaticRecovery = false)
        {
            if (_roomClient == null)
            {
                return Task.FromResult(ShooterGatewayFullStateSyncRequestResult.NotRequested);
            }

            var requestKey = ShooterClientFullStateSyncRequestKey.FromRequest(in request);
            lock (_fullStateSyncGate)
            {
                var nowTimestamp = Stopwatch.GetTimestamp();
                if (coalesceAutomaticRecovery)
                {
                    if (!ShouldRequestFullStateSync())
                    {
                        _automaticFullStateSyncAwaitingRecovery = false;
                    }
                    else if (_automaticFullStateSyncAwaitingRecovery &&
                             nowTimestamp - _automaticFullStateSyncAcceptedTimestamp < AutomaticFullStateSyncRetrySeconds * Stopwatch.Frequency)
                    {
                        Interlocked.Increment(ref _automaticFullStateSyncCoalescedRequestCount);
                        return Task.FromResult(ShooterGatewayFullStateSyncRequestResult.NotRequested);
                    }
                }
                else if (requestKey.Equals(_lastFullStateSyncRequestKey))
                {
                    return Task.FromResult(ShooterGatewayFullStateSyncRequestResult.NotRequested);
                }

                if (_fullStateSyncInFlight != null)
                {
                    return _fullStateSyncInFlight;
                }

                var completion = new TaskCompletionSource<ShooterGatewayFullStateSyncRequestResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _fullStateSyncInFlight = completion.Task;
                _ = ExecuteFullSnapshotRequestAsync(
                    _roomClient,
                    request,
                    requestKey,
                    timeout,
                    cancellationToken,
                    coalesceAutomaticRecovery,
                    completion);
                return completion.Task;
            }
        }

        private Task<ShooterGatewayFullStateSyncRequestResult> ExecuteFullSnapshotRecoveryAsync(
            NetworkSessionRecoveryExecutionContext context,
            CancellationToken cancellationToken)
        {
            var timeout = context.State is TimeSpan configuredTimeout
                ? configuredTimeout
                : (TimeSpan?)null;
            return RequestFullSnapshotAsync(
                CreateFullStateSyncRequest(),
                timeout,
                cancellationToken,
                coalesceAutomaticRecovery: true);
        }

        private async Task ExecuteFullSnapshotRequestAsync(
            IShooterRoomGatewayRoomClient roomClient,
            ShooterGatewayFullStateSyncRequest request,
            ShooterClientFullStateSyncRequestKey requestKey,
            TimeSpan? timeout,
            CancellationToken cancellationToken,
            bool coalesceAutomaticRecovery,
            TaskCompletionSource<ShooterGatewayFullStateSyncRequestResult> completion)
        {
            try
            {
                var result = await _session.RequestFullSnapshotResyncAsync(
                    roomClient,
                    request,
                    timeout,
                    cancellationToken).ConfigureAwait(false);
                if (result.Accepted)
                {
                    lock (_fullStateSyncGate)
                    {
                        _lastFullStateSyncRequestKey = requestKey;
                        if (coalesceAutomaticRecovery)
                        {
                            _automaticFullStateSyncAwaitingRecovery = true;
                            _automaticFullStateSyncAcceptedTimestamp = Stopwatch.GetTimestamp();
                        }
                    }
                }

                completion.TrySetResult(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                lock (_fullStateSyncGate)
                {
                    if (ReferenceEquals(_fullStateSyncInFlight, completion.Task))
                    {
                        _fullStateSyncInFlight = null;
                    }
                }
            }
        }

        private bool ShouldRequestFullStateSync()
        {
            return _session.NeedsReliableEventResync
                || _session.NeedsFullSnapshotResync
                || _session.Presentation.NeedsPureStateFullBaselineResync;
        }

        private void ClearAutomaticFullStateSyncIfRecovered()
        {
            if (ShouldRequestFullStateSync())
            {
                return;
            }

            lock (_fullStateSyncGate)
            {
                _automaticFullStateSyncAwaitingRecovery = false;
            }
        }

        public Task<ShooterClientGatewayInputSubmitResult> SubmitLocalInputToGatewayAsync(
            float moveX,
            float moveY,
            float aimX,
            float aimY,
            bool fire,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return SubmitLocalInputToGatewayAsync(moveX, moveY, aimX, aimY, fire, ShooterPlayerAttackSlots.Primary, timeout, cancellationToken);
        }

        public Task<ShooterClientGatewayInputSubmitResult> SubmitLocalInputToGatewayAsync(
            float moveX,
            float moveY,
            float aimX,
            float aimY,
            bool fire,
            int attackSlot,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var command = ShooterClientInputBuilder.CreateCommand(GetPlayerIdAsInt(), moveX, moveY, aimX, aimY, fire, attackSlot);
            return SubmitLocalInputToGatewayAsync(command, timeout, cancellationToken);
        }

        private int GetPlayerIdAsInt()
        {
            if (_flow.PlayerId > int.MaxValue)
            {
                throw new InvalidOperationException("playerId is too large for ShooterPlayerCommand.");
            }

            return (int)_flow.PlayerId;
        }
    }

    internal readonly struct ShooterClientFullStateSyncRequestKey : IEquatable<ShooterClientFullStateSyncRequestKey>
    {
        private readonly string _sessionToken;
        private readonly string _battleId;
        private readonly string _roomId;
        private readonly ulong _worldId;
        private readonly int _clientFrame;
        private readonly int _lastAuthoritativeFrame;
        private readonly uint _clientStateHash;
        private readonly uint _authoritativeStateHash;
        private readonly string _reason;

        private ShooterClientFullStateSyncRequestKey(
            string sessionToken,
            string battleId,
            string roomId,
            ulong worldId,
            int clientFrame,
            int lastAuthoritativeFrame,
            uint clientStateHash,
            uint authoritativeStateHash,
            string reason)
        {
            _sessionToken = sessionToken ?? string.Empty;
            _battleId = battleId ?? string.Empty;
            _roomId = roomId ?? string.Empty;
            _worldId = worldId;
            _clientFrame = clientFrame;
            _lastAuthoritativeFrame = lastAuthoritativeFrame;
            _clientStateHash = clientStateHash;
            _authoritativeStateHash = authoritativeStateHash;
            _reason = reason ?? string.Empty;
        }

        public static ShooterClientFullStateSyncRequestKey FromRequest(in ShooterGatewayFullStateSyncRequest request)
        {
            return new ShooterClientFullStateSyncRequestKey(
                request.SessionToken,
                request.BattleId,
                request.RoomId,
                request.WorldId,
                request.ClientFrame,
                request.LastAuthoritativeFrame,
                request.ClientStateHash,
                request.AuthoritativeStateHash,
                request.Reason);
        }

        public bool Equals(ShooterClientFullStateSyncRequestKey other)
        {
            return string.Equals(_sessionToken, other._sessionToken, StringComparison.Ordinal)
                && string.Equals(_battleId, other._battleId, StringComparison.Ordinal)
                && string.Equals(_roomId, other._roomId, StringComparison.Ordinal)
                && _worldId == other._worldId
                && _clientFrame == other._clientFrame
                && _lastAuthoritativeFrame == other._lastAuthoritativeFrame
                && _clientStateHash == other._clientStateHash
                && _authoritativeStateHash == other._authoritativeStateHash
                && string.Equals(_reason, other._reason, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is ShooterClientFullStateSyncRequestKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StringComparer.Ordinal.GetHashCode(_sessionToken);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(_battleId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(_roomId);
                hash = (hash * 397) ^ _worldId.GetHashCode();
                hash = (hash * 397) ^ _clientFrame;
                hash = (hash * 397) ^ _lastAuthoritativeFrame;
                hash = (hash * 397) ^ (int)_clientStateHash;
                hash = (hash * 397) ^ (int)_authoritativeStateHash;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(_reason);
                return hash;
            }
        }
    }
}
