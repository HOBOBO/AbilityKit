using System.Collections.Generic;
using AbilityKit.Demo.Shooter;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Contracts.Shooter;
using AbilityKit.Orleans.Grains.Gameplays.Shooter.Rooms;
using AbilityKit.Orleans.Grains.Rooms.Gameplay;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Rooms;

public sealed class ShooterRoomGameplayAdapterTests
{
    [Fact]
    public void RegistryResolve_WhenRoomTypeIsShooter_ReturnsShooterAdapter()
    {
        var registry = new RoomGameplayRegistry();

        var adapter = registry.Resolve(ShooterGameplay.RoomType);

        Assert.IsType<ShooterRoomGameplayAdapter>(adapter);
    }

    [Fact]
    public void CanStart_RequiresConfiguredMinimumPlayers()
    {
        var adapter = new ShooterRoomGameplayAdapter();
        var summary = CreateSummary(new Dictionary<string, string>
        {
            [ShooterRoomTagKeys.MinPlayers] = "2"
        }, maxPlayers: 4);
        var state = adapter.CreateState(summary);
        adapter.Join(state, summary, new HashSet<string>(), "player-a");
        adapter.SetReady(state, new RoomReadyRequest("player-a", true));

        Assert.False(adapter.CanStart(state));

        adapter.Join(state, summary, new HashSet<string> { "player-a" }, "player-b");
        adapter.SetReady(state, new RoomReadyRequest("player-b", true));

        Assert.True(adapter.CanStart(state));
    }

    [Fact]
    public void BuildBattleInitParams_WhenShooterRoomReady_MapsRoomToBattleInit()
    {
        var adapter = new ShooterRoomGameplayAdapter();
        var summary = CreateSummary(new Dictionary<string, string>
        {
            [ShooterRoomTagKeys.TickRate] = "20",
            [ShooterRoomTagKeys.MapId] = "3",
            [ShooterRoomTagKeys.RandomSeed] = "1234",
            [ShooterRoomTagKeys.DurationFrames] = "3600",
            [ShooterRoomTagKeys.VictoryTargetDefeats] = "100000",
            [ShooterRoomTagKeys.ContinueAfterAllPlayersDefeated] = "true"
        });
        var state = adapter.CreateState(summary);
        adapter.Join(state, summary, new HashSet<string>(), "player-a");
        adapter.Join(state, summary, new HashSet<string> { "player-a" }, "player-b");
        adapter.SetReady(state, new RoomReadyRequest("player-a", true));
        adapter.SetReady(state, new RoomReadyRequest("player-b", true));

        var initParams = adapter.BuildBattleInitParams(
            state,
            summary,
            new StartRoomBattleRequest(
                "player-a",
                GameplayId: 0,
                RuleSetId: 7,
                ConfigVersion: 8,
                ProtocolVersion: 9,
                WorldType: null,
                ClientId: "client-a",
                SyncOptions: new BattleSyncStartOptions(
                    SyncTemplateId: ShooterServerProtocol.PredictRollbackAuthorityTemplate,
                    SyncModel: 2,
                    NetworkEnvironmentId: "ideal",
                    CarrierName: "DemoHarness",
                    EnableAuthoritativeWorld: true,
                    InterpolationEnabled: false,
                    InputDelayFrames: 3)));

        Assert.True(adapter.CanStart(state));
        Assert.Equal(ShooterGameplay.RoomType, initParams.RoomType);
        Assert.Equal(ShooterGameplay.WorldType, initParams.WorldType);
        Assert.Equal(ShooterGameplay.GameplayId, initParams.GameplayId);
        Assert.Equal(20, initParams.TickRate);
        Assert.Equal(3, initParams.MapId);
        Assert.Equal(1234, initParams.RandomSeed);
        Assert.Equal(3600, initParams.DurationFrames);
        Assert.Equal(100000, initParams.VictoryTargetDefeats);
        Assert.True(initParams.ContinueAfterAllPlayersDefeated);
        Assert.Equal(7, initParams.RuleSetId);
        Assert.Equal(8, initParams.ConfigVersion);
        Assert.Equal(9, initParams.ProtocolVersion);
        Assert.Equal("client-a", initParams.ClientId);
        Assert.Equal(3, initParams.InputDelayFrames);
        Assert.NotNull(initParams.SyncOptions);
        Assert.Equal(ShooterServerProtocol.PredictRollbackAuthorityTemplate, initParams.SyncOptions!.SyncTemplateId);
        Assert.Equal(2, initParams.SyncOptions.SyncModel);
        Assert.Equal("ideal", initParams.SyncOptions.NetworkEnvironmentId);
        Assert.Equal("DemoHarness", initParams.SyncOptions.CarrierName);
        Assert.True(initParams.SyncOptions.EnableAuthoritativeWorld);
        Assert.False(initParams.SyncOptions.InterpolationEnabled);
        Assert.Equal(3, initParams.SyncOptions.InputDelayFrames);
        Assert.NotEqual(0UL, initParams.WorldId);
        Assert.Collection(
            initParams.Players!,
            first =>
            {
                Assert.Equal(1U, first.PlayerId);
                Assert.Equal("player-a", first.AccountId);
                Assert.Equal(0f, first.PosX);
            },
            second =>
            {
                Assert.Equal(2U, second.PlayerId);
                Assert.Equal("player-b", second.AccountId);
                Assert.Equal(2f, second.PosX);
            });
    }

    [Fact]
    public void PlayerSlots_WhenEarlierMemberLeaves_DoNotShiftRemainingPlayers()
    {
        var adapter = new ShooterRoomGameplayAdapter();
        var summary = CreateSummary(maxPlayers: 3);
        var state = adapter.CreateState(summary);
        adapter.Join(state, summary, new HashSet<string>(), "player-a");
        adapter.Join(state, summary, new HashSet<string> { "player-a" }, "player-b");
        adapter.Join(state, summary, new HashSet<string> { "player-a", "player-b" }, "player-c");

        adapter.Leave(state, "player-a");

        var snapshots = adapter.BuildPlayerSnapshots(state);
        var initParams = adapter.BuildBattleInitParams(state, summary, CreateStartRequest());

        Assert.Collection(
            snapshots,
            first =>
            {
                Assert.Equal("player-b", first.AccountId);
                Assert.Equal(2U, first.PlayerId);
                Assert.Equal(2, first.HeroId);
                Assert.Equal(2, first.SpawnPointId);
            },
            second =>
            {
                Assert.Equal("player-c", second.AccountId);
                Assert.Equal(3U, second.PlayerId);
                Assert.Equal(3, second.HeroId);
                Assert.Equal(3, second.SpawnPointId);
            });
        Assert.Collection(
            initParams.Players!,
            first =>
            {
                Assert.Equal("player-b", first.AccountId);
                Assert.Equal(2U, first.PlayerId);
                Assert.Equal(2, first.ActorId);
                Assert.Equal(2f, first.PosX);
            },
            second =>
            {
                Assert.Equal("player-c", second.AccountId);
                Assert.Equal(3U, second.PlayerId);
                Assert.Equal(3, second.ActorId);
                Assert.Equal(4f, second.PosX);
            });
    }

    [Fact]
    public void PlayerSlots_WhenMemberJoinsAfterLeave_ReusesReleasedSlotForLateJoin()
    {
        var adapter = new ShooterRoomGameplayAdapter();
        var summary = CreateSummary(maxPlayers: 3);
        var state = adapter.CreateState(summary);
        adapter.Join(state, summary, new HashSet<string>(), "player-a");
        adapter.Join(state, summary, new HashSet<string> { "player-a" }, "player-b");
        adapter.Join(state, summary, new HashSet<string> { "player-a", "player-b" }, "player-c");

        adapter.Leave(state, "player-b");
        adapter.Join(state, summary, new HashSet<string> { "player-a", "player-c" }, "player-d");

        var snapshots = adapter.BuildPlayerSnapshots(state);
        var lateJoinPlayer = adapter.BuildLateJoinPlayer(state, summary, "player-d");

        Assert.Collection(
            snapshots,
            first =>
            {
                Assert.Equal("player-a", first.AccountId);
                Assert.Equal(1U, first.PlayerId);
            },
            second =>
            {
                Assert.Equal("player-d", second.AccountId);
                Assert.Equal(2U, second.PlayerId);
            },
            third =>
            {
                Assert.Equal("player-c", third.AccountId);
                Assert.Equal(3U, third.PlayerId);
            });
        Assert.NotNull(lateJoinPlayer);
        Assert.Equal("player-d", lateJoinPlayer!.AccountId);
        Assert.Equal(2U, lateJoinPlayer.PlayerId);
        Assert.Equal(2, lateJoinPlayer.ActorId);
        Assert.Equal(2f, lateJoinPlayer.PosX);
    }

    private static StartRoomBattleRequest CreateStartRequest()
    {
        return new StartRoomBattleRequest(
            "player-a",
            GameplayId: 0,
            RuleSetId: 0,
            ConfigVersion: 1,
            ProtocolVersion: 1,
            WorldType: null,
            ClientId: "client-a");
    }

    private static RoomSummary CreateSummary(Dictionary<string, string>? tags = null, int maxPlayers = 2)
    {
        return new RoomSummary(
            Region: "local",
            ServerId: "server-a",
            RoomId: "shooter-room-1",
            RoomType: ShooterGameplay.RoomType,
            Title: "Shooter Room",
            IsPublic: true,
            MaxPlayers: maxPlayers,
            PlayerCount: 0,
            OwnerAccountId: "player-a",
            CreatedAtUnixMs: 0,
            Tags: tags);
    }
}
