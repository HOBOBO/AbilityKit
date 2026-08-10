using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Core.Logging;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Owns one cancellable gateway room preparation transaction.
    /// </summary>
    internal sealed class GatewayPreparationRuntime : IDisposable
    {
        private const uint GuestLoginOpCode = 100;

        private readonly object _gate = new object();
        private readonly GatewayClockSynchronizer _clock;
        private readonly Dictionary<WorldId, GatewayWorldStartAnchor> _worldStartAnchors =
            new Dictionary<WorldId, GatewayWorldStartAnchor>();

        private CancellationTokenSource _cancellation;
        private Task _task;
        private int _generation;

        internal GatewayPreparationRuntime(GatewayClockSynchronizer clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        internal Task Task
        {
            get
            {
                lock (_gate) return _task;
            }
        }

        internal IReadOnlyDictionary<WorldId, GatewayWorldStartAnchor> WorldStartAnchors
        {
            get
            {
                lock (_gate)
                {
                    return new Dictionary<WorldId, GatewayWorldStartAnchor>(
                        _worldStartAnchors);
                }
            }
        }

        internal void Start(
            IConnection connection,
            IGatewayRoomClient client,
            BattleStartPlan plan,
            Action<BattleStartPlan> planPublished,
            Action<GatewayTimeSyncEwma, GatewayTimeSyncRuntimeOptions> clockSamplePublished,
            Action<Exception> clockFailurePublished)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (client == null) throw new ArgumentNullException(nameof(client));

            CancellationTokenSource previous;
            int generation;
            CancellationToken token;

            lock (_gate)
            {
                previous = _cancellation;
                _generation++;
                generation = _generation;
                _worldStartAnchors.Clear();
                _clock.Dispose();
                _cancellation = new CancellationTokenSource();
                token = _cancellation.Token;
                _task = RunAsync(
                    generation,
                    connection,
                    client,
                    plan,
                    planPublished,
                    clockSamplePublished,
                    clockFailurePublished,
                    token);
            }

            CancelAndDispose(previous);
        }

        private async Task RunAsync(
            int generation,
            IConnection connection,
            IGatewayRoomClient client,
            BattleStartPlan plan,
            Action<BattleStartPlan> planPublished,
            Action<GatewayTimeSyncEwma, GatewayTimeSyncRuntimeOptions> clockSamplePublished,
            Action<Exception> clockFailurePublished,
            CancellationToken token)
        {
            await WaitForConnectionAsync(connection, token);
            ThrowIfStale(generation, token);

            var preparedPlan = await EnsureSessionTokenAsync(client, plan, token);
            ThrowIfStale(generation, token);

            GatewayWorldStartAnchor anchor = default;
            WorldId anchorWorldId = default;
            var hasAnchor = false;
            var gateway = preparedPlan.Gateway;

            if (gateway.AutoCreateRoom)
            {
                var result = await client.CreateRoomAsync(
                    gateway.SessionToken,
                    gateway.Region,
                    gateway.ServerId,
                    string.IsNullOrEmpty(preparedPlan.World.WorldType) ? "battle" : preparedPlan.World.WorldType,
                    string.Empty,
                    true,
                    10,
                    null,
                    cancellationToken: token);
                ThrowIfStale(generation, token);

                var worldId = GatewayRoomPreparationHelper.ResolveCreatedRoomWorldId(in result);
                preparedPlan = preparedPlan.WithGatewayRoom(worldId, result.NumericRoomId);
                gateway = preparedPlan.Gateway;
                var joinResult = await client.JoinRoomAsync(
                    gateway.SessionToken,
                    gateway.Region,
                    gateway.ServerId,
                    GatewayRoomPreparationHelper.ResolveCreatedRoomJoinRoomId(
                        in result,
                        gateway.NumericRoomId),
                    cancellationToken: token);
                ThrowIfStale(generation, token);

                anchor = joinResult.WorldStartAnchor;
                anchorWorldId = new WorldId(preparedPlan.World.WorldId);
                hasAnchor = anchor.ServerTickFrequency != 0;
            }
            else if (gateway.AutoJoinRoom)
            {
                var joinRoomId = GatewayRoomPreparationHelper.ResolveJoinRoomId(preparedPlan);
                var result = await client.JoinRoomAsync(
                    gateway.SessionToken,
                    gateway.Region,
                    gateway.ServerId,
                    joinRoomId,
                    cancellationToken: token);
                ThrowIfStale(generation, token);

                var worldId = GatewayRoomPreparationHelper.ResolveJoinedRoomWorldId(in result, joinRoomId);
                preparedPlan = preparedPlan.WithGatewayRoom(worldId, result.NumericRoomId);
                anchor = result.WorldStartAnchor;
                anchorWorldId = new WorldId(preparedPlan.World.WorldId);
                hasAnchor = anchor.ServerTickFrequency != 0;
            }

            lock (_gate)
            {
                ThrowIfStale(generation, token);
                if (hasAnchor)
                {
                    _worldStartAnchors[anchorWorldId] = anchor;
                }
            }

            PublishPlanIfCurrent(generation, token, planPublished, preparedPlan);
            lock (_gate)
            {
                ThrowIfStale(generation, token);
                _clock.Start(
                    client,
                    preparedPlan.TimeSync,
                    (estimate, options) => PublishClockSampleIfCurrent(
                        generation,
                        token,
                        clockSamplePublished,
                        estimate,
                        options),
                    exception => PublishClockFailureIfCurrent(
                        generation,
                        token,
                        clockFailurePublished,
                        exception));
            }
        }

        private static async Task WaitForConnectionAsync(
            IConnection connection,
            CancellationToken token)
        {
            while (connection.State == ConnectionState.Connecting)
            {
                token.ThrowIfCancellationRequested();
                await System.Threading.Tasks.Task.Yield();
            }

            token.ThrowIfCancellationRequested();
            if (connection.State != ConnectionState.Connected)
            {
                throw new InvalidOperationException(
                    $"Gateway room connection not connected. state={connection.State}");
            }

            Log.Info("[GatewayPreparationRuntime] Gateway room connection established.");
        }

        private static async Task<BattleStartPlan> EnsureSessionTokenAsync(
            IGatewayRoomClient client,
            BattleStartPlan plan,
            CancellationToken token)
        {
            if (!string.IsNullOrWhiteSpace(plan.Gateway.SessionToken)) return plan;

            var sessionToken = await client.GuestLoginAsync(
                GuestLoginOpCode,
                cancellationToken: token);
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                throw new InvalidOperationException(
                    "Gateway guest login failed: sessionToken is empty.");
            }

            return plan.WithGatewaySessionToken(sessionToken);
        }

        private void ThrowIfStale(int generation, CancellationToken token)
        {
            if (!IsCurrentGeneration(generation, token))
            {
                throw new OperationCanceledException(token);
            }
        }

        private bool IsCurrentGeneration(int generation, CancellationToken token)
        {
            return generation == _generation &&
                   _cancellation != null &&
                   _cancellation.Token == token &&
                   !token.IsCancellationRequested;
        }

        private void PublishPlanIfCurrent(
            int generation,
            CancellationToken token,
            Action<BattleStartPlan> published,
            BattleStartPlan plan)
        {
            lock (_gate)
            {
                if (!IsCurrentGeneration(generation, token)) return;
            }

            published?.Invoke(plan);
        }

        private void PublishClockSampleIfCurrent(
            int generation,
            CancellationToken token,
            Action<GatewayTimeSyncEwma, GatewayTimeSyncRuntimeOptions> published,
            GatewayTimeSyncEwma estimate,
            GatewayTimeSyncRuntimeOptions options)
        {
            lock (_gate)
            {
                if (!IsCurrentGeneration(generation, token)) return;
            }

            published?.Invoke(estimate, options);
        }

        private void PublishClockFailureIfCurrent(
            int generation,
            CancellationToken token,
            Action<Exception> published,
            Exception exception)
        {
            lock (_gate)
            {
                if (!IsCurrentGeneration(generation, token)) return;
            }

            published?.Invoke(exception);
        }

        internal void StopWork()
        {
            CancellationTokenSource cancellation;
            lock (_gate)
            {
                _generation++;
                cancellation = _cancellation;
                _cancellation = null;
                _task = null;
                _clock.StopWork();
            }

            CancelAndDispose(cancellation);
        }

        internal bool TryGetWorldStartAnchor(
            WorldId worldId,
            out GatewayWorldStartAnchor anchor)
        {
            lock (_gate)
            {
                anchor = default;
                return !string.IsNullOrEmpty(worldId.Value) &&
                       _worldStartAnchors.TryGetValue(worldId, out anchor) &&
                       anchor.ServerTickFrequency != 0;
            }
        }

        internal void ClearSessionData()
        {
            lock (_gate) _worldStartAnchors.Clear();
            _clock.ClearEstimate();
        }

        public void Dispose()
        {
            StopWork();
            ClearSessionData();
        }

        private static void CancelAndDispose(CancellationTokenSource cancellation)
        {
            if (cancellation == null) return;
            try
            {
                if (!cancellation.IsCancellationRequested) cancellation.Cancel();
            }
            finally
            {
                cancellation.Dispose();
            }
        }
    }
}
