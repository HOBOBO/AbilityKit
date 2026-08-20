using System;
using System.IO;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Client;

public sealed class ShooterRemoteCoordinatorInputContractTests
{
    [Fact]
    public void RemotePlayModeInputSubmissionUsesFrameworkQueueWithoutCoordinatorSession()
    {
        var playModeHost = ReadUnityPackageSource(
            "com.abilitykit.demo.shooter.view.runtime",
            "Runtime", "Unity", "PlayMode", "ShooterRemoteStateSyncPlayModeHost.cs");
        var inputSubmitStrategy = ReadUnityPackageSource(
            "com.abilitykit.demo.shooter.view.runtime",
            "Runtime", "Unity", "PlayMode", "ShooterRemoteInputSubmitStrategy.cs");

        Assert.Contains("_inputSubmitStrategy = ShooterRemoteInputSubmitStrategy.Create(", playModeHost);
        Assert.Contains("state.Launch.Battle,", playModeHost);
        Assert.Contains("inputSubmitStrategy?.SubmitOrQueue(in submitResult);", ReadUnityPackageSource(
            "com.abilitykit.demo.shooter.view.runtime",
            "Runtime", "Unity", "PlayMode", "ShooterRemoteInputPump.cs"));
        Assert.Contains("battle.SubmitAcceptedInputToGatewayAsync(local, requestTimeout)", inputSubmitStrategy);
        Assert.Contains("RemoteClientInputSubmitQueue<ShooterClientInputSubmitResult, ShooterClientGatewayInputSubmitResult>", inputSubmitStrategy);
        Assert.DoesNotContain("ShooterCoordinatorInputBridge", playModeHost);
        Assert.DoesNotContain("SessionCoordinator", inputSubmitStrategy);
    }

    [Fact]
    public void RemotePlayModeUsesGatewayAssignedPlayerIdForInputAndPresentation()
    {
        var playModeHost = ReadUnityPackageSource(
            "com.abilitykit.demo.shooter.view.runtime",
            "Runtime", "Unity", "PlayMode", "ShooterRemoteStateSyncPlayModeHost.cs");
        var frameBuilder = ReadUnityPackageSource(
            "com.abilitykit.demo.shooter.view.runtime",
            "Runtime", "Unity", "PlayMode", "ShooterRemotePresentationFrameBuilder.cs");

        Assert.Contains("_effectiveControlledPlayerId = ResolveEffectiveControlledPlayerId(", playModeHost);
        Assert.Contains("connectionResult.Launch.Session.Presentation.ControlledPlayerId = _effectiveControlledPlayerId;", playModeHost);
        Assert.Contains("ResolveEffectiveControlledPlayerId(state.Launch.Flow, _options.SessionOptions.ControlledPlayerId)", playModeHost);
        Assert.Contains("flow.PlayerId > 0u && flow.PlayerId <= int.MaxValue", playModeHost);
        Assert.Contains("int controlledPlayerId", frameBuilder);
        Assert.Contains("controlledPlayerId,", frameBuilder);
        Assert.DoesNotContain("connectionResult.Launch.Session.Presentation.ControlledPlayerId = launchOptions.SessionOptions.ControlledPlayerId;", playModeHost);

        var bindPlayerIndex = playModeHost.IndexOf(
            "connectionResult.Launch.Session.Presentation.ControlledPlayerId = _effectiveControlledPlayerId;",
            StringComparison.Ordinal);
        var fullSyncIndex = playModeHost.IndexOf(
            "new ShooterInitialFullStateSyncCoordinator(",
            bindPlayerIndex,
            StringComparison.Ordinal);
        Assert.True(bindPlayerIndex >= 0 && fullSyncIndex > bindPlayerIndex,
            "The gateway-assigned player id must be bound before applying the AOI full baseline.");
    }

    [Fact]
    public void RemotePlayModePauseStopsPumpAndResumeUsesRestoreOnlyReconnect()
    {
        var playModeHost = ReadUnityPackageSource(
            "com.abilitykit.demo.shooter.view.runtime",
            "Runtime", "Unity", "PlayMode", "ShooterRemoteStateSyncPlayModeHost.cs");
        var reconnectLaunchOptionsBuilder = ReadUnityPackageSource(
            "com.abilitykit.demo.shooter.view.runtime",
            "Runtime", "Unity", "PlayMode", "ShooterReconnectLaunchOptionsBuilder.cs");

        var initialFullStateSync = ReadUnityPackageSource(
            "com.abilitykit.demo.shooter.view.runtime",
            "Runtime", "Unity", "PlayMode", "ShooterInitialFullStateSyncCoordinator.cs");
        var connectionFlow = ReadUnityPackageSource(
            "com.abilitykit.demo.shooter.view.runtime",
            "Runtime", "PlayMode", "ShooterRemoteStateSyncConnectionFlow.cs");

        Assert.Contains("public static bool IsPaused => _isPaused;", playModeHost);
        Assert.Contains("public static bool IsAutoReconnecting => _isAutoReconnecting;", playModeHost);
        Assert.Contains("public static void Pause()", playModeHost);
        Assert.Contains("public static void PauseForReconnectValidation()", playModeHost);
        Assert.Contains("Pause();", playModeHost);
        Assert.Contains("state.Launcher.Close();", playModeHost);
        Assert.Contains("_inputSubmitStrategy?.Reset();", playModeHost);
        Assert.Contains("if (state == null || _isPaused)", playModeHost);
        Assert.Contains("if (_isAutoReconnecting)", playModeHost);
        Assert.Contains("TickAutoReconnect(deltaSeconds);", playModeHost);
        Assert.Contains("public static Task<ShooterClientNetworkLaunchResult> ResumeFromPauseAsync()", playModeHost);
        Assert.Contains("return ResumePausedSessionAsync(resumeOptions);", playModeHost);
        Assert.Contains("restoredState = await StartSessionAsync(resumeOptions, generation);", playModeHost);
        Assert.Contains("RenderRestoredStateWithoutAdvancingClient(restoredState, resumeOptions);", playModeHost);
        Assert.Contains("ViewSink.Clear();", playModeHost);
        Assert.Contains("ViewSink.Render(in frame);", playModeHost);
        Assert.Contains("_isPaused = true;", playModeHost);
        Assert.Contains("TryBeginAutoReconnectAfterSocketLoss(state)", playModeHost);
        Assert.Contains("connection.State == ConnectionState.Connected", playModeHost);
        Assert.Contains("connection.State == ConnectionState.Connecting", playModeHost);
        Assert.Contains("SessionReconnectScheduler.TryTakeAttempt", playModeHost);
        Assert.Contains("SessionReconnectScheduler.IsExhausted", playModeHost);
        Assert.Contains("_ = ResumeAfterSocketLossAsync(_pausedResumeOptions, _lifecycleGeneration);", playModeHost);
        Assert.Contains("if (!IsCurrentLifecycle(sourceGeneration))", playModeHost);
        Assert.Contains("ThrowIfStaleLifecycle(generation);", playModeHost);
        Assert.Contains("ShooterReconnectLaunchOptionsBuilder.RestoreOnly(_options", playModeHost);
        Assert.Contains("ShooterRemoteStateSyncLaunchMode.RestoreOnly", reconnectLaunchOptionsBuilder);
        Assert.Contains("source.ReliableEventCheckpointStore", reconnectLaunchOptionsBuilder);
        Assert.Contains("source.ReliableEventCheckpointLifecycleOptions", reconnectLaunchOptionsBuilder);
        Assert.Contains("new ShooterInitialFullStateSyncCoordinator(", playModeHost);
        Assert.Contains("NotifyStateChangedIfCurrent(generation)).RequestIfNeededAsync(", playModeHost);
        Assert.Contains("RequiresInitialFullStateSync => EntryKind == ShooterRoomGatewayEntryKind.LateJoin", connectionFlow);
        Assert.Contains("SnapshotPushDispatched += OnSnapshotPushDispatched", initialFullStateSync);
        Assert.Contains("while (!snapshotApplied)", initialFullStateSync);
        Assert.Contains("IsFullSnapshotPush(opCode) && IsApplied(result, session)", initialFullStateSync);
        Assert.Contains("IsApplied(result, session)", initialFullStateSync);
        Assert.Contains("LastInitialFullStateSyncApplyResult", playModeHost);

        var resumeBodyStart = playModeHost.IndexOf(
            "private static async Task<ShooterClientNetworkLaunchResult> ResumePausedSessionAsync(",
            StringComparison.Ordinal);
        var resumeBodyEnd = playModeHost.IndexOf(
            "public static void Stop()",
            resumeBodyStart,
            StringComparison.Ordinal);
        Assert.True(resumeBodyStart >= 0 && resumeBodyEnd > resumeBodyStart);
        var resumeBody = playModeHost.Substring(resumeBodyStart, resumeBodyEnd - resumeBodyStart);
        Assert.Contains("_lastError = ex;", resumeBody);
        Assert.Contains("_isPaused = true;", resumeBody);
        Assert.DoesNotContain(".Session.Tick(", resumeBody);
        Assert.DoesNotContain("return StartAsync(resumeOptions);", resumeBody);

        var renderLatestIndex = playModeHost.IndexOf(
            "RenderRestoredStateWithoutAdvancingClient(restoredState, resumeOptions);",
            StringComparison.Ordinal);
        var resumeIndex = playModeHost.IndexOf("_isPaused = false;", renderLatestIndex, StringComparison.Ordinal);
        Assert.True(renderLatestIndex >= 0 && resumeIndex > renderLatestIndex,
            "The latest full state must render before client advancement resumes.");
    }

    [Fact]
    public void PlayModeMenuExposesRemotePauseAndResumeControls()
    {
        var playModeMenu = ReadUnityPackageSource(
            "com.abilitykit.demo.shooter.view.runtime",
            "Runtime", "Unity", "PlayMode", "ShooterPlayModeMenu.cs");
        var formalMultiplayerController = ReadUnityPackageSource(
            "com.abilitykit.demo.shooter.view.runtime",
            "Runtime", "Unity", "PlayMode", "ShooterFormalMultiplayerController.cs");

        Assert.Contains("Pause Client", playModeMenu);
        Assert.Contains("ShooterRemoteStateSyncPlayModeHost.Pause();", playModeMenu);
        Assert.Contains("Resume & Refresh", playModeMenu);
        Assert.Contains("RunAsync(\"refresh latest state\", ResumeRemoteAsync);", playModeMenu);
        Assert.Contains("ShooterRemoteStateSyncPlayModeHost.ResumeFromPauseAsync()", playModeMenu);
        Assert.Contains("IsAutoReconnecting", playModeMenu);
        Assert.Contains("IsWaitingForInitialFullStateSync", playModeMenu);
        Assert.Contains("LastInitialFullStateSyncApplyResult", playModeMenu);
        Assert.Contains("return \"Syncing Latest State\";", playModeMenu);
        Assert.Contains("return \"Refreshing Latest State\";", playModeMenu);
        Assert.Contains("return \"Auto Reconnecting\";", playModeMenu);
        Assert.Contains("return \"Paused\";", playModeMenu);
        Assert.Contains("State: Client paused", formalMultiplayerController);
        Assert.Contains("State: Refreshing latest state", formalMultiplayerController);
        Assert.Contains("Pause Client", formalMultiplayerController);
        Assert.Contains("Resume & Refresh", formalMultiplayerController);
        Assert.Contains("ShooterRemoteStateSyncPlayModeHost.Pause();", formalMultiplayerController);
        Assert.DoesNotContain("Simulate Disconnect", formalMultiplayerController);
        Assert.DoesNotContain("simulating disconnect", formalMultiplayerController);
    }

    [Fact]
    public void RemoteInputPathDoesNotCreateASecondSessionLifecycle()
    {
        var root = FindRepositoryRoot(AppContext.BaseDirectory);
        var packageRoot = Path.Combine(root, "Unity", "Packages", "com.abilitykit.demo.shooter.view.runtime");
        var asmdef = ReadUnityPackageSource(
            "com.abilitykit.demo.shooter.view.runtime",
            "Runtime", "com.abilitykit.demo.shooter.view.runtime.asmdef");

        Assert.DoesNotContain("AbilityKit.Coordinator", asmdef);
        Assert.False(File.Exists(Path.Combine(packageRoot, "Runtime", "Hosting", "ShooterCoordinatorInputBridge.cs")));
        Assert.False(File.Exists(Path.Combine(packageRoot, "Runtime", "Hosting", "ShooterCoordinatorSessionHost.cs")));
        Assert.False(File.Exists(Path.Combine(packageRoot, "Runtime", "Hosting", "ShooterGatewayCoordinatorInputTransport.cs")));
    }

    [Fact]
    public void RestoreFirstConnectionUsesFrameworkPolicy()
    {
        var connectionFlow = ReadUnityPackageSource(
            "com.abilitykit.demo.shooter.view.runtime",
            "Runtime", "PlayMode", "ShooterRemoteStateSyncConnectionFlow.cs");
        var restoreFirstPolicy = ReadUnityPackageSource(
            "com.abilitykit.network.room",
            "Runtime", "RoomGatewayRestoreFirstConnectionPolicy.cs");
        var root = FindRepositoryRoot(AppContext.BaseDirectory);
        var legacyPolicyPath = Path.Combine(
            root,
            "Unity", "Packages", "com.abilitykit.host.extension",
            "Runtime", "Session", "RoomGatewayRestoreFirstConnectionPolicy.cs");

        Assert.Contains("RoomGatewayRestoreFirstConnectionPolicy.ConnectAsync", connectionFlow);
        Assert.Contains("RestoreRoomAsLaunchAsync", connectionFlow);
        Assert.DoesNotContain("catch (Exception ex) when (launchOptions.LaunchMode == ShooterRemoteStateSyncLaunchMode.RestoreFirst)", connectionFlow);
        Assert.Contains("public static class RoomGatewayRestoreFirstConnectionPolicy", restoreFirstPolicy);
        Assert.False(File.Exists(legacyPolicyPath));
        Assert.Contains("allowFallbackCreate", restoreFirstPolicy);
        Assert.Contains("UsedFallbackCreate", restoreFirstPolicy);
        Assert.Contains("RestoreFailure", restoreFirstPolicy);
    }

    [Fact]
    public void SyncCoreDirectlyComposesFrameAndInputControllers()
    {
        var inputCoordinator = ReadUnityPackageSource(
            "com.abilitykit.demo.shooter.view.runtime",
            "Runtime", "Client", "Session", "ShooterClientInputCoordinator.cs");
        var syncCore = ReadUnityPackageSource(
            "com.abilitykit.demo.shooter.view.runtime",
            "Runtime", "Client", "Synchronization", "ShooterClientSyncCore.cs");
        var predictRollback = ReadUnityPackageSource(
            "com.abilitykit.demo.shooter.view.runtime",
            "Runtime", "Client", "Synchronization", "ShooterClientPredictRollbackSyncController.cs");
        var authoritativeInterpolation = ReadUnityPackageSource(
            "com.abilitykit.demo.shooter.view.runtime",
            "Runtime", "Client", "Synchronization", "ShooterClientAuthoritativeInterpolationSyncController.cs");

        Assert.Contains("private readonly ShooterClientFrameSyncController _frameSync;", inputCoordinator);
        Assert.Contains("public ShooterClientInputCoordinator(ShooterClientFrameSyncController frameSync", inputCoordinator);
        Assert.DoesNotContain("public ShooterClientInputCoordinator(ShooterClientFrameSyncCoordinator", inputCoordinator);
        Assert.Contains("private readonly ShooterClientFrameSyncController _frameSync;", syncCore);
        Assert.Contains("new ShooterClientInputCoordinator(_frameSync, gateway)", syncCore);
        Assert.DoesNotContain("ShooterClientFrameSyncCoordinator", syncCore);
        Assert.Contains("private readonly ShooterClientSyncCore _core;", predictRollback);
        Assert.Contains("private readonly ShooterClientSyncCore _core;", authoritativeInterpolation);
        Assert.DoesNotContain("ShooterClientFrameSyncCoordinator", predictRollback);
        Assert.DoesNotContain("ShooterClientFrameSyncCoordinator", authoritativeInterpolation);
    }

    private static string ReadUnityPackageSource(string packageName, params string[] relativeParts)
    {
        var root = FindRepositoryRoot(AppContext.BaseDirectory);
        var path = Path.Combine(root, "Unity", "Packages", packageName, Path.Combine(relativeParts));
        Assert.True(File.Exists(path), $"Expected Unity package source file to exist: {path}");
        return File.ReadAllText(path);
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                && Directory.Exists(Path.Combine(directory.FullName, "Unity"))
                && Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate repository root from {startDirectory}.");
    }
}
