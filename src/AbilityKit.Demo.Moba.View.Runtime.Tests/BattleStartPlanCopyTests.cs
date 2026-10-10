using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Protocol.Moba;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class BattleStartPlanCopyTests
{
    [Fact]
    public void GroupedLaunchSpec_RequiresMatchingWorldIdentity()
    {
        var identity = new MobaBattleLaunchIdentity("battle-1", "match-1", "world-1");
        var createWorld = new MobaCreateWorldSpec("match-1", 1, 42, 30, 0,
            Array.Empty<MobaPlayerLoadout>(), 1);
        var profile = MobaBattleLaunchProfile.Create("client-1",
            MobaBattleLaunchMode.RoomFlow, MobaBattleLaunchSyncMode.FrameSync,
            MobaBattleLaunchAuthorityMode.ServerAuthority);
        var spec = new MobaBattleLaunchSpec(in identity, new PlayerId("1"),
            in createWorld, in profile);

        Assert.Equal("world-1", spec.WorldId);
        Assert.Equal("match-1", spec.MatchId);
        Assert.Equal(42, spec.RandomSeed);

        var mismatched = new MobaCreateWorldSpec("other-match", 1, 42, 30, 0,
            Array.Empty<MobaPlayerLoadout>(), 1);
        Assert.Throws<ArgumentException>(() => new MobaBattleLaunchSpec(
            in identity, new PlayerId("1"), in mismatched, in profile));
    }

    [Fact]
    public void GroupedOptionsAndCopies_PreserveCheckpointAndGatewayCapacity()
    {
        var source = BattleStartPlanBuilder
            .ForWorld("world-1", "battle", "client-1", "1", 30, 0)
            .WithGateway(false, "127.0.0.1", 4000, 12, "old-token", "dev", "local",
                false, false, "room-1", 110, 111, maxPlayers: 5)
            .Build();
        var checkpoint = new MobaReliableBattleEventCheckpoint("battle-1", "epoch-1", 9);
        var options = new BattleStartPlanOptions(
            in source.World, source.HostMode, in source.Sync, in source.Authority,
            in source.Gateway, in source.Auto, in source.RunModeOptions,
            in source.CreateWorld, in source.TimeSync, source.LaunchSpec, checkpoint);
        var plan = new BattleStartPlan(in options);

        Assert.Equal(9, plan.ReliableEventCheckpoint.LastAcknowledgedSequence);
        Assert.Equal(5, plan.Gateway.MaxPlayers);

        var withToken = plan.WithGatewaySessionToken("new-token");
        var withRoom = withToken.WithGatewayRoom("world-2", 42);
        var replay = withRoom.WithInputReplay("recording.bin");
        foreach (var copy in new[] { withToken, withRoom, replay })
        {
            Assert.Equal(9, copy.ReliableEventCheckpoint.LastAcknowledgedSequence);
            Assert.Equal(5, copy.Gateway.MaxPlayers);
        }
        Assert.Equal("new-token", withToken.Gateway.SessionToken);
        Assert.Equal((ulong)42, withRoom.Gateway.NumericRoomId);
        Assert.Equal(BattleRunMode.Replay, replay.RunModeOptions.RunMode);
    }
}
