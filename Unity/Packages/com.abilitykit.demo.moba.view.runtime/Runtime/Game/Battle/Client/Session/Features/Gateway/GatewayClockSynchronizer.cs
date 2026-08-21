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
        private Task _pendingStop = System.Threading.Tasks.Task.CompletedTask;
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

        internal int Start(
            IGatewayClockCapability client,
            in BattleStartPlanTimeSyncOptions options,
            Action<GatewayTimeSyncEwma, GatewayTimeSyncRuntimeOptions> samplePublished,
            Action<Exception> failurePublished)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            CancellationTokenSource previousCancellation;
            Task previousTask;
            Task precedingStop;
            TaskCompletionSource<bool> stopCompletion;
            int generation;
            CancellationToken token;
            var runtimeOptions = GatewayTimeSyncHelper.ResolveRuntimeOptions(options);

            lock (_gate)
            {
                previousCancellation = _cancellation;
                previousTask = _task;
                precedingStop = _pendingStop;
                stopCompletion = CreateStopCompletion();
                _pendingStop = stopCompletion.Task;
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

            BeginDrain(
                precedingStop,
                previousTask,
                previousCancellation,
                stopCompletion);
            return generation;
        }

        private async Task RunAsync(
            int generation,
            IGatewayClockCapability client,
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
            _ = StopWorkAsync();
        }

        internal Task StopWorkAsync()
        {
            return StopWorkAsync(expectedGeneration: null);
        }

        internal Task StopWorkAsync(int expectedGeneration)
        {
            return StopWorkAsync((int?)expectedGeneration);
        }

        private Task StopWorkAsync(int? expectedGeneration)
        {
            CancellationTokenSource cancellation;
            Task task;
            Task precedingStop;
            TaskCompletionSource<bool> stopCompletion;

            lock (_gate)
            {
                if (expectedGeneration.HasValue &&
                    expectedGeneration.Value != _generation)
                {
                    return System.Threading.Tasks.Task.CompletedTask;
                }

                _generation++;
                cancellation = _cancellation;
                task = _task;
                _cancellation = null;
                _task = null;
                precedingStop = _pendingStop;
                stopCompletion = CreateStopCompletion();
                _pendingStop = stopCompletion.Task;
            }

            BeginDrain(precedingStop, task, cancellation, stopCompletion);
            return stopCompletion.Task;
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

        private static TaskCompletionSource<bool> CreateStopCompletion()
        {
            return new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static void BeginDrain(
            Task precedingStop,
            Task task,
            CancellationTokenSource cancellation,
            TaskCompletionSource<bool> completion)
        {
            Cancel(cancellation);
            _ = CompleteDrainAsync(
                precedingStop,
                task,
                cancellation,
                completion);
        }

        private static async Task CompleteDrainAsync(
            Task precedingStop,
            Task task,
            CancellationTokenSource cancellation,
            TaskCompletionSource<bool> completion)
        {
            try
            {
                await System.Threading.Tasks.Task.WhenAll(
                        precedingStop ?? System.Threading.Tasks.Task.CompletedTask,
                        AwaitOwnedTaskAsync(task, cancellation))
                    .ConfigureAwait(false);
                completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                cancellation?.Dispose();
            }
        }

        private static async Task AwaitOwnedTaskAsync(
            Task task,
            CancellationTokenSource cancellation)
        {
            if (task == null) return;

            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                cancellation != null && cancellation.IsCancellationRequested)
            {
            }
        }

        private static void Cancel(CancellationTokenSource cancellation)
        {
            if (cancellation != null && !cancellation.IsCancellationRequested)
            {
                cancellation.Cancel();
            }
        }
    }
}
