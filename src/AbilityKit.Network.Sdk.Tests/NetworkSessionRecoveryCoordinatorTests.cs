using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Sdk.Tests;

public sealed class NetworkSessionRecoveryCoordinatorTests
{
    [Fact]
    public void CoordinatorRejectsEmptySignalAndStartsWithoutDecision()
    {
        var coordinator = new NetworkSessionRecoveryCoordinator();
        var empty = default(NetworkSessionRecoverySignal);

        Assert.False(coordinator.CurrentDecision.HasDecision);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            coordinator.TryReport(in empty, out _));
    }

    [Theory]
    [InlineData(NetworkSessionRecoverySignalKind.ConnectionLost, NetworkSessionRecoveryAction.WaitForReconnect, 10, false)]
    [InlineData(NetworkSessionRecoverySignalKind.ReconnectExhausted, NetworkSessionRecoveryAction.RebuildSession, 60, true)]
    [InlineData(NetworkSessionRecoverySignalKind.SnapshotResyncRequired, NetworkSessionRecoveryAction.RequestFullSnapshot, 30, false)]
    [InlineData(NetworkSessionRecoverySignalKind.ReliableEventResyncRequired, NetworkSessionRecoveryAction.RestoreReliableEventBaseline, 40, false)]
    [InlineData(NetworkSessionRecoverySignalKind.CheckpointFlushFailed, NetworkSessionRecoveryAction.None, 5, false)]
    [InlineData(NetworkSessionRecoverySignalKind.CheckpointCircuitOpen, NetworkSessionRecoveryAction.RebuildSession, 60, true)]
    public void DefaultPolicyMapsFrameworkSignals(
        NetworkSessionRecoverySignalKind kind,
        NetworkSessionRecoveryAction expectedAction,
        int expectedPriority,
        bool expectedTermination)
    {
        var coordinator = new NetworkSessionRecoveryCoordinator();
        var signal = new NetworkSessionRecoverySignal(kind);

        var accepted = coordinator.TryReport(in signal, out var decision);

        Assert.True(accepted);
        Assert.Equal(expectedAction, decision.Action);
        Assert.Equal(expectedPriority, decision.Priority);
        Assert.Equal(expectedTermination, decision.TerminatesCurrentSession);
        Assert.Equal(kind, decision.Signal.Kind);
    }

    [Fact]
    public void CoordinatorDeduplicatesSameSignalInsideConfiguredWindow()
    {
        var now = new DateTimeOffset(2026, 8, 14, 0, 0, 0, TimeSpan.Zero);
        var suppressedReason = default(NetworkSessionRecoverySuppressionReason?);
        var coordinator = new NetworkSessionRecoveryCoordinator(
            new NetworkSessionRecoveryOptions
            {
                DuplicateSignalWindow = TimeSpan.FromSeconds(2),
                UtcNowProvider = () => now,
                DiagnosticsSink = new DelegatingNetworkSessionRecoveryDiagnosticsSink(
                    suppressed: (_, reason) => suppressedReason = reason)
            });
        var signal = new NetworkSessionRecoverySignal(
            NetworkSessionRecoverySignalKind.SnapshotResyncRequired,
            frame: 42,
            correlationContext: "battle-1");

        Assert.True(coordinator.TryReport(in signal, out _));
        now = now.AddSeconds(1);
        Assert.False(coordinator.TryReport(in signal, out var duplicateDecision));

        Assert.Equal(NetworkSessionRecoveryAction.RequestFullSnapshot, duplicateDecision.Action);
        Assert.Equal(NetworkSessionRecoverySuppressionReason.Duplicate, suppressedReason);
        Assert.Equal(1, coordinator.GetDiagnostics().DuplicateSignalCount);
    }

    [Fact]
    public void CoordinatorEscalatesButDoesNotDowngradeActiveAction()
    {
        var coordinator = new NetworkSessionRecoveryCoordinator(
            new NetworkSessionRecoveryOptions { DuplicateSignalWindow = TimeSpan.Zero });
        var snapshot = new NetworkSessionRecoverySignal(
            NetworkSessionRecoverySignalKind.SnapshotResyncRequired);
        var exhausted = new NetworkSessionRecoverySignal(
            NetworkSessionRecoverySignalKind.ReconnectExhausted);
        var connectionLost = new NetworkSessionRecoverySignal(
            NetworkSessionRecoverySignalKind.ConnectionLost);

        Assert.True(coordinator.TryReport(in snapshot, out _));
        Assert.True(coordinator.TryReport(in exhausted, out var escalated));
        Assert.False(coordinator.TryReport(in connectionLost, out var retained));

        Assert.Equal(NetworkSessionRecoveryAction.RebuildSession, escalated.Action);
        Assert.Equal(NetworkSessionRecoveryAction.RebuildSession, retained.Action);
        Assert.Equal(1, coordinator.GetDiagnostics().PrioritySuppressedCount);
    }

    [Fact]
    public void RecoveredSignalResetsActiveActionAndDeduplicationState()
    {
        var coordinator = new NetworkSessionRecoveryCoordinator();
        var required = new NetworkSessionRecoverySignal(
            NetworkSessionRecoverySignalKind.ReliableEventResyncRequired,
            frame: 9);
        var recovered = new NetworkSessionRecoverySignal(
            NetworkSessionRecoverySignalKind.Recovered,
            SyncHealthSeverity.Info,
            frame: 10);

        Assert.True(coordinator.TryReport(in required, out _));
        Assert.True(coordinator.TryReport(in recovered, out var recoveredDecision));
        Assert.True(coordinator.TryReport(in required, out var repeatedAfterRecovery));

        Assert.Equal(NetworkSessionRecoveryAction.None, recoveredDecision.Action);
        Assert.Equal(NetworkSessionRecoveryAction.RestoreReliableEventBaseline, repeatedAfterRecovery.Action);
        Assert.Equal(1, coordinator.GetDiagnostics().ResetCount);
    }

    [Fact]
    public void OptionsSnapshotCustomRulesAndPublishStructuredDiagnostics()
    {
        var published = new List<NetworkSessionRecoveryDecision>();
        var rulePolicy = new NetworkSessionRecoveryRulePolicy().SetRule(
            NetworkSessionRecoverySignalKind.CheckpointCircuitOpen,
            new NetworkSessionRecoveryDirective(
                NetworkSessionRecoveryAction.ReturnToLobby,
                priority: 80,
                terminatesCurrentSession: true,
                reason: "项目不允许无持久化基线继续战斗。"));
        var options = new NetworkSessionRecoveryOptions
        {
            Policy = rulePolicy,
            DiagnosticsSink = new DelegatingNetworkSessionRecoveryDiagnosticsSink(
                decision => published.Add(decision))
        };
        var coordinator = new NetworkSessionRecoveryCoordinator(options);

        rulePolicy.SetRule(
            NetworkSessionRecoverySignalKind.CheckpointCircuitOpen,
            new NetworkSessionRecoveryDirective(NetworkSessionRecoveryAction.AbortSession, 100, true));
        var signal = new NetworkSessionRecoverySignal(
            NetworkSessionRecoverySignalKind.CheckpointCircuitOpen,
            SyncHealthSeverity.Error);
        Assert.True(coordinator.TryReport(in signal, out var decision));

        Assert.Equal(NetworkSessionRecoveryAction.ReturnToLobby, decision.Action);
        Assert.Single(published);
        Assert.Equal(1, coordinator.GetDiagnostics().PublishedDecisionCount);
    }

    [Fact]
    public void EscalationCanBeDisabledByOptions()
    {
        var coordinator = new NetworkSessionRecoveryCoordinator(
            new NetworkSessionRecoveryOptions
            {
                AllowActionEscalation = false,
                DuplicateSignalWindow = TimeSpan.Zero
            });
        var waiting = new NetworkSessionRecoverySignal(NetworkSessionRecoverySignalKind.ConnectionLost);
        var exhausted = new NetworkSessionRecoverySignal(NetworkSessionRecoverySignalKind.ReconnectExhausted);

        Assert.True(coordinator.TryReport(in waiting, out _));
        Assert.False(coordinator.TryReport(in exhausted, out var retained));

        Assert.Equal(NetworkSessionRecoveryAction.WaitForReconnect, retained.Action);
    }

    [Fact]
    public async Task ActionRouterExecutesRegisteredHandlerWithProjectState()
    {
        var coordinator = new NetworkSessionRecoveryCoordinator();
        var signal = new NetworkSessionRecoverySignal(
            NetworkSessionRecoverySignalKind.SnapshotResyncRequired);
        Assert.True(coordinator.TryReport(in signal, out var decision));
        var router = new NetworkSessionRecoveryActionRouter<string>()
            .Register(
                NetworkSessionRecoveryAction.RequestFullSnapshot,
                (context, _) => Task.FromResult(
                    $"{context.Decision.Signal.Kind}:{context.State}"));

        var result = await router.ExecuteAsync(decision, state: "battle-1");

        Assert.True(result.Succeeded);
        Assert.True(result.HasValue);
        Assert.Equal("SnapshotResyncRequired:battle-1", result.Value);
    }

    [Fact]
    public async Task ActionRouterDistinguishesNoActionUnhandledAndFailure()
    {
        var router = new NetworkSessionRecoveryActionRouter<int>()
            .Register(
                NetworkSessionRecoveryAction.RequestFullSnapshot,
                (_, _) => Task.FromException<int>(new InvalidOperationException("request failed")));
        var noAction = await router.ExecuteAsync(default);
        var coordinator = new NetworkSessionRecoveryCoordinator();
        var reconnect = new NetworkSessionRecoverySignal(
            NetworkSessionRecoverySignalKind.ReconnectExhausted);
        Assert.True(coordinator.TryReport(in reconnect, out var unhandledDecision));
        var unhandled = await router.ExecuteAsync(unhandledDecision);
        coordinator.Reset();
        var snapshot = new NetworkSessionRecoverySignal(
            NetworkSessionRecoverySignalKind.SnapshotResyncRequired);
        Assert.True(coordinator.TryReport(in snapshot, out var failingDecision));
        var failed = await router.ExecuteAsync(failingDecision);

        Assert.Equal(NetworkSessionRecoveryExecutionStatus.NoAction, noAction.Status);
        Assert.Equal(NetworkSessionRecoveryExecutionStatus.Unhandled, unhandled.Status);
        Assert.Equal(NetworkSessionRecoveryExecutionStatus.Failed, failed.Status);
        Assert.IsType<InvalidOperationException>(failed.Exception);
    }

    [Fact]
    public async Task ActionRouterCanReturnCancellationOrPreserveThrowBehavior()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var coordinator = new NetworkSessionRecoveryCoordinator();
        var signal = new NetworkSessionRecoverySignal(
            NetworkSessionRecoverySignalKind.SnapshotResyncRequired);
        Assert.True(coordinator.TryReport(in signal, out var decision));
        var returning = new NetworkSessionRecoveryActionRouter<int>(
                new NetworkSessionRecoveryActionRouterOptions<int>
                {
                    CancellationPolicy = NetworkSessionRecoveryCancellationPolicy.ReturnCancelled
                })
            .Register(
                NetworkSessionRecoveryAction.RequestFullSnapshot,
                (_, token) => Task.FromCanceled<int>(token));
        var throwing = new NetworkSessionRecoveryActionRouter<int>(
                new NetworkSessionRecoveryActionRouterOptions<int>
                {
                    HandlerFailurePolicy = NetworkSessionRecoveryHandlerFailurePolicy.Throw
                })
            .Register(
                NetworkSessionRecoveryAction.RequestFullSnapshot,
                (_, _) => Task.FromException<int>(new InvalidOperationException("strict failure")));

        var cancelled = await returning.ExecuteAsync(
            decision,
            cancellationToken: cancellation.Token);

        Assert.Equal(NetworkSessionRecoveryExecutionStatus.Cancelled, cancelled.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => throwing.ExecuteAsync(decision));
    }

    [Fact]
    public async Task RecoveryRuntimeAutomaticallyExecutesAcceptedDecisionWithProvidedState()
    {
        var router = new NetworkSessionRecoveryActionRouter<string>()
            .Register(
                NetworkSessionRecoveryAction.WaitForReconnect,
                (context, _) => Task.FromResult(
                    $"{context.Decision.Signal.Kind}:{context.State}"));
        using var runtime = new NetworkSessionRecoveryRuntime<string>(
            router,
            runtimeOptions: new NetworkSessionRecoveryRuntimeOptions
            {
                ExecutionStateProvider = _ => "connection-1"
            });
        var signal = new NetworkSessionRecoverySignal(
            NetworkSessionRecoverySignalKind.ReconnectScheduled);

        Assert.True(runtime.TryReport(in signal, out var decision));
        var execution = await runtime.PendingExecution;

        Assert.Equal(NetworkSessionRecoveryAction.WaitForReconnect, decision.Action);
        Assert.Equal("ReconnectScheduled:connection-1", execution.Value);
        Assert.True(runtime.HasLastExecution);
        Assert.Equal(1, runtime.GetRuntimeDiagnostics().AcceptedDecisionCount);
        Assert.Equal(1, runtime.GetRuntimeDiagnostics().CompletedExecutionCount);
    }

    [Fact]
    public async Task RecoveryRuntimeManualModeUsesOptionSnapshotAndExplicitState()
    {
        var calls = 0;
        var router = new NetworkSessionRecoveryActionRouter<string>()
            .Register(
                NetworkSessionRecoveryAction.RequestFullSnapshot,
                (context, _) =>
                {
                    calls++;
                    return Task.FromResult((string)context.State!);
                });
        var options = new NetworkSessionRecoveryRuntimeOptions
        {
            ExecutionMode = NetworkSessionRecoveryExecutionMode.Manual
        };
        using var runtime = new NetworkSessionRecoveryRuntime<string>(
            router,
            runtimeOptions: options);
        options.ExecutionMode = NetworkSessionRecoveryExecutionMode.Automatic;
        var signal = new NetworkSessionRecoverySignal(
            NetworkSessionRecoverySignalKind.SnapshotResyncRequired);

        Assert.True(runtime.TryReport(in signal, out _));
        Assert.Equal(0, calls);
        Assert.Equal(0, runtime.GetRuntimeDiagnostics().StartedExecutionCount);

        var execution = await runtime.ExecuteCurrentAsync("battle-1");

        Assert.Equal("battle-1", execution.Value);
        Assert.Equal(1, calls);
        Assert.Equal(1, runtime.GetRuntimeDiagnostics().StartedExecutionCount);
    }

    [Fact]
    public async Task RecoveryRuntimeResetCancelsAndSuppressesStaleExecution()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var publishedCompletions = 0;
        var router = new NetworkSessionRecoveryActionRouter<int>(
                new NetworkSessionRecoveryActionRouterOptions<int>
                {
                    CancellationPolicy = NetworkSessionRecoveryCancellationPolicy.ReturnCancelled
                })
            .Register(
                NetworkSessionRecoveryAction.RequestFullSnapshot,
                async (_, cancellationToken) =>
                {
                    entered.TrySetResult(true);
                    await release.Task;
                    cancellationToken.ThrowIfCancellationRequested();
                    return 7;
                });
        using var runtime = new NetworkSessionRecoveryRuntime<int>(router);
        runtime.ExecutionCompleted += _ => publishedCompletions++;
        var signal = new NetworkSessionRecoverySignal(
            NetworkSessionRecoverySignalKind.SnapshotResyncRequired);

        Assert.True(runtime.TryReport(in signal, out _));
        var staleExecution = runtime.PendingExecution;
        await entered.Task;
        runtime.Reset();
        release.TrySetResult(true);
        var result = await staleExecution;

        Assert.Equal(NetworkSessionRecoveryExecutionStatus.Cancelled, result.Status);
        Assert.False(runtime.CurrentDecision.HasDecision);
        Assert.False(runtime.HasLastExecution);
        Assert.Equal(0, publishedCompletions);
        Assert.Equal(1, runtime.GetRuntimeDiagnostics().StaleExecutionCount);
        Assert.Equal(1, runtime.GetRuntimeDiagnostics().ResetCount);
    }

    [Fact]
    public async Task RecoveryRuntimeEscalationCancelsOlderActionAndKeepsLatestResult()
    {
        var firstEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var router = new NetworkSessionRecoveryActionRouter<int>(
                new NetworkSessionRecoveryActionRouterOptions<int>
                {
                    CancellationPolicy = NetworkSessionRecoveryCancellationPolicy.ReturnCancelled
                })
            .Register(
                NetworkSessionRecoveryAction.RequestFullSnapshot,
                async (_, cancellationToken) =>
                {
                    firstEntered.TrySetResult(true);
                    await releaseFirst.Task;
                    cancellationToken.ThrowIfCancellationRequested();
                    return 1;
                })
            .Register(
                NetworkSessionRecoveryAction.RebuildSession,
                (_, _) => Task.FromResult(2));
        using var runtime = new NetworkSessionRecoveryRuntime<int>(
            router,
            new NetworkSessionRecoveryOptions { DuplicateSignalWindow = TimeSpan.Zero });
        var snapshot = new NetworkSessionRecoverySignal(
            NetworkSessionRecoverySignalKind.SnapshotResyncRequired);
        var exhausted = new NetworkSessionRecoverySignal(
            NetworkSessionRecoverySignalKind.ReconnectExhausted);

        Assert.True(runtime.TryReport(in snapshot, out _));
        var staleExecution = runtime.PendingExecution;
        await firstEntered.Task;
        Assert.True(runtime.TryReport(in exhausted, out _));
        var latest = await runtime.PendingExecution;
        releaseFirst.TrySetResult(true);
        var stale = await staleExecution;

        Assert.Equal(2, latest.Value);
        Assert.Equal(NetworkSessionRecoveryExecutionStatus.Cancelled, stale.Status);
        Assert.Equal(2, runtime.LastExecution.Value);
        Assert.Equal(NetworkSessionRecoverySignalKind.ReconnectExhausted,
            runtime.CurrentDecision.Signal.Kind);
        Assert.Equal(1, runtime.GetRuntimeDiagnostics().StaleExecutionCount);
    }

    [Fact]
    public async Task RecoveryRuntimeCompletionAdvancesGenerationWithoutExecutingNoActionDecision()
    {
        var calls = 0;
        var router = new NetworkSessionRecoveryActionRouter<bool>()
            .Register(
                NetworkSessionRecoveryAction.WaitForReconnect,
                (_, _) =>
                {
                    calls++;
                    return Task.FromResult(true);
                });
        using var runtime = new NetworkSessionRecoveryRuntime<bool>(router);
        var lost = new NetworkSessionRecoverySignal(
            NetworkSessionRecoverySignalKind.ConnectionLost,
            correlationContext: "room-1");

        Assert.True(runtime.TryReport(in lost, out _));
        await runtime.PendingExecution;
        Assert.True(runtime.CompleteRecovery("room-1", "restored"));

        Assert.Equal(1, calls);
        Assert.Equal(NetworkSessionRecoverySignalKind.Recovered,
            runtime.CurrentDecision.Signal.Kind);
        Assert.Equal(2, runtime.GetRuntimeDiagnostics().AcceptedDecisionCount);
        Assert.Equal(1, runtime.GetRuntimeDiagnostics().StartedExecutionCount);
    }
}
