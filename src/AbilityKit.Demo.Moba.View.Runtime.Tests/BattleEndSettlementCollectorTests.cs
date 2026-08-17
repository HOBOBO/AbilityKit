using AbilityKit.Game.Battle.Presentation.Features.Settlement;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class BattleEndSettlementCollectorTests
{
    [Fact]
    public void BuildClampsNegativeDurationAndHandlesInvalidTickRate()
    {
        var collector = new BattleEndSettlementCollector();

        var projection = collector.Build(startFrame: 120, lastFrame: 90, tickRate: 0);

        Assert.Equal(0, projection.MatchDurationFrames);
        Assert.Equal(0, projection.MatchDurationSeconds);
    }

    [Fact]
    public void BuildConvertsFramesUsingIntegerTickRate()
    {
        var collector = new BattleEndSettlementCollector();

        var projection = collector.Build(startFrame: 10, lastFrame: 135, tickRate: 30);

        Assert.Equal(125, projection.MatchDurationFrames);
        Assert.Equal(4, projection.MatchDurationSeconds);
    }

    [Fact]
    public void BuildProjectsPlayersAndLocalVictorySemantics()
    {
        var collector = new BattleEndSettlementCollector();
        var remote = new BattleEndPlayerProjectionInput(
            "remote",
            teamId: 2,
            heroId: 202,
            isLocalPlayer: false,
            finalHp: 500,
            maxHp: 1000,
            isAlive: true);
        var local = new BattleEndPlayerProjectionInput(
            "local",
            teamId: 1,
            heroId: 101,
            isLocalPlayer: true,
            finalHp: 250,
            maxHp: 1200,
            isAlive: true);
        collector.AddPlayer(in remote);
        collector.AddPlayer(in local);

        var projection = collector.Build(startFrame: 0, lastFrame: 90);

        Assert.Equal(1, projection.WinningTeamId);
        Assert.True(projection.LocalPlayerVictory);
        Assert.Collection(
            projection.Players,
            player =>
            {
                Assert.Equal("remote", player.PlayerId);
                Assert.Equal(2, player.TeamId);
                Assert.Equal(202, player.HeroId);
                Assert.False(player.IsLocalPlayer);
            },
            player =>
            {
                Assert.Equal("local", player.PlayerId);
                Assert.Equal(1, player.TeamId);
                Assert.Equal(101, player.HeroId);
                Assert.Equal(250, player.FinalHp);
                Assert.Equal(1200, player.MaxHp);
                Assert.True(player.IsAlive);
            });
    }

    [Fact]
    public void DeadLocalPlayerIsNotReportedAsVictorious()
    {
        var collector = new BattleEndSettlementCollector();
        var local = new BattleEndPlayerProjectionInput(
            "local",
            teamId: 3,
            heroId: 101,
            isLocalPlayer: true,
            finalHp: 0,
            maxHp: 1000,
            isAlive: false);
        collector.AddPlayer(in local);

        var projection = collector.Build(startFrame: 0, lastFrame: 30);

        Assert.Equal(3, projection.WinningTeamId);
        Assert.False(projection.LocalPlayerVictory);
    }

    [Fact]
    public void BuiltProjectionOwnsPlayerSnapshotAcrossCollectorReuse()
    {
        var collector = new BattleEndSettlementCollector();
        var firstPlayer = new BattleEndPlayerProjectionInput(
            "first",
            teamId: 1,
            heroId: 101,
            isLocalPlayer: true,
            finalHp: 100,
            maxHp: 100,
            isAlive: true);
        collector.AddPlayer(in firstPlayer);
        var firstProjection = collector.Build(startFrame: 0, lastFrame: 30);

        collector.Reset();
        var secondPlayer = new BattleEndPlayerProjectionInput(
            "second",
            teamId: 2,
            heroId: 202,
            isLocalPlayer: true,
            finalHp: 0,
            maxHp: 100,
            isAlive: false);
        collector.AddPlayer(in secondPlayer);
        var secondProjection = collector.Build(startFrame: 30, lastFrame: 60);

        Assert.Single(firstProjection.Players);
        Assert.Equal("first", firstProjection.Players[0].PlayerId);
        Assert.Single(secondProjection.Players);
        Assert.Equal("second", secondProjection.Players[0].PlayerId);
    }
}
