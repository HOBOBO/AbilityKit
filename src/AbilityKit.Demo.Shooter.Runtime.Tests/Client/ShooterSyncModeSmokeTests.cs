using System.Collections.Generic;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Demo.Harness;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

[Collection("ShooterAcceptance")]
public sealed class ShooterSyncModeSmokeTests
{
    private const int SmokeStepCount = 4;
    private const int ConvergenceStepCount = 8;
    private const float SmokeDeltaSeconds = 1f / 30f;

    public static IEnumerable<object[]> ImplementedSyncModes()
    {
        yield return new object[]
        {
            NetworkSyncModel.PredictRollback,
            ShooterDemoHarnessCarrier.DefaultCarrierName,
            typeof(ShooterClientPredictRollbackSyncController),
            false
        };
        yield return new object[]
        {
            NetworkSyncModel.AuthoritativeInterpolation,
            ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName,
            typeof(ShooterClientAuthoritativeInterpolationSyncController),
            true
        };
        yield return new object[]
        {
            NetworkSyncModel.BatchStateSync,
            ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName,
            typeof(ShooterClientAuthoritativeInterpolationSyncController),
            true
        };
        yield return new object[]
        {
            NetworkSyncModel.MassBattleLodSync,
            ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName,
            typeof(ShooterClientAuthoritativeInterpolationSyncController),
            true
        };
        yield return new object[]
        {
            NetworkSyncModel.HybridHeroPrediction,
            ShooterHybridDemoHarnessCarrier.DefaultCarrierName,
            typeof(ShooterClientHybridHeroPredictionSyncController),
            true
        };
    }

    public static IEnumerable<object[]> RuntimeConvergentClientServerSyncModes()
    {
        yield return new object[]
        {
            NetworkSyncModel.PredictRollback,
            ShooterDemoHarnessCarrier.DefaultCarrierName,
            typeof(ShooterClientPredictRollbackSyncController),
            false
        };
        yield return new object[]
        {
            NetworkSyncModel.HybridHeroPrediction,
            ShooterHybridDemoHarnessCarrier.DefaultCarrierName,
            typeof(ShooterClientHybridHeroPredictionSyncController),
            true
        };
    }

    [Theory]
    [MemberData(nameof(ImplementedSyncModes))]
    public void ImplementedSyncModeRunsIdealAuthoritativeSmokeToHealthyState(
        NetworkSyncModel syncModel,
        string expectedCarrierName,
        System.Type expectedControllerType,
        bool expectsInterpolationDiagnostics)
    {
        using var session = ShooterAcceptanceLab.Create(
            syncModel,
            NetworkConditionProfile.Ideal,
            networkName: $"{syncModel} Smoke",
            randomSeed: 1401,
            interpolationConfig: SmokeInterpolationConfig(),
            enableAuthoritativeWorld: true);

        var result = session.Run(SmokeStepCount, SmokeDeltaSeconds, seed: 1401);

        Assert.Equal(syncModel, session.SyncModel);
        Assert.Equal(expectedCarrierName, session.Carrier.CarrierName);
        Assert.IsType(expectedControllerType, session.Controller);
        Assert.Equal(DemoHarnessRunStatus.Completed, result.Status);
        Assert.True(result.Completed);
        Assert.Equal(syncModel, result.Scenario.SyncModel);
        Assert.Equal(expectedCarrierName, result.Scenario.CarrierName);
        Assert.Equal(NetworkConditionProfile.Ideal, result.Scenario.NetworkProfile);
        Assert.Equal(SmokeStepCount, result.Scenario.StepCount);
        Assert.Equal(SmokeStepCount, result.Metrics.StepsRun);
        Assert.Equal(SmokeStepCount, result.Metrics.TotalTicks);
        Assert.Equal(session.Runtime.CurrentFrame, result.Metrics.LastFrame);
        Assert.Equal(session.Presentation.ViewModel.Frame, result.Metrics.LastFrame);
        Assert.Equal(0, result.Metrics.HealthErrorCount);
        Assert.Equal(0, result.Metrics.HealthWarningCount);
        Assert.True(result.Metrics.LastFrame > 0);

        var snapshot = session.Runtime.GetSnapshot();
        Assert.Equal(result.Metrics.LastFrame, snapshot.Frame);
        Assert.Equal(2, snapshot.Players.Length);
        Assert.False(session.Controller.NeedsFullSnapshotResync);

        Assert.True(session.HasAuthoritativeWorld);
        Assert.NotNull(session.AuthoritativeWorld);
        Assert.Equal(SmokeStepCount, session.AuthoritativeWorld!.CurrentFrame);
        Assert.Equal(SmokeStepCount, session.LastCarrierTimeAnchor.LocalFrame);
        Assert.Equal(SmokeStepCount, session.LastCarrierTimeAnchor.AuthoritativeFrame);
        Assert.NotNull(session.CarrierNetworkStats);
        Assert.Equal(SmokeStepCount, session.CarrierNetworkStats.Value.InboundReceived);
        Assert.Equal(SmokeStepCount, session.CarrierNetworkStats.Value.InboundDelivered);
        Assert.Equal(0, session.CarrierNetworkStats.Value.PendingCount);
        Assert.NotNull(session.LastCarrierSnapshotApplyResult);
        Assert.NotEqual(ShooterSnapshotApplyResult.Ignored, session.LastCarrierSnapshotApplyResult.Value);

        var comparison = session.CompareWorlds();
        Assert.Equal(snapshot.Frame, comparison.ClientFrame);
        Assert.Equal(session.AuthoritativeWorld.CurrentFrame, comparison.AuthorityFrame);
        Assert.Equal(2, comparison.Divergences.Count);

        AssertInterpolationSmoke(session, expectsInterpolationDiagnostics);
    }

    [Theory]
    [MemberData(nameof(ImplementedSyncModes))]
    public void ImplementedSyncModeRunsFourPlayerAuthoritativeSmokeToHealthyState(
        NetworkSyncModel syncModel,
        string expectedCarrierName,
        System.Type expectedControllerType,
        bool expectsInterpolationDiagnostics)
    {
        const int playerCount = 4;
        var players = FourPlayerRoster();
        using var session = ShooterAcceptanceLab.Create(
            syncModel,
            NetworkConditionProfile.Ideal,
            networkName: $"{syncModel} Four Player Smoke",
            randomSeed: 2401,
            players: players,
            matchId: $"{syncModel}-four-player-smoke".ToLowerInvariant(),
            interpolationConfig: SmokeInterpolationConfig(),
            enableAuthoritativeWorld: true);

        SubmitMultiPlayerFrame(session, 0, new[]
        {
            new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, false),
            new ShooterPlayerCommand(2, -1f, 0f, -1f, 0f, false),
            new ShooterPlayerCommand(3, 0f, 1f, 0f, 1f, false),
            new ShooterPlayerCommand(4, 0f, -1f, 0f, -1f, false)
        });
        SubmitMultiPlayerFrame(session, 1, new[]
        {
            new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true),
            new ShooterPlayerCommand(2, 0f, 0f, -1f, 0f, true),
            new ShooterPlayerCommand(3, 0f, 0f, 0f, 1f, false),
            new ShooterPlayerCommand(4, 0f, 0f, 0f, -1f, false)
        });

        var result = session.Run(SmokeStepCount, SmokeDeltaSeconds, seed: 2401);

        Assert.Equal(syncModel, session.SyncModel);
        Assert.Equal(expectedCarrierName, session.Carrier.CarrierName);
        Assert.IsType(expectedControllerType, session.Controller);
        Assert.Equal(DemoHarnessRunStatus.Completed, result.Status);
        Assert.True(result.Completed);
        Assert.Equal(SmokeStepCount, result.Metrics.StepsRun);
        Assert.Equal(SmokeStepCount, result.Metrics.TotalTicks);
        Assert.Equal(0, result.Metrics.HealthErrorCount);
        Assert.Equal(0, result.Metrics.HealthWarningCount);

        var snapshot = session.Runtime.GetSnapshot();
        Assert.Equal(playerCount, snapshot.Players.Length);
        Assert.False(session.Controller.NeedsFullSnapshotResync);
        AssertContainsPlayers(snapshot.Players, playerCount);

        Assert.True(session.HasAuthoritativeWorld);
        Assert.NotNull(session.AuthoritativeWorld);
        Assert.Equal(playerCount, session.AuthoritativeWorld!.GetSnapshot().Players.Length);
        Assert.Equal(SmokeStepCount, session.AuthoritativeWorld.CurrentFrame);
        Assert.Equal(SmokeStepCount, session.LastCarrierTimeAnchor.LocalFrame);
        Assert.Equal(SmokeStepCount, session.LastCarrierTimeAnchor.AuthoritativeFrame);
        Assert.NotNull(session.CarrierNetworkStats);
        Assert.Equal(SmokeStepCount, session.CarrierNetworkStats.Value.InboundReceived);
        Assert.Equal(SmokeStepCount, session.CarrierNetworkStats.Value.InboundDelivered);
        Assert.Equal(0, session.CarrierNetworkStats.Value.PendingCount);
        Assert.NotNull(session.LastCarrierSnapshotApplyResult);
        Assert.NotEqual(ShooterSnapshotApplyResult.Ignored, session.LastCarrierSnapshotApplyResult.Value);

        var comparison = session.CompareWorlds();
        Assert.Equal(snapshot.Frame, comparison.ClientFrame);
        Assert.Equal(session.AuthoritativeWorld.CurrentFrame, comparison.AuthorityFrame);
        Assert.Equal(playerCount, comparison.Divergences.Count);
        for (var playerId = 1; playerId <= playerCount; playerId++)
        {
            Assert.Contains(comparison.Divergences, d => d.PlayerId == playerId);
        }

        AssertInterpolationSmoke(session, expectsInterpolationDiagnostics);
    }

    [Theory]
    [MemberData(nameof(ImplementedSyncModes))]
    public void ImplementedSyncModeFourIndependentClientsConvergeToSameFinalSnapshot(
        NetworkSyncModel syncModel,
        string expectedCarrierName,
        System.Type expectedControllerType,
        bool expectsInterpolationDiagnostics)
    {
        const int playerCount = 4;
        var sessions = new List<ShooterAcceptanceSession>(playerCount);
        try
        {
            for (var controlledPlayerId = 1; controlledPlayerId <= playerCount; controlledPlayerId++)
            {
                sessions.Add(ShooterAcceptanceLab.Create(
                    syncModel,
                    NetworkConditionProfile.Ideal,
                    networkName: $"{syncModel} Client {controlledPlayerId} Convergence",
                    randomSeed: 3401,
                    players: FourPlayerRoster(),
                    matchId: $"{syncModel}-four-client-convergence".ToLowerInvariant(),
                    interpolationConfig: SmokeInterpolationConfig(),
                    enableAuthoritativeWorld: true));
            }

            SubmitMultiClientFrame(sessions, 0, new[]
            {
                new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, false),
                new ShooterPlayerCommand(2, -1f, 0f, -1f, 0f, false),
                new ShooterPlayerCommand(3, 0f, 1f, 0f, 1f, false),
                new ShooterPlayerCommand(4, 0f, -1f, 0f, -1f, false)
            });
            SubmitMultiClientFrame(sessions, 1, new[]
            {
                new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true),
                new ShooterPlayerCommand(2, 0f, 0f, -1f, 0f, true),
                new ShooterPlayerCommand(3, -1f, 0f, -1f, 0f, false),
                new ShooterPlayerCommand(4, 1f, 0f, 1f, 0f, false)
            });
            SubmitMultiClientFrame(sessions, 2, new[]
            {
                new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, false),
                new ShooterPlayerCommand(2, 0f, 0f, -1f, 0f, false),
                new ShooterPlayerCommand(3, 0f, 0f, 0f, 1f, true),
                new ShooterPlayerCommand(4, 0f, 0f, 0f, -1f, true)
            });

            for (var i = 0; i < sessions.Count; i++)
            {
                var result = sessions[i].Run(ConvergenceStepCount, SmokeDeltaSeconds, seed: 3401 + i);
                Assert.Equal(syncModel, sessions[i].SyncModel);
                Assert.Equal(expectedCarrierName, sessions[i].Carrier.CarrierName);
                Assert.IsType(expectedControllerType, sessions[i].Controller);
                Assert.Equal(DemoHarnessRunStatus.Completed, result.Status);
                Assert.True(result.Completed);
                Assert.Equal(0, result.Metrics.HealthErrorCount);
                Assert.Equal(0, result.Metrics.HealthWarningCount);
                Assert.Equal(ConvergenceStepCount, sessions[i].Runtime.CurrentFrame);
                Assert.Equal(ConvergenceStepCount, sessions[i].AuthoritativeWorld!.CurrentFrame);
                Assert.False(sessions[i].Controller.NeedsFullSnapshotResync);
                AssertInterpolationSmoke(sessions[i], expectsInterpolationDiagnostics);
            }

            var expectedSnapshot = sessions[0].Runtime.GetSnapshot();
            var expectedHash = sessions[0].Runtime.ComputeStateHash();
            Assert.Equal(playerCount, expectedSnapshot.Players.Length);
            AssertContainsPlayers(expectedSnapshot.Players, playerCount);

            for (var i = 1; i < sessions.Count; i++)
            {
                Assert.Equal(expectedHash, sessions[i].Runtime.ComputeStateHash());
                AssertFinalSnapshotEqual(expectedSnapshot, sessions[i].Runtime.GetSnapshot());
            }
        }
        finally
        {
            for (var i = 0; i < sessions.Count; i++)
            {
                sessions[i].Dispose();
            }
        }
    }

    [Theory]
    [MemberData(nameof(RuntimeConvergentClientServerSyncModes))]
    public void ImplementedSyncModeFourClientsConvergeThroughSingleAuthoritativeServer(
        NetworkSyncModel syncModel,
        string expectedCarrierName,
        System.Type expectedControllerType,
        bool expectsInterpolationDiagnostics)
    {
        const int playerCount = 4;
        var matchId = $"{syncModel}-single-server-topology".ToLowerInvariant();
        var players = FourPlayerRosterArray();
        var start = new ShooterStartGamePayload(
            matchId,
            ShooterAcceptanceLab.DefaultTickRate,
            4401,
            players);
        using var server = ShooterBattleWorldSession.Create($"{matchId}-server");
        Assert.True(server.Runtime.StartGame(in start));

        var sessions = new List<ShooterAcceptanceSession>(playerCount);
        var links = new List<ShooterCarrierNetworkLink>(playerCount);
        try
        {
            for (var controlledPlayerId = 1; controlledPlayerId <= playerCount; controlledPlayerId++)
            {
                var session = ShooterAcceptanceLab.Create(
                    syncModel,
                    NetworkConditionProfile.Ideal,
                    networkName: $"{syncModel} Client {controlledPlayerId} Single Server",
                    randomSeed: 4401,
                    players: players,
                    matchId: matchId,
                    interpolationConfig: SmokeInterpolationConfig(),
                    enableAuthoritativeWorld: false);
                sessions.Add(session);
                links.Add(new ShooterCarrierNetworkLink(session.Controller, NetworkConditionProfile.Ideal, seed: 4401 + controlledPlayerId));
            }

            for (var commandFrame = 0; commandFrame < ConvergenceStepCount; commandFrame++)
            {
                var commands = BuildClientServerFrameCommands(commandFrame);
                Assert.Equal(playerCount, commands.Length);

                for (var i = 0; i < sessions.Count; i++)
                {
                    for (var commandIndex = 0; commandIndex < commands.Length; commandIndex++)
                    {
                        Assert.Equal(1, sessions[i].Controller.SubmitLocalInput(in commands[commandIndex]).AcceptedInputs);
                    }
                }

                Assert.Equal(playerCount, server.Runtime.SubmitInput(commandFrame, commands));

                for (var i = 0; i < sessions.Count; i++)
                {
                    var tick = sessions[i].Controller.Tick(SmokeDeltaSeconds);
                    Assert.Equal(commandFrame + 1, tick.Frame);
                }

                Assert.True(server.Runtime.Tick(SmokeDeltaSeconds));
                Assert.Equal(commandFrame + 1, server.Runtime.CurrentFrame);

                var timestamp = (commandFrame + 1) * SmokeDeltaSeconds;
                var clockMs = (long)System.Math.Round(timestamp * 1000d);
                var packed = server.Runtime.ExportPackedSnapshot(worldId: 1UL, isFullSnapshot: true, authorityOverride: true);
                for (var i = 0; i < links.Count; i++)
                {
                    links[i].PublishSnapshot(in packed, timestamp);
                    links[i].Advance(clockMs);
                    Assert.Equal(ShooterSnapshotApplyResult.AppliedPackedSnapshot, links[i].LastApplyResult);
                }
            }

            var serverSnapshot = server.Runtime.GetSnapshot();
            var serverHash = server.Runtime.ComputeStateHash();
            Assert.Equal(ConvergenceStepCount, serverSnapshot.Frame);
            Assert.Equal(playerCount, serverSnapshot.Players.Length);
            AssertContainsPlayers(serverSnapshot.Players, playerCount);

            for (var i = 0; i < sessions.Count; i++)
            {
                Assert.Equal(syncModel, sessions[i].SyncModel);
                Assert.Equal(expectedCarrierName, sessions[i].Carrier.CarrierName);
                Assert.IsType(expectedControllerType, sessions[i].Controller);
                Assert.False(sessions[i].HasAuthoritativeWorld);
                Assert.Equal(ConvergenceStepCount, sessions[i].Runtime.CurrentFrame);
                Assert.False(sessions[i].Controller.NeedsFullSnapshotResync);
                Assert.Equal(serverHash, sessions[i].Runtime.ComputeStateHash());
                AssertFinalSnapshotEqual(serverSnapshot, sessions[i].Runtime.GetSnapshot());
                AssertClientServerInterpolationBuffer(sessions[i], expectsInterpolationDiagnostics);
            }
        }
        finally
        {
            for (var i = 0; i < sessions.Count; i++)
            {
                sessions[i].Dispose();
            }
        }
    }

    [Fact]
    public void RunnableCatalogTemplatesRunTemplateDrivenSmokeToHealthyState()
    {
        foreach (var template in ShooterAcceptanceCatalog.SyncTemplates)
        {
            if (!template.IsRunnable)
            {
                continue;
            }

            using var session = ShooterAcceptanceLab.Create(in template);
            var result = session.Run(SmokeStepCount, SmokeDeltaSeconds, seed: 6401);

            Assert.Equal(template.SyncModel, session.SyncModel);
            Assert.Equal(template.DisplayName, session.NetworkName);
            Assert.Equal(template.ExpectedCarrierName, session.Carrier.CarrierName);
            Assert.Equal(template.RecommendedPlayerCount, session.Runtime.GetSnapshot().Players.Length);
            Assert.Equal(DemoHarnessRunStatus.Completed, result.Status);
            Assert.True(result.Completed);
            Assert.Equal(template.SyncModel, result.Scenario.SyncModel);
            Assert.Equal(template.ExpectedCarrierName, result.Scenario.CarrierName);
            Assert.Equal(SmokeStepCount, result.Metrics.StepsRun);
            Assert.Equal(SmokeStepCount, result.Metrics.TotalTicks);
            Assert.Equal(0, result.Metrics.HealthErrorCount);
            Assert.Equal(0, result.Metrics.HealthWarningCount);
            Assert.False(session.Controller.NeedsFullSnapshotResync);
            Assert.Equal(template.ExpectsInterpolationDiagnostics, session.Controller is IInterpolationDiagnosticsProvider);
        }
    }

    [Fact]
    public void BatchAndMassBattleTemplatesRunDelayedPlaybackSmokeWithExpectedSendPolicy()
    {
        var templateIds = new[]
        {
            "batch-state-low-frequency",
            "mass-battle-lod-aoi"
        };

        foreach (var templateId in templateIds)
        {
            var template = ShooterAcceptanceCatalog.GetSyncTemplate(templateId);
            using var session = ShooterAcceptanceLab.Create(in template);
            var result = session.Run(SmokeStepCount, SmokeDeltaSeconds, seed: 7401);

            Assert.Equal(DemoHarnessRunStatus.Completed, result.Status);
            Assert.True(result.Completed);
            Assert.True(template.SendPolicy.BatchWindowFrames > 1);
            Assert.True(template.SendPolicy.SnapshotIntervalFrames > 1);
            Assert.True(template.SendPolicy.InterpolationDelayFrames > 0);
            Assert.True(template.SendPolicy.ActiveEntityBudget <= template.SendPolicy.MaxEntityCount);
            Assert.IsType<ShooterClientAuthoritativeInterpolationSyncController>(session.Controller);
            Assert.Equal(ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName, session.Carrier.CarrierName);

            var diagnosticsProvider = Assert.IsAssignableFrom<IInterpolationDiagnosticsProvider>(session.Controller);
            session.Controller.Tick(SmokeDeltaSeconds);
            var diagnostics = diagnosticsProvider.GetInterpolationDiagnostics();
            Assert.True(diagnostics.BufferedRemoteSnapshotCount >= 0);
            Assert.True(diagnostics.EstimatedServerTicks >= 0);
            Assert.True(diagnostics.RemotePlaybackTicks >= 0);
            Assert.False(diagnostics.IsRemotePlaybackStarved);
        }
    }

    [Fact]
    public void AuthoritativeInterpolationRunsStateSyncSnapshotPushSmoke()
    {
        const int playerCount = 4;
        var syncModel = NetworkSyncModel.AuthoritativeInterpolation;
        var matchId = "authoritative-interpolation-state-sync-smoke";
        var players = FourPlayerRosterArray();
        var start = new ShooterStartGamePayload(
            matchId,
            ShooterAcceptanceLab.DefaultTickRate,
            5401,
            players);
        using var server = ShooterBattleWorldSession.Create($"{matchId}-server");
        Assert.True(server.Runtime.StartGame(in start));

        using var session = ShooterAcceptanceLab.Create(
            syncModel,
            NetworkConditionProfile.Ideal,
            networkName: "Authoritative Interpolation State Sync Smoke",
            randomSeed: 5401,
            players: players,
            matchId: matchId,
            interpolationConfig: SmokeInterpolationConfig(),
            enableAuthoritativeWorld: false);
        var link = new ShooterCarrierNetworkLink(session.Controller, NetworkConditionProfile.Ideal, seed: 5401);

        for (var commandFrame = 0; commandFrame < SmokeStepCount; commandFrame++)
        {
            var commands = BuildClientServerFrameCommands(commandFrame);
            Assert.Equal(playerCount, commands.Length);

            for (var i = 0; i < commands.Length; i++)
            {
                session.Controller.SubmitLocalInput(in commands[i]);
            }

            Assert.Equal(playerCount, server.Runtime.SubmitInput(commandFrame, commands));
            var tick = session.Controller.Tick(SmokeDeltaSeconds);
            Assert.Equal(commandFrame + 1, tick.Frame);
            Assert.True(server.Runtime.Tick(SmokeDeltaSeconds));

            var timestamp = (commandFrame + 1) * SmokeDeltaSeconds;
            var clockMs = (long)System.Math.Round(timestamp * 1000d);
            var packed = server.Runtime.ExportPackedSnapshot(worldId: 1UL, isFullSnapshot: true, authorityOverride: true);
            link.PublishSnapshot(in packed, timestamp);
            link.Advance(clockMs);

            Assert.NotEqual(ShooterSnapshotApplyResult.Ignored, link.LastApplyResult);
            Assert.Equal(commandFrame + 1, session.Runtime.CurrentFrame);
            Assert.False(session.Controller.NeedsFullSnapshotResync);
        }

        var serverSnapshot = server.Runtime.GetSnapshot();
        Assert.Equal(syncModel, session.SyncModel);
        Assert.Equal(ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName, session.Carrier.CarrierName);
        Assert.IsType<ShooterClientAuthoritativeInterpolationSyncController>(session.Controller);
        Assert.False(session.HasAuthoritativeWorld);
        Assert.Equal(SmokeStepCount, session.Runtime.CurrentFrame);
        Assert.Equal(SmokeStepCount, serverSnapshot.Frame);
        Assert.Equal(playerCount, serverSnapshot.Players.Length);
        Assert.Equal(server.Runtime.ComputeStateHash(), session.Runtime.ComputeStateHash());
        AssertFinalSnapshotEqual(serverSnapshot, session.Runtime.GetSnapshot());
        AssertClientServerInterpolationBuffer(session, expectsInterpolationDiagnostics: true);
    }

    [Fact]
    public void ImplementedSyncModeSmokeMatrixContainsEveryCatalogModeOnce()
    {
        var seen = new HashSet<NetworkSyncModel>();
        foreach (var row in ImplementedSyncModes())
        {
            seen.Add((NetworkSyncModel)row[0]);
        }

        foreach (var option in ShooterAcceptanceCatalog.SyncModes)
        {
            if (option.Implemented)
            {
                Assert.Contains(option.Model, seen);
            }
        }

        Assert.Equal(5, seen.Count);
    }

    private static void AssertInterpolationSmoke(
        ShooterAcceptanceSession session,
        bool expectsInterpolationDiagnostics)
    {
        if (!expectsInterpolationDiagnostics)
        {
            Assert.False(session.Controller is IInterpolationDiagnosticsProvider);
            return;
        }

        var diagnosticsProvider = Assert.IsAssignableFrom<IInterpolationDiagnosticsProvider>(session.Controller);
        var beforeTick = diagnosticsProvider.GetInterpolationDiagnostics();
        Assert.True(beforeTick.BufferedRemoteSnapshotCount > 0);
        Assert.True(beforeTick.EstimatedServerTicks > 0);

        session.Controller.Tick(SmokeDeltaSeconds);

        var afterTick = diagnosticsProvider.GetInterpolationDiagnostics();
        Assert.True(afterTick.BufferedRemoteSnapshotCount > 0);
        Assert.True(afterTick.EstimatedServerTicks > 0);
        Assert.True(afterTick.RemotePlaybackTicks >= 0);
        Assert.True(afterTick.HasPublishedRemoteFrame);
        Assert.False(afterTick.IsRemotePlaybackStarved);
    }

    private static void AssertClientServerInterpolationBuffer(
        ShooterAcceptanceSession session,
        bool expectsInterpolationDiagnostics)
    {
        if (!expectsInterpolationDiagnostics)
        {
            Assert.False(session.Controller is IInterpolationDiagnosticsProvider);
            return;
        }

        var diagnosticsProvider = Assert.IsAssignableFrom<IInterpolationDiagnosticsProvider>(session.Controller);
        var diagnostics = diagnosticsProvider.GetInterpolationDiagnostics();
        Assert.True(diagnostics.BufferedRemoteSnapshotCount > 0);
        Assert.True(diagnostics.EstimatedServerTicks > 0);
        Assert.False(diagnostics.IsRemotePlaybackStarved);
    }

    private static void SubmitMultiPlayerFrame(
        ShooterAcceptanceSession session,
        int commandFrame,
        IReadOnlyList<ShooterPlayerCommand> commands)
    {
        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            session.Controller.SubmitLocalInput(in command);
            session.EnqueueAuthoritativeInput(commandFrame, in command);
        }
    }

    private static void SubmitMultiClientFrame(
        IReadOnlyList<ShooterAcceptanceSession> sessions,
        int commandFrame,
        IReadOnlyList<ShooterPlayerCommand> commands)
    {
        for (var i = 0; i < sessions.Count; i++)
        {
            SubmitMultiPlayerFrame(sessions[i], commandFrame, commands);
        }
    }

    private static IReadOnlyList<ShooterStartPlayer> FourPlayerRoster()
    {
        return FourPlayerRosterArray();
    }

    private static ShooterStartPlayer[] FourPlayerRosterArray()
    {
        return new[]
        {
            new ShooterStartPlayer(1, "P1", -1.5f, 0f),
            new ShooterStartPlayer(2, "P2", 1.5f, 0f),
            new ShooterStartPlayer(3, "P3", 0f, 1.5f),
            new ShooterStartPlayer(4, "P4", 0f, -1.5f)
        };
    }

    private static ShooterPlayerCommand[] BuildClientServerFrameCommands(int commandFrame)
    {
        switch (commandFrame % 4)
        {
            case 0:
                return new[]
                {
                    new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, false),
                    new ShooterPlayerCommand(2, -1f, 0f, -1f, 0f, false),
                    new ShooterPlayerCommand(3, 0f, 1f, 0f, 1f, false),
                    new ShooterPlayerCommand(4, 0f, -1f, 0f, -1f, false)
                };
            case 1:
                return new[]
                {
                    new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, true),
                    new ShooterPlayerCommand(2, 0f, 0f, -1f, 0f, true),
                    new ShooterPlayerCommand(3, -1f, 0f, -1f, 0f, false),
                    new ShooterPlayerCommand(4, 1f, 0f, 1f, 0f, false)
                };
            case 2:
                return new[]
                {
                    new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, false),
                    new ShooterPlayerCommand(2, 0f, 0f, -1f, 0f, false),
                    new ShooterPlayerCommand(3, 0f, 0f, 0f, 1f, true),
                    new ShooterPlayerCommand(4, 0f, 0f, 0f, -1f, true)
                };
            default:
                return new[]
                {
                    new ShooterPlayerCommand(1, -0.5f, 0f, -1f, 0f, false),
                    new ShooterPlayerCommand(2, 0.5f, 0f, 1f, 0f, false),
                    new ShooterPlayerCommand(3, 0f, -0.5f, 0f, -1f, false),
                    new ShooterPlayerCommand(4, 0f, 0.5f, 0f, 1f, false)
                };
        }
    }

    private static void AssertContainsPlayers(ShooterPlayerSnapshot[] players, int playerCount)
    {
        for (var playerId = 1; playerId <= playerCount; playerId++)
        {
            Assert.Contains(players, p => p.PlayerId == playerId);
        }
    }

    private static void AssertFinalSnapshotEqual(
        ShooterStateSnapshotPayload expected,
        ShooterStateSnapshotPayload actual)
    {
        Assert.Equal(expected.Frame, actual.Frame);
        Assert.Equal(expected.Players.Length, actual.Players.Length);
        Assert.Equal(expected.Bullets.Length, actual.Bullets.Length);
        Assert.Equal(expected.Events.Length, actual.Events.Length);

        for (var i = 0; i < expected.Players.Length; i++)
        {
            var expectedPlayer = expected.Players[i];
            var actualPlayer = actual.Players[i];
            Assert.Equal(expectedPlayer.PlayerId, actualPlayer.PlayerId);
            Assert.Equal(expectedPlayer.X, actualPlayer.X);
            Assert.Equal(expectedPlayer.Y, actualPlayer.Y);
            Assert.Equal(expectedPlayer.Hp, actualPlayer.Hp);
            Assert.Equal(expectedPlayer.Score, actualPlayer.Score);
            Assert.Equal(expectedPlayer.Alive, actualPlayer.Alive);
        }

        for (var i = 0; i < expected.Bullets.Length; i++)
        {
            var expectedBullet = expected.Bullets[i];
            Assert.True(TryFindBullet(actual.Bullets, expectedBullet.BulletId, out var actualBullet), $"Expected bullet {expectedBullet.BulletId} to exist in actual final snapshot.");
            Assert.Equal(expectedBullet.OwnerPlayerId, actualBullet.OwnerPlayerId);
            Assert.Equal(expectedBullet.X, actualBullet.X);
            Assert.Equal(expectedBullet.Y, actualBullet.Y);
            Assert.Equal(expectedBullet.VelocityX, actualBullet.VelocityX);
            Assert.Equal(expectedBullet.VelocityY, actualBullet.VelocityY);
            Assert.Equal(expectedBullet.RemainingFrames, actualBullet.RemainingFrames);
        }
    }

    private static bool TryFindBullet(ShooterBulletSnapshot[] bullets, int bulletId, out ShooterBulletSnapshot bullet)
    {
        for (var i = 0; i < bullets.Length; i++)
        {
            if (bullets[i].BulletId == bulletId)
            {
                bullet = bullets[i];
                return true;
            }
        }

        bullet = default;
        return false;
    }

    private static InterpolationConfig SmokeInterpolationConfig()
    {
        return new InterpolationConfig(
            ticksPerSecond: 1000L,
            interpolationDelayTicks: 0L,
            bufferCapacity: 8,
            catchUpRate: 0d,
            maxExtrapolationTicks: 250L);
    }
}
