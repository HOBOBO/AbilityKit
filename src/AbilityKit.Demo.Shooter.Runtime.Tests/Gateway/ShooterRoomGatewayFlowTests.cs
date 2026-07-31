using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.View;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterRoomGatewayFlowTests
{
    [Fact]
    public void RoomGatewayFlowUsesFrameworkStagedApisInsteadOfObsoleteAggregateApis()
    {
        var sourcePath = FindRepositoryFile(
            "Unity",
            "Packages",
            "com.abilitykit.demo.shooter.view.runtime",
            "Runtime",
            "Client",
            "Gateway",
            "ShooterRoomGatewayFlow.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("_flow.CreateReadyStartAndSubscribeAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_flow.JoinReadyStartAndSubscribeAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_flow.RestoreRoomAsync(", source, StringComparison.Ordinal);
        Assert.Contains("_flow.CreateRoomAsync(", source, StringComparison.Ordinal);
        Assert.Contains("_flow.JoinRoomAsync(", source, StringComparison.Ordinal);
        Assert.Contains("_flow.BeginLoadingAsync(", source, StringComparison.Ordinal);
        Assert.Contains("_flow.ReportAssetsLoadedAsync(", source, StringComparison.Ordinal);
        Assert.Contains("_flow.WaitForBattleStartAsync(", source, StringComparison.Ordinal);
        Assert.Contains("_flow.SubscribeStateSyncAsync(", source, StringComparison.Ordinal);
        Assert.Contains("_flow.RestoreAsync(", source, StringComparison.Ordinal);

        var frameworkSource = File.ReadAllText(FindRepositoryFile(
            "Unity",
            "Packages",
            "com.abilitykit.host.extension",
            "Runtime",
            "Session",
            "RoomGatewaySessionFlow.cs"));
        Assert.DoesNotContain("CreateReadyStartAndSubscribeAsync(", frameworkSource, StringComparison.Ordinal);
        Assert.DoesNotContain("JoinReadyStartAndSubscribeAsync(", frameworkSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Task<RoomGatewaySessionFlowResult> RestoreRoomAsync(", frameworkSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RoomGatewayFlowCreatesReadyStartsSubscribesAndBuildsBattleInputContext()
    {
        var roomClient = new ScriptedShooterRoomClient
        {
            JoinCurrentPlayerId = 121u
        };
        var flow = new ShooterRoomGatewayFlow(roomClient);
        var launchSpec = new ShooterRoomLaunchSpec(
            "local",
            "dev",
            "Shooter Room",
            ShooterGameplay.DefaultMaxPlayers,
            ShooterGameplay.GameplayId,
            ruleSetId: 1,
            configVersion: 1,
            protocolVersion: 1,
            ShooterGameplay.WorldType,
            "client-a",
            new Dictionary<string, string>
            {
                ["mode"] = "duo",
                ["syncTemplateId"] = "runtime-snapshot-interpolation",
                ["syncModel"] = "2",
                ["networkEnvironmentId"] = "wan-90ms",
                ["carrierName"] = "server",
                ["enableAuthoritativeWorld"] = "True",
                ["interpolationEnabled"] = "True",
                ["inputDelayFrames"] = "4"
            },
            "runtime-snapshot-interpolation",
            syncModel: 2,
            networkEnvironmentId: "wan-90ms",
            carrierName: "server",
            enableAuthoritativeWorld: true,
            interpolationEnabled: true,
            inputDelayFrames: 4);

        var result = await flow.CreateReadyStartAndSubscribeAsync("session-token", launchSpec, playerId: 21u);

        Assert.Equal("create:shooter", roomClient.Calls[0]);
        Assert.Equal("join:room-1", roomClient.Calls[1]);
        Assert.Equal("ready:room-1:True", roomClient.Calls[2]);
        Assert.Equal("begin-loading:room-1", roomClient.Calls[3]);
        var postLoadingIndex = AssertProgressSequence(roomClient.Calls, 4, "room-1");
        Assert.Equal("assets-loaded:room-1", roomClient.Calls[postLoadingIndex]);
        Assert.Equal("get-snapshot:room-1", roomClient.Calls[postLoadingIndex + 1]);
        Assert.Equal("subscribe:room-1:battle-1", roomClient.Calls[postLoadingIndex + 2]);
        Assert.DoesNotContain(roomClient.Calls, call => call.StartsWith("start:", StringComparison.Ordinal));
        Assert.Equal("session-token", roomClient.LastCreateRequest.SessionToken);
        Assert.Equal("local", roomClient.LastCreateRequest.Region);
        Assert.Equal("dev", roomClient.LastCreateRequest.ServerId);
        Assert.Equal(ShooterGameplay.RoomType, roomClient.LastCreateRequest.RoomType);
        Assert.Equal(ShooterGameplay.DefaultMaxPlayers, roomClient.LastCreateRequest.MaxPlayers);
        Assert.NotNull(roomClient.LastCreateRequest.Tags);
        var createTags = roomClient.LastCreateRequest.Tags!;
        Assert.Equal("duo", createTags["mode"]);
        Assert.Equal("runtime-snapshot-interpolation", createTags["syncTemplateId"]);
        Assert.Equal("2", createTags["syncModel"]);
        Assert.Equal("wan-90ms", createTags["networkEnvironmentId"]);
        Assert.Equal("server", createTags["carrierName"]);
        Assert.Equal("True", createTags["enableAuthoritativeWorld"]);
        Assert.Equal("True", createTags["interpolationEnabled"]);
        Assert.Equal("4", createTags["inputDelayFrames"]);
        Assert.Equal("room-1", roomClient.LastReportAssetsLoadedRequest.RoomId);
        Assert.Equal(7L, roomClient.LastReportAssetsLoadedRequest.LaunchGeneration);
        Assert.Equal(3, roomClient.LastReportAssetsLoadedRequest.ManifestVersion);
        Assert.Equal("manifest-shooter-v3", roomClient.LastReportAssetsLoadedRequest.ManifestHash);
        Assert.False(string.IsNullOrWhiteSpace(roomClient.LastReportAssetsLoadedRequest.CommandId));
        Assert.Equal(100, roomClient.LastReportLoadingProgressRequest.Progress);
        Assert.Equal(string.Empty, roomClient.LastSubscribeRequest.EventEpoch);
        Assert.Equal(0L, roomClient.LastSubscribeRequest.LastEventAck);
        Assert.Equal("room-1", result.RoomId);
        Assert.Equal(1001ul, result.NumericRoomId);
        Assert.Equal("battle-1", result.BattleId);
        Assert.Equal(9001ul, result.WorldId);
        Assert.Equal(121u, result.PlayerId);
        Assert.True(result.CanStart);
        Assert.True(result.Started);
        Assert.True(result.Subscribed);
        Assert.Equal(30, result.WorldStartAnchor.StartFrame);
        Assert.Equal(1200000L, result.ServerNowTicks);
        Assert.Equal(33, result.TargetFrame);
        Assert.Equal(3, result.CatchUpFrames);
        Assert.Equal(ShooterRoomGatewayEntryKind.TeamLobby, result.EntryKind);
        Assert.Equal("subscribed", result.Message);

        var inputContext = result.CreateBattleInputContext(frame: 8);
        Assert.Equal("session-token", inputContext.SessionToken);
        Assert.Equal("battle-1", inputContext.BattleId);
        Assert.Equal(9001ul, inputContext.WorldId);
        Assert.Equal(8, inputContext.Frame);
        Assert.Equal(121u, inputContext.PlayerId);
    }

    [Fact]
    public async Task RoomGatewayFlowJoinsExistingRoomWithoutCreate()
    {
        var roomClient = new ScriptedShooterRoomClient
        {
            JoinCurrentPlayerId = 131u
        };
        var flow = new ShooterRoomGatewayFlow(roomClient);

        var result = await flow.JoinReadyStartAndSubscribeAsync(
            "session-token",
            "existing-room",
            ShooterRoomLaunchSpec.CreateDefault("client-b"),
            playerId: 31u);

        Assert.DoesNotContain(roomClient.Calls, call => call.StartsWith("create:", StringComparison.Ordinal));
        Assert.Equal("join:existing-room", roomClient.Calls[0]);
        Assert.Equal("ready:existing-room:True", roomClient.Calls[1]);
        Assert.Equal("get-snapshot:existing-room", roomClient.Calls[2]);
        var postLoadingIndex = AssertProgressSequence(roomClient.Calls, 3, "existing-room");
        Assert.Equal("assets-loaded:existing-room", roomClient.Calls[postLoadingIndex]);
        Assert.Equal("get-snapshot:existing-room", roomClient.Calls[postLoadingIndex + 1]);
        Assert.Equal("subscribe:existing-room:battle-1", roomClient.Calls[postLoadingIndex + 2]);
        Assert.DoesNotContain(roomClient.Calls, call => call.StartsWith("begin-loading:", StringComparison.Ordinal));
        Assert.DoesNotContain(roomClient.Calls, call => call.StartsWith("start:", StringComparison.Ordinal));
        Assert.Equal(7L, roomClient.LastReportAssetsLoadedRequest.LaunchGeneration);
        Assert.Equal(3, roomClient.LastReportAssetsLoadedRequest.ManifestVersion);
        Assert.Equal("manifest-shooter-v3", roomClient.LastReportAssetsLoadedRequest.ManifestHash);
        Assert.Equal("existing-room", result.RoomId);
        Assert.Equal("battle-1", result.BattleId);
        Assert.Equal(131u, result.PlayerId);
        Assert.Equal(30, result.WorldStartAnchor.StartFrame);
        Assert.Equal(33, result.TargetFrame);
        Assert.Equal(ShooterRoomGatewayEntryKind.TeamLobby, result.EntryKind);
    }

    [Fact]
    public async Task RoomGatewayFlowReconnectsRunningBattleWithoutReadyOrStart()
    {
        var roomClient = new ScriptedShooterRoomClient
        {
            JoinKind = ShooterGatewayRoomJoinKind.Reconnect,
            JoinBattleId = "battle-running",
            JoinWorldId = 9101ul,
            JoinServerNowTicks = 1123456L,
            JoinWorldStartAnchor = new ShooterGatewayWorldStartAnchor(123456L, 10000000L, 18, 1d / 30d),
            JoinCanStart = false,
            JoinCurrentPlayerId = 141u
        };
        var flow = new ShooterRoomGatewayFlow(roomClient);

        var result = await flow.JoinReadyStartAndSubscribeAsync(
            "session-token",
            "running-room",
            ShooterRoomLaunchSpec.CreateDefault("client-reconnect"),
            playerId: 41u);

        Assert.Equal(2, roomClient.Calls.Count);
        Assert.Equal("join:running-room", roomClient.Calls[0]);
        Assert.Equal("subscribe:running-room:battle-running", roomClient.Calls[1]);
        Assert.DoesNotContain(roomClient.Calls, call => call.StartsWith("ready:", StringComparison.Ordinal));
        Assert.DoesNotContain(roomClient.Calls, call => call.StartsWith("start:", StringComparison.Ordinal));
        Assert.Equal(ShooterRoomGatewayEntryKind.Reconnect, result.EntryKind);
        Assert.Equal("battle-running", result.BattleId);
        Assert.Equal(9101ul, result.WorldId);
        Assert.Equal(1123456L, result.ServerNowTicks);
        Assert.Equal(21, result.TargetFrame);
        Assert.Equal(3, result.CatchUpFrames);
        Assert.False(result.CanStart);
        Assert.True(result.Started);
        Assert.True(result.Subscribed);
        Assert.Equal(141u, result.PlayerId);
    }

    [Fact]
    public async Task RoomGatewayFlowRestoreCarriesReliableEventCursorIntoSubscription()
    {
        var roomClient = new ScriptedShooterRoomClient
        {
            JoinBattleId = "battle-restored",
            JoinWorldId = 9301ul,
            JoinCurrentPlayerId = 143u,
            RestoreIsInBattle = true
        };
        var flow = new ShooterRoomGatewayFlow(roomClient);

        var result = await flow.RestoreRoomAsync(
            "session-token",
            "local",
            "dev",
            ShooterRoomLaunchSpec.CreateDefault("client-restore"),
            playerId: 43u,
            eventEpoch: "epoch-restore",
            lastEventAck: 27L);

        Assert.Equal("restore:local:dev", roomClient.Calls[0]);
        Assert.Equal("get-snapshot:room-1", roomClient.Calls[1]);
        Assert.Equal("subscribe:room-1:battle-restored", roomClient.Calls[2]);
        Assert.Equal("epoch-restore", roomClient.LastSubscribeRequest.EventEpoch);
        Assert.Equal(27L, roomClient.LastSubscribeRequest.LastEventAck);
        Assert.Equal("battle-restored", result.BattleId);
        Assert.Equal(9301ul, result.WorldId);
        Assert.Equal(143u, result.PlayerId);
    }

    [Fact]
    public async Task RoomGatewayFlowRestoreNoActiveRoomPreservesDiagnosticWithoutContinuing()
    {
        var roomClient = new ScriptedShooterRoomClient
        {
            RestoreHasActiveRoom = false,
            RestoreStatus = ShooterGatewayRoomRestoreStatus.NoActiveRoom,
            RestoreErrorCode = ShooterGatewayRoomRestoreErrorCode.NoAccountRoomMapping
        };
        var flow = new ShooterRoomGatewayFlow(roomClient);

        var result = await flow.RestoreRoomAsync(
            "session-token",
            "local",
            "dev",
            ShooterRoomLaunchSpec.CreateDefault("client-no-room"),
            playerId: 43u);

        Assert.Equal(new[] { "restore:local:dev" }, roomClient.Calls);
        Assert.Equal(ShooterGatewayRoomRestoreStatus.NoActiveRoom, result.RestoreStatus);
        Assert.Equal(ShooterGatewayRoomRestoreErrorCode.NoAccountRoomMapping, result.RestoreErrorCode);
        Assert.False(result.CanRetryRestore);
        Assert.False(result.Started);
        Assert.False(result.Subscribed);
    }

    [Fact]
    public async Task RoomGatewayFlowRestoreTimeoutReturnsRetryableDiagnostic()
    {
        var roomClient = new ScriptedShooterRoomClient
        {
            RestoreException = new TimeoutException("restore timeout")
        };
        var flow = new ShooterRoomGatewayFlow(roomClient);

        var result = await flow.RestoreRoomAsync(
            "session-token",
            "local",
            "dev",
            ShooterRoomLaunchSpec.CreateDefault("client-timeout"),
            playerId: 43u);

        Assert.Equal(ShooterGatewayRoomRestoreStatus.Timeout, result.RestoreStatus);
        Assert.Equal(ShooterGatewayRoomRestoreErrorCode.Timeout, result.RestoreErrorCode);
        Assert.True(result.CanRetryRestore);
        Assert.Contains("restore timeout", result.Message);
        Assert.False(result.Started);
        Assert.False(result.Subscribed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RoomGatewayFlowRestoresFromEachPreBattleStageWithoutRepeatingCompletedStages(
        int restorePhase)
    {
        var roomClient = new ScriptedShooterRoomClient
        {
            JoinBattleId = string.Empty,
            JoinWorldId = 0ul,
            JoinCurrentPlayerId = 144u,
            RestoreIsInBattle = false,
            RestoreSnapshotPhase = restorePhase,
            SnapshotLocalIsOwner = restorePhase == 0
        };
        var flow = new ShooterRoomGatewayFlow(roomClient);

        var result = await flow.RestoreRoomAsync(
            "session-token",
            "local",
            "dev",
            ShooterRoomLaunchSpec.CreateDefault("client-staged-restore"),
            playerId: 44u,
            eventEpoch: "epoch-staged",
            lastEventAck: 31L);

        var callIndex = 0;
        Assert.Equal("restore:local:dev", roomClient.Calls[callIndex++]);
        Assert.Equal("get-snapshot:room-1", roomClient.Calls[callIndex++]);
        if (restorePhase == 0)
        {
            Assert.Equal("ready:room-1:True", roomClient.Calls[callIndex++]);
            Assert.Equal("begin-loading:room-1", roomClient.Calls[callIndex++]);
        }
        if (restorePhase <= 1)
        {
            callIndex = AssertProgressSequence(roomClient.Calls, callIndex, "room-1");
            Assert.Equal("assets-loaded:room-1", roomClient.Calls[callIndex++]);
        }
        Assert.Equal("get-snapshot:room-1", roomClient.Calls[callIndex++]);
        Assert.Equal("subscribe:room-1:battle-1", roomClient.Calls[callIndex++]);
        Assert.Equal(callIndex, roomClient.Calls.Count);
        Assert.Equal("epoch-staged", roomClient.LastSubscribeRequest.EventEpoch);
        Assert.Equal(31L, roomClient.LastSubscribeRequest.LastEventAck);
        Assert.Equal("battle-1", result.BattleId);
        Assert.Equal(9001ul, result.WorldId);
        Assert.Equal(144u, result.PlayerId);
        Assert.True(result.Started);
        Assert.True(result.Subscribed);
    }

    [Fact]
    public async Task RoomOwnerWaitsForCanStartBeforeBeginningLoading()
    {
        var roomClient = new ScriptedShooterRoomClient
        {
            ReadyCanStart = false,
            PreAssetsSnapshotPhase = 0,
            PreAssetsSnapshotCanStart = true,
            SnapshotLocalIsOwner = true
        };
        var flow = new ShooterRoomGatewayFlow(roomClient);

        var result = await flow.CreateReadyStartAndSubscribeAsync(
            "session-token",
            ShooterRoomLaunchSpec.CreateDefault("owner-client"),
            playerId: 21u);

        Assert.Equal("ready:room-1:True", roomClient.Calls[2]);
        Assert.Equal("get-snapshot:room-1", roomClient.Calls[3]);
        Assert.Equal("begin-loading:room-1", roomClient.Calls[4]);
        Assert.True(result.Started);
        Assert.True(result.Subscribed);
    }

    [Fact]
    public async Task RoomGatewayFlowLateJoinsRunningBattleWithoutReadyOrStart()
    {
        var roomClient = new ScriptedShooterRoomClient
        {
            JoinKind = ShooterGatewayRoomJoinKind.LateJoin,
            JoinBattleId = "battle-mid",
            JoinWorldId = 9201ul,
            JoinServerNowTicks = 2123456L,
            JoinWorldStartAnchor = new ShooterGatewayWorldStartAnchor(123456L, 10000000L, 24, 1d / 30d),
            JoinCanStart = false,
            JoinCurrentPlayerId = 142u
        };
        var flow = new ShooterRoomGatewayFlow(roomClient);

        var result = await flow.JoinReadyStartAndSubscribeAsync(
            "session-token",
            "mid-room",
            ShooterRoomLaunchSpec.CreateDefault("client-late"),
            playerId: 42u);

        Assert.Equal(2, roomClient.Calls.Count);
        Assert.Equal("join:mid-room", roomClient.Calls[0]);
        Assert.Equal("subscribe:mid-room:battle-mid", roomClient.Calls[1]);
        Assert.Equal(ShooterRoomGatewayEntryKind.LateJoin, result.EntryKind);
        Assert.Equal("battle-mid", result.BattleId);
        Assert.Equal(9201ul, result.WorldId);
        Assert.Equal(2123456L, result.ServerNowTicks);
        Assert.Equal(30, result.TargetFrame);
        Assert.Equal(6, result.CatchUpFrames);
        Assert.False(result.CanStart);
        Assert.True(result.Started);
        Assert.True(result.Subscribed);
        Assert.Equal(142u, result.PlayerId);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, Path.Combine(segments));
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository source file.");
    }

    private static int AssertProgressSequence(
        IReadOnlyList<string> calls,
        int startIndex,
        string roomId)
    {
        var index = startIndex;
        var prefix = "loading-progress:" + roomId + ":";
        while (index < calls.Count && calls[index].StartsWith(prefix, StringComparison.Ordinal))
        {
            index++;
        }

        Assert.True(index > startIndex, "Expected at least one loading progress report.");
        Assert.Equal(prefix + "100", calls[index - 1]);
        return index;
    }
}
