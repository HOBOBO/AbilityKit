using System.Collections.Generic;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Rooms;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Rooms;

public sealed class RoomBattleInitSpecHasherTests
{
    [Fact]
    public void Compute_ReturnsSameHash_ForIdenticalParams()
    {
        var a = CreateParams(worldId: 100, tickRate: 20, mapId: 1);
        var b = CreateParams(worldId: 100, tickRate: 20, mapId: 1);

        var hashA = RoomBattleInitSpecHasher.Compute(a);
        var hashB = RoomBattleInitSpecHasher.Compute(b);

        Assert.Equal(hashA, hashB);
        Assert.False(string.IsNullOrEmpty(hashA));
    }

    [Fact]
    public void Compute_ReturnsDifferentHash_ForDifferentScalarFields()
    {
        var a = CreateParams(worldId: 100, tickRate: 20, mapId: 1);
        var b = CreateParams(worldId: 200, tickRate: 20, mapId: 1);

        var hashA = RoomBattleInitSpecHasher.Compute(a);
        var hashB = RoomBattleInitSpecHasher.Compute(b);

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void Compute_ReturnsDifferentHash_WhenEnemyBudgetChanges()
    {
        var a = CreateParams();
        var b = CreateParams();
        a.EnemyBudget = 512;
        b.EnemyBudget = 2048;

        Assert.NotEqual(RoomBattleInitSpecHasher.Compute(a), RoomBattleInitSpecHasher.Compute(b));
    }

    [Fact]
    public void Compute_ReturnsDifferentHash_WhenContinueAfterAllPlayersDefeatedChanges()
    {
        var a = CreateParams();
        var b = CreateParams();
        b.ContinueAfterAllPlayersDefeated = true;

        Assert.NotEqual(RoomBattleInitSpecHasher.Compute(a), RoomBattleInitSpecHasher.Compute(b));
    }

    [Fact]
    public void Compute_ReturnsSameHash_RegardlessOfPlayerOrder()
    {
        var player1 = CreatePlayer(1, "acc-1");
        var player2 = CreatePlayer(2, "acc-2");

        var a = CreateParams(players: new List<PlayerInitInfo> { player1, player2 });
        var b = CreateParams(players: new List<PlayerInitInfo> { player2, player1 });

        var hashA = RoomBattleInitSpecHasher.Compute(a);
        var hashB = RoomBattleInitSpecHasher.Compute(b);

        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public void Compute_IgnoresWorldStartAnchor()
    {
        var a = CreateParams();
        a.WorldStartAnchor = new WorldStartAnchor(1000, 60, 0, 0.0166);

        var b = CreateParams();
        b.WorldStartAnchor = new WorldStartAnchor(9999, 30, 5, 0.0333);

        Assert.Equal(RoomBattleInitSpecHasher.Compute(a), RoomBattleInitSpecHasher.Compute(b));
    }

    [Fact]
    public void Compute_ReturnsDifferentHash_WhenSpawnContractChanges()
    {
        var playerA = CreatePlayer(1, "acc-1");
        var playerB = CreatePlayer(1, "acc-1");
        playerB.SpawnIndex = 1;

        var a = CreateParams(players: new List<PlayerInitInfo> { playerA });
        var b = CreateParams(players: new List<PlayerInitInfo> { playerB });

        Assert.NotEqual(RoomBattleInitSpecHasher.Compute(a), RoomBattleInitSpecHasher.Compute(b));
    }

    [Fact]
    public void Compute_ReturnsEmpty_ForNullParams()
    {
        Assert.Equal(string.Empty, RoomBattleInitSpecHasher.Compute(null!));
    }

    private static BattleInitParams CreateParams(
        ulong worldId = 1,
        int tickRate = 20,
        int mapId = 1,
        List<PlayerInitInfo>? players = null)
    {
        return new BattleInitParams
        {
            WorldId = worldId,
            TickRate = tickRate,
            MapId = mapId,
            GameplayId = 0,
            RuleSetId = 0,
            ConfigVersion = 1,
            ProtocolVersion = 1,
            RandomSeed = 42,
            InputDelayFrames = 0,
            DurationFrames = 72000,
            WorldType = "moba",
            ClientId = "client-1",
            RoomType = "moba",
            Players = players ?? new List<PlayerInitInfo>()
        };
    }

    private static PlayerInitInfo CreatePlayer(uint playerId, string accountId)
    {
        return new PlayerInitInfo
        {
            PlayerId = playerId,
            ActorId = (int)playerId,
            HeroId = 100 + (int)playerId,
            TeamId = playerId % 2 == 0 ? 2 : 1,
            Level = 1,
            PosX = playerId * 1.0f,
            PosY = 0f,
            PosZ = playerId * 2.0f,
            AccountId = accountId,
            SkillIds = new List<int> { 1, 2, 3 }
        };
    }
}
