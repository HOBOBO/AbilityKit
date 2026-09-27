using AbilityKit.Demo.Tiny.Server;
using AbilityKit.Demo.Tiny.Turn;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Gameplay;
using AbilityKit.Orleans.Grains.Gameplays.Tiny;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class TinyTurnBattleIntegrationTests
{
    [Fact]
    public void TurnRulesRejectWrongPlayerAndPreserveTurnAcrossSnapshot()
    {
        var battle = new TinyTurnBattle();
        Assert.False(battle.Submit(2, new byte[] { 1 }));
        Assert.False(battle.Submit(1, new byte[] { 2 }));
        battle.Tick();
        Assert.Equal(0, battle.Turn);
        Assert.True(battle.Submit(1, new byte[] { 1 }));
        Assert.False(battle.Submit(1, new byte[] { 1 }));
        battle.Tick();
        Assert.Equal(2u, battle.CurrentPlayerId);
        Assert.Equal(1, battle.PlayerTwoHp);

        var replay = new TinyTurnBattle();
        replay.RestoreState(TinyTurnStateCodec.Decode(
            TinyTurnStateCodec.Encode(battle.CaptureState())));
        Assert.Equal(battle.ComputeHash(), replay.ComputeHash());
        Assert.True(battle.Submit(2, new byte[] { 1 }));
        Assert.True(replay.Submit(2, new byte[] { 1 }));
        battle.Tick();
        replay.Tick();
        Assert.Equal(battle.ComputeHash(), replay.ComputeHash());
        Assert.True(battle.Submit(1, new byte[] { 1 }));
        battle.Tick();
        Assert.Equal(1u, battle.WinnerId);
        Assert.Equal(0u, battle.CurrentPlayerId);
        Assert.False(battle.Submit(1, new byte[] { 1 }));
    }

    [Fact]
    public void TurnModuleRegistersIndependentRoomAndAuthoritativeResult()
    {
        var catalog = ServerGameplayModuleCatalog.Default
            .WithModule(TinyServerGameplayModule.Create())
            .WithModule(TinyTurnGameplayModule.Create());
        var module = catalog.ResolveModule(TinyTurnBattle.RoomType);
        Assert.Equal(TinyTurnBattle.RoomType, module.CreateRoomAdapter().RoomType);
        var room = new RoomSummary("dev", "local", "turn-test", TinyTurnBattle.RoomType,
            "Tiny Turn", false, 2, 0, "owner", 0, null);
        var roomAdapter = module.CreateRoomAdapter();
        var manifest = roomAdapter.BuildLaunchManifest(roomAdapter.CreateState(room), room);
        Assert.Contains("tiny:turn.rules.v1", manifest.AssetReferences);
        Assert.DoesNotContain("tiny:rules.v1", manifest.AssetReferences);
        Assert.Equal(TinyTurnBattle.WorldType,
            Assert.Single(module.CreateWorldBlueprints()).WorldType);
        Assert.Single(module.SyncProfile.Templates);

        using var manager = new ServerBattleWorldManager(NullLogger.Instance, catalog);
        using var session = module.CreateBattleRuntimeAdapter(manager).CreateSession("turn-test");
        Assert.True(session.Start(new BattleInitParams
        {
            WorldId = 47, TickRate = TinyTurnBattle.TickRate,
            Players = new List<PlayerInitInfo>
            {
                new() { PlayerId = 1 }, new() { PlayerId = 2 }
            }
        }).Succeeded);
        var owner = Command(1);
        var guest = Command(2);
        Assert.False(session.ValidateInput(guest).Accepted);
        Assert.Equal(1, session.SubmitInputs(1, new[] { owner }));
        Assert.True(session.Tick(1, TinyTurnBattle.TickRate, 0.1f));
        var first = session.CreateStateSyncPush(47, 1, false);
        Assert.True(first.IsFullSnapshot);
        Assert.Equal(TinyTurnBattle.SnapshotOpCode, first.PayloadOpCode);
        Assert.Equal(2u, TinyTurnStateCodec.Decode(first.Payload!).CurrentPlayerId);
        Assert.Equal(1, session.SubmitInputs(2, new[] { guest }));
        Assert.True(session.Tick(2, TinyTurnBattle.TickRate, 0.1f));
        Assert.Equal(1, session.SubmitInputs(3, new[] { owner }));
        Assert.True(session.Tick(3, TinyTurnBattle.TickRate, 0.1f));
        var result = session.GetSnapshot(3)!;
        Assert.True(result.MatchFinal);
        Assert.Equal(1, result.MatchState);
        Assert.False(session.ValidateInput(owner).Accepted);
    }

    [Fact]
    public void QueuedTurnInputIsConfirmedOnlyByAuthoritativeState()
    {
        using var manager = new ServerBattleWorldManager(NullLogger.Instance,
            ServerGameplayModuleCatalog.Default.WithModule(TinyTurnGameplayModule.Create()));
        using var session = TinyTurnGameplayModule.Create()
            .CreateBattleRuntimeAdapter(manager).CreateSession("queued-turn-test");
        Assert.True(session.Start(new BattleInitParams
        {
            WorldId = 48, TickRate = TinyTurnBattle.TickRate,
            Players = new List<PlayerInitInfo>
            {
                new() { PlayerId = 1 }, new() { PlayerId = 2 }
            }
        }).Succeeded);
        var owner = Command(1);
        Assert.True(session.ValidateInput(owner).Accepted);
        Assert.True(session.ValidateInput(owner).Accepted);
        Assert.Equal(1, session.SubmitInputs(1, new[] { owner, Command(1) }));
        Assert.True(session.Tick(1, TinyTurnBattle.TickRate, 0.1f));
        var state = TinyTurnStateCodec.Decode(session.CreateStateSyncPush(48, 1, true).Payload!);
        Assert.Equal(1, state.Turn);
        Assert.Equal(2u, state.CurrentPlayerId);
        Assert.False(session.ValidateInput(owner).Accepted);
    }

    private static BattleInputItem Command(uint playerId) => new()
    {
        PlayerId = playerId, OpCode = TinyTurnBattle.InputOpCode,
        Payload = new byte[] { 1 }
    };
}
