using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Sdk.Tests;

public sealed class ProductionCheckpointStoreTests
{
    [Fact]
    public void FileStoreRoundTripsAndRemovesCheckpoint()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "abilitykit-checkpoint-" + Guid.NewGuid().ToString("N") + ".dat");
        try
        {
            var store = new FileReliableEventCheckpointStore(path);
            var checkpoint = new ReliableEventCheckpoint("battle-1", "epoch-1", 7L);
            store.Save(in checkpoint);

            Assert.True(store.TryLoad("battle-1", out var loaded));
            Assert.Equal(checkpoint.LastAcknowledgedSequence, loaded.LastAcknowledgedSequence);
            Assert.True(store.Remove("battle-1"));
            Assert.False(store.TryLoad("battle-1", out _));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void DelegatingStoreUsesAllProvidedOperations()
    {
        ReliableEventCheckpoint? current = null;
        var store = new DelegatingReliableEventCheckpointStore(
            streamId => current.HasValue && current.Value.StreamId == streamId ? current : null,
            value => current = value,
            streamId =>
            {
                if (!current.HasValue || current.Value.StreamId != streamId) return false;
                current = null;
                return true;
            });
        var checkpoint = new ReliableEventCheckpoint("battle-1", "epoch-1", 2L);

        store.Save(in checkpoint);

        Assert.True(store.TryLoad("battle-1", out var loaded));
        Assert.Equal(2L, loaded.LastAcknowledgedSequence);
        Assert.True(store.Remove("battle-1"));
    }

    [Fact]
    public async Task DelegatingStoreInvokesProvidedFlushOperation()
    {
        var flushCount = 0;
        var store = new DelegatingReliableEventCheckpointStore(
            _ => null,
            _ => { },
            _ => false,
            _ =>
            {
                flushCount++;
                return Task.CompletedTask;
            });

        await store.FlushAsync();

        Assert.Equal(1, flushCount);
    }

    [Fact]
    public async Task BufferedStoreFlushesLatestValueAndReportsNoFailure()
    {
        var inner = new InMemoryReliableEventCheckpointStore();
        var buffered = new BufferedReliableEventCheckpointStore(inner);
        try
        {
            var first = new ReliableEventCheckpoint("battle-1", "epoch-1", 1L);
            var latest = new ReliableEventCheckpoint("battle-1", "epoch-1", 9L);
            buffered.Save(in first);
            buffered.Save(in latest);

            await buffered.FlushAsync();

            Assert.True(inner.TryLoad("battle-1", out var loaded));
            Assert.Equal(9L, loaded.LastAcknowledgedSequence);
            Assert.Null(buffered.LastFailure);
            Assert.Equal(0, buffered.FailureCount);
        }
        finally
        {
            buffered.Dispose();
        }
    }

    [Fact]
    public async Task BufferedStoreRemoveInvalidatesQueuedWrite()
    {
        var inner = new InMemoryReliableEventCheckpointStore();
        var buffered = new BufferedReliableEventCheckpointStore(inner);
        try
        {
            var checkpoint = new ReliableEventCheckpoint("battle-1", "epoch-1", 4L);
            buffered.Save(in checkpoint);
            buffered.Remove("battle-1");

            await buffered.FlushAsync();

            Assert.False(inner.TryLoad("battle-1", out _));
        }
        finally
        {
            buffered.Dispose();
        }
    }

    [Fact]
    public async Task BufferedStorePublishesBackgroundFailureWithoutBreakingSaveCaller()
    {
        var failures = new List<ReliableEventCheckpointStoreFailure>();
        var inner = new ThrowingCheckpointStore();
        var buffered = new BufferedReliableEventCheckpointStore(inner);
        buffered.Failure += failures.Add;
        try
        {
            var checkpoint = new ReliableEventCheckpoint("battle-1", "epoch-1", 1L);
            buffered.Save(in checkpoint);
            await buffered.FlushAsync();

            Assert.Equal(1, buffered.FailureCount);
            Assert.Single(failures);
            Assert.Equal("save", failures[0].Operation);
        }
        finally
        {
            buffered.Dispose();
        }
    }

    [Fact]
    public async Task LifecycleCoordinatorRecordsSuccessfulFlushTrigger()
    {
        var flushCount = 0;
        var store = new DelegatingReliableEventCheckpointStore(
            _ => null,
            _ => { },
            _ => false,
            _ =>
            {
                flushCount++;
                return Task.CompletedTask;
            });
        var coordinator = new ReliableEventCheckpointLifecycleCoordinator(store);

        var result = await coordinator.FlushAsync(
            ReliableEventCheckpointFlushTrigger.Disconnect);
        var diagnostics = coordinator.GetDiagnostics();

        Assert.True(result.Succeeded);
        Assert.Equal(1, flushCount);
        Assert.Equal(1, diagnostics.AttemptCount);
        Assert.Equal(1, diagnostics.SuccessCount);
        Assert.Equal(ReliableEventCheckpointFlushTrigger.Disconnect, diagnostics.LastTrigger);
    }

    [Fact]
    public async Task LifecycleCoordinatorSkipsStoreWithoutFlushCapability()
    {
        var coordinator = new ReliableEventCheckpointLifecycleCoordinator(
            new InMemoryReliableEventCheckpointStore());

        var result = await coordinator.FlushAsync(
            ReliableEventCheckpointFlushTrigger.ApplicationPause);

        Assert.Equal(ReliableEventCheckpointFlushStatus.Skipped, result.Status);
        Assert.Equal(1, coordinator.GetDiagnostics().SkippedCount);
    }

    [Fact]
    public async Task LifecycleCoordinatorCapturesAndPublishesFlushFailure()
    {
        var expected = new InvalidOperationException("flush failed");
        var failures = new List<ReliableEventCheckpointLifecycleFailure>();
        var store = new DelegatingReliableEventCheckpointStore(
            _ => null,
            _ => { },
            _ => false,
            _ => Task.FromException(expected));
        var coordinator = new ReliableEventCheckpointLifecycleCoordinator(store);
        coordinator.Failure += failures.Add;

        var result = await coordinator.FlushAsync(
            ReliableEventCheckpointFlushTrigger.ApplicationQuit);

        Assert.Equal(ReliableEventCheckpointFlushStatus.Failed, result.Status);
        Assert.Same(expected, result.Failure);
        Assert.Single(failures);
        Assert.Equal(1, coordinator.GetDiagnostics().FailureCount);
    }

    [Fact]
    public async Task LifecycleCoordinatorCanThrowAfterPublishingFailure()
    {
        var expected = new InvalidOperationException("flush failed");
        var publishedCount = 0;
        var store = new DelegatingReliableEventCheckpointStore(
            _ => null,
            _ => { },
            _ => false,
            _ => Task.FromException(expected));
        var coordinator = new ReliableEventCheckpointLifecycleCoordinator(
            store,
            new ReliableEventCheckpointLifecycleOptions
            {
                FailurePolicy = ReliableEventCheckpointFlushFailurePolicy.ThrowAfterPublish
            });
        coordinator.Failure += _ => publishedCount++;

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.FlushAsync(ReliableEventCheckpointFlushTrigger.Dispose));

        Assert.Same(expected, thrown);
        Assert.Equal(1, publishedCount);
        Assert.Equal(1, coordinator.GetDiagnostics().FailureCount);
    }

    [Fact]
    public async Task LifecycleCoordinatorDetectsStoreReportedBackgroundFailure()
    {
        var store = new ReportingFlushStore();
        var coordinator = new ReliableEventCheckpointLifecycleCoordinator(store);

        var result = await coordinator.FlushAsync(
            ReliableEventCheckpointFlushTrigger.RoomLeave);

        Assert.Equal(ReliableEventCheckpointFlushStatus.Failed, result.Status);
        Assert.Same(store.Failure, result.Failure);
        Assert.Equal(1, coordinator.GetDiagnostics().FailureCount);
    }

    [Fact]
    public async Task LifecycleCoordinatorCanIgnoreStoreReportedBackgroundFailure()
    {
        var store = new ReportingFlushStore();
        var coordinator = new ReliableEventCheckpointLifecycleCoordinator(
            store,
            new ReliableEventCheckpointLifecycleOptions
            {
                TreatReportedStoreFailureAsFlushFailure = false
            });

        var result = await coordinator.FlushAsync(
            ReliableEventCheckpointFlushTrigger.Manual);

        Assert.Equal(ReliableEventCheckpointFlushStatus.Succeeded, result.Status);
        Assert.Null(result.Failure);
        Assert.Equal(1, coordinator.GetDiagnostics().SuccessCount);
    }

    [Fact]
    public async Task LifecycleCoordinatorCanAllowConcurrentFlushes()
    {
        var enteredCount = 0;
        var bothEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new DelegatingReliableEventCheckpointStore(
            _ => null,
            _ => { },
            _ => false,
            async _ =>
            {
                if (Interlocked.Increment(ref enteredCount) == 2)
                {
                    bothEntered.TrySetResult(true);
                }

                await release.Task;
            });
        var coordinator = new ReliableEventCheckpointLifecycleCoordinator(
            store,
            new ReliableEventCheckpointLifecycleOptions
            {
                SerializeConcurrentFlushes = false
            });

        var first = coordinator.FlushAsync(ReliableEventCheckpointFlushTrigger.Disconnect);
        var second = coordinator.FlushAsync(ReliableEventCheckpointFlushTrigger.ApplicationPause);
        await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2, coordinator.GetDiagnostics().ActiveFlushCount);

        release.TrySetResult(true);
        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(2, coordinator.GetDiagnostics().SuccessCount);
    }

    [Fact]
    public async Task LifecycleCoordinatorRetriesAndPublishesStructuredDiagnostics()
    {
        var flushCount = 0;
        var retries = new List<ReliableEventCheckpointFlushRetry>();
        var sinkRetries = new List<ReliableEventCheckpointFlushRetry>();
        var sinkResults = new List<ReliableEventCheckpointFlushResult>();
        var sink = new DelegatingReliableEventCheckpointLifecycleDiagnosticsSink(
            sinkRetries.Add,
            sinkResults.Add);
        var store = new DelegatingReliableEventCheckpointStore(
            _ => null,
            _ => { },
            _ => false,
            _ => Interlocked.Increment(ref flushCount) < 3
                ? Task.FromException(new InvalidOperationException("transient failure"))
                : Task.CompletedTask);
        var coordinator = new ReliableEventCheckpointLifecycleCoordinator(
            store,
            new ReliableEventCheckpointLifecycleOptions
            {
                RetryPolicy = new ReliableEventCheckpointExponentialBackoffRetryPolicy(
                    maxRetryCount: 2,
                    initialDelay: TimeSpan.Zero,
                    maximumDelay: TimeSpan.Zero),
                DiagnosticsSink = sink
            });
        coordinator.RetryScheduled += retries.Add;

        var result = await coordinator.FlushAsync(
            ReliableEventCheckpointFlushTrigger.ApplicationQuit);
        var diagnostics = coordinator.GetDiagnostics();

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.StoreAttemptCount);
        Assert.Equal(2, result.RetryCount);
        Assert.Equal(3, flushCount);
        Assert.Equal(2, retries.Count);
        Assert.Equal(2, sinkRetries.Count);
        Assert.Single(sinkResults);
        Assert.Equal(2, diagnostics.RetryCount);
        Assert.Equal(3, diagnostics.LastStoreAttemptCount);
        Assert.Equal(ReliableEventCheckpointFlushTrigger.ApplicationQuit, sinkResults[0].Trigger);
    }

    [Fact]
    public async Task LifecycleCoordinatorRetriesAfterAttemptTimeout()
    {
        var flushCount = 0;
        var store = new DelegatingReliableEventCheckpointStore(
            _ => null,
            _ => { },
            _ => false,
            cancellationToken => Interlocked.Increment(ref flushCount) == 1
                ? Task.Delay(Timeout.Infinite, cancellationToken)
                : Task.CompletedTask);
        var coordinator = new ReliableEventCheckpointLifecycleCoordinator(
            store,
            new ReliableEventCheckpointLifecycleOptions
            {
                FlushAttemptTimeout = TimeSpan.FromMilliseconds(20),
                RetryPolicy = new ReliableEventCheckpointExponentialBackoffRetryPolicy(
                    maxRetryCount: 1,
                    initialDelay: TimeSpan.Zero,
                    maximumDelay: TimeSpan.Zero)
            });

        var result = await coordinator.FlushAsync(
            ReliableEventCheckpointFlushTrigger.Disconnect);
        var diagnostics = coordinator.GetDiagnostics();

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.StoreAttemptCount);
        Assert.Equal(1, result.RetryCount);
        Assert.Equal(1, diagnostics.TimeoutCount);
        Assert.Equal(1, diagnostics.RetryCount);
        Assert.Equal(2, diagnostics.LastStoreAttemptCount);
    }

    [Fact]
    public async Task LifecycleCoordinatorResolvesPolicyByFlushTrigger()
    {
        var flushCount = 0;
        var store = new DelegatingReliableEventCheckpointStore(
            _ => null,
            _ => { },
            _ => false,
            _ => Interlocked.Increment(ref flushCount) == 1
                ? Task.FromException(new InvalidOperationException("transient failure"))
                : Task.CompletedTask);
        var disconnectPolicy = new ReliableEventCheckpointFlushPolicy(
            ReliableEventCheckpointFlushFailurePolicy.CaptureAndContinue,
            retryPolicy: new ReliableEventCheckpointExponentialBackoffRetryPolicy(
                maxRetryCount: 1,
                initialDelay: TimeSpan.Zero,
                maximumDelay: TimeSpan.Zero));
        var resolver = new ReliableEventCheckpointTriggerPolicyResolver();
        resolver.Set(ReliableEventCheckpointFlushTrigger.Disconnect, in disconnectPolicy);
        var coordinator = new ReliableEventCheckpointLifecycleCoordinator(
            store,
            new ReliableEventCheckpointLifecycleOptions
            {
                TriggerPolicyResolver = resolver
            });

        var result = await coordinator.FlushAsync(
            ReliableEventCheckpointFlushTrigger.Disconnect);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.StoreAttemptCount);
        Assert.Equal(2, flushCount);
    }

    [Fact]
    public async Task LifecycleCoordinatorOpensCircuitAfterConsecutiveFailures()
    {
        var flushCount = 0;
        var store = new DelegatingReliableEventCheckpointStore(
            _ => null,
            _ => { },
            _ => false,
            _ =>
            {
                flushCount++;
                return Task.FromException(new InvalidOperationException("persistent failure"));
            });
        var coordinator = new ReliableEventCheckpointLifecycleCoordinator(
            store,
            new ReliableEventCheckpointLifecycleOptions
            {
                CircuitBreaker = new ReliableEventCheckpointCircuitBreakerOptions
                {
                    FailureThreshold = 2,
                    BreakDuration = TimeSpan.FromMinutes(1)
                }
            });

        var first = await coordinator.FlushAsync(ReliableEventCheckpointFlushTrigger.Manual);
        var second = await coordinator.FlushAsync(ReliableEventCheckpointFlushTrigger.Manual);
        var rejected = await coordinator.FlushAsync(ReliableEventCheckpointFlushTrigger.Manual);
        var diagnostics = coordinator.GetDiagnostics();

        Assert.Equal(ReliableEventCheckpointFlushStatus.Failed, first.Status);
        Assert.Equal(ReliableEventCheckpointFlushStatus.Failed, second.Status);
        Assert.Equal(ReliableEventCheckpointFlushStatus.CircuitOpen, rejected.Status);
        Assert.IsType<ReliableEventCheckpointCircuitOpenException>(rejected.Failure);
        Assert.Equal(2, flushCount);
        Assert.Equal(2, diagnostics.FailureCount);
        Assert.Equal(1, diagnostics.CircuitOpenCount);
        Assert.Equal(2, diagnostics.ConsecutiveFailureCount);
        Assert.Equal(ReliableEventCheckpointCircuitState.Open, diagnostics.CircuitState);
    }

    [Fact]
    public async Task LifecycleCoordinatorClosesCircuitAfterSuccessfulHalfOpenProbe()
    {
        var flushCount = 0;
        var store = new DelegatingReliableEventCheckpointStore(
            _ => null,
            _ => { },
            _ => false,
            _ => Interlocked.Increment(ref flushCount) == 1
                ? Task.FromException(new InvalidOperationException("first failure"))
                : Task.CompletedTask);
        var coordinator = new ReliableEventCheckpointLifecycleCoordinator(
            store,
            new ReliableEventCheckpointLifecycleOptions
            {
                CircuitBreaker = new ReliableEventCheckpointCircuitBreakerOptions
                {
                    FailureThreshold = 1,
                    BreakDuration = TimeSpan.Zero
                }
            });

        var failed = await coordinator.FlushAsync(ReliableEventCheckpointFlushTrigger.Manual);
        var recovered = await coordinator.FlushAsync(ReliableEventCheckpointFlushTrigger.Manual);
        var diagnostics = coordinator.GetDiagnostics();

        Assert.Equal(ReliableEventCheckpointFlushStatus.Failed, failed.Status);
        Assert.True(recovered.Succeeded);
        Assert.Equal(2, flushCount);
        Assert.Equal(0, diagnostics.ConsecutiveFailureCount);
        Assert.Equal(ReliableEventCheckpointCircuitState.Closed, diagnostics.CircuitState);
    }

    [Fact]
    public void LifecyclePresetsExposeExpectedPolicyModes()
    {
        var resilient = ReliableEventCheckpointLifecyclePresets.CreateResilientClient();
        var strict = ReliableEventCheckpointLifecyclePresets.CreateStrictValidation();

        Assert.NotNull(resilient.RetryPolicy);
        Assert.NotNull(resilient.CircuitBreaker);
        Assert.NotNull(resilient.TriggerPolicyResolver);
        Assert.True(resilient.TriggerPolicyResolver.TryResolve(
            ReliableEventCheckpointFlushTrigger.ApplicationQuit,
            out var quitPolicy));
        Assert.Equal(TimeSpan.FromSeconds(1), quitPolicy.FlushAttemptTimeout);
        Assert.Equal(
            ReliableEventCheckpointFlushFailurePolicy.ThrowAfterPublish,
            strict.FailurePolicy);
        Assert.Null(strict.CircuitBreaker);
    }

    private sealed class ThrowingCheckpointStore : IReliableEventCheckpointStore
    {
        public bool TryLoad(string streamId, out ReliableEventCheckpoint checkpoint)
        {
            checkpoint = default;
            return false;
        }

        public void Save(in ReliableEventCheckpoint checkpoint)
        {
            throw new InvalidOperationException("write failed");
        }

        public bool Remove(string streamId) => false;
    }

    private sealed class ReportingFlushStore :
        IReliableEventCheckpointStore,
        IReliableEventCheckpointStoreFlushable,
        IReliableEventCheckpointStoreDiagnosticsProvider
    {
        private int _failureCount;

        public Exception Failure { get; } = new InvalidOperationException("background failed");

        public bool TryLoad(string streamId, out ReliableEventCheckpoint checkpoint)
        {
            checkpoint = default;
            return false;
        }

        public void Save(in ReliableEventCheckpoint checkpoint)
        {
        }

        public bool Remove(string streamId) => false;

        public Task FlushAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _failureCount++;
            return Task.CompletedTask;
        }

        public ReliableEventCheckpointStoreDiagnostics GetCheckpointStoreDiagnostics()
        {
            return new ReliableEventCheckpointStoreDiagnostics(
                _failureCount,
                _failureCount > 0 ? Failure : null);
        }
    }

}
