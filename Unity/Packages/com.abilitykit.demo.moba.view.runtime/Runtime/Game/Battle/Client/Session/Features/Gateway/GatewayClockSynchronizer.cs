using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Owns the gateway clock synchronization loop for one preparation generation.
    /// </summary>
    internal sealed class GatewayClockSynchronizer : IDisposable
    {
        private const int NotifyThreshold = 3;

        private readonly object _gate = new object();
        private CancellationTokenSource _cancellation;
        private Task _task;
        private int _generation;
        private GatewayTimeSyncEwma _estimate;

        internal Task Task
        {
            get
            {
                lock (_gate) return _task;
            }
        }

        internal GatewayTimeSyncEwma Estimate
        {
            get
            {
                lock (_gate) return _estimate;
            }
        }

        internal void Start(
            IGatewayRoomClient client,
            in BattleStartPlanTimeSyncOptions options,
            Action<GatewayTimeSyncEwma, GatewayTimeSyncRuntimeOptions> samplePublished,
            Action<Exception> failurePublished)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            CancellationTokenSource previous;
            int generation;
            CancellationToken token;
            var runtimeOptions = GatewayTimeSyncHelper.ResolveRuntimeOptions(options);

            lock (_gate)
            {
                previous = _cancellation;
                _generation++;
                generation = _generation;
                _estimate = default;
                _cancellation = new CancellationTokenSource();
                token = _cancellation.Token;
                _task = System.Threading.Tasks.Task.Run(
                    () => RunAsync(
                        generation,
                        client,
                        runtimeOptions,
                        samplePublished,
                        failurePublished,
                        token),
                    token);
            }

            CancelAndDispose(previous);
        }

        private async Task RunAsync(
            int generation,
            IGatewayRoomClient client,
            GatewayTimeSyncRuntimeOptions options,
            Action<GatewayTimeSyncEwma, GatewayTimeSyncRuntimeOptions> samplePublished,
            Action<Exception> failurePublished,
            CancellationToken token)
        {
            var failureCount = 0;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var clientSendTicks = Stopwatch.GetTimestamp();
                    var result = await client.TimeSyncAsync(
                        options.OpCode,
                        clientSendTicks,
                        TimeSpan.FromMilliseconds(options.TimeoutMs),
                        token);
                    var clientReceiveTicks = Stopwatch.GetTimestamp();
                    var sample = GatewayTimeSyncHelper.CalculateSample(
                        clientSendTicks,
                        clientReceiveTicks,
                        result.ServerNowTicks,
                        result.ServerTickFrequency,
                        Stopwatch.Frequency);

                    GatewayTimeSyncEwma estimate;
                    lock (_gate)
                    {
                        if (!IsCurrentGeneration(generation, token)) return;

                        _estimate = GatewayTimeSyncHelper.ApplySample(
                            _estimate.HasClockSync,
                            _estimate.ClockOffsetSecondsEwma,
                            _estimate.RttSecondsEwma,
                            _estimate.Samples,
                            in sample,
                            options.Alpha);
                        failureCount = 0;
                        estimate = _estimate;
                    }

                    samplePublished?.Invoke(estimate, options);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    var shouldNotify = false;
                    lock (_gate)
                    {
                        if (!IsCurrentGeneration(generation, token)) return;

                        failureCount++;
                        GatewaySessionFailurePolicy.LogTimeSyncFailure(exception, failureCount);
                        shouldNotify = GatewaySessionFailurePolicy.ShouldNotifyTimeSyncFailure(
                            exception,
                            failureCount,
                            NotifyThreshold);
                    }

                    if (shouldNotify) failurePublished?.Invoke(exception);
                }

                try
                {
                    await Task.Delay(options.IntervalMs, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private bool IsCurrentGeneration(int generation, CancellationToken token)
        {
            return generation == _generation &&
                   _cancellation != null &&
                   _cancellation.Token == token &&
                   !token.IsCancellationRequested;
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
            }

            CancelAndDispose(cancellation);
        }

        internal void ClearEstimate()
        {
            lock (_gate) _estimate = default;
        }

        public void Dispose()
        {
            StopWork();
            ClearEstimate();
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
