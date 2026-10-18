using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class GatewayWireClientSessionSmokeTests
{
    [Fact]
    public async Task TwoHeadlessClientsConsumeGatewayWireAndOneRecoversIndependently()
    {
        using var owner = new BattleClientSessionDriver(new HeadlessHost());
        using var member = new BattleClientSessionDriver(new HeadlessHost());
        var ownerView = new SnapshotProbe();
        var memberView = new SnapshotProbe();
        owner.AttachProjection("owner", ownerView);
        member.AttachProjection("member", memberView);
        Assert.Equal(1, ownerView.ResetCount);
        Assert.Equal(1, memberView.ResetCount);
        await Task.WhenAll(owner.StartAsync(), member.StartAsync());

        var baseline = Decode(frame: 10, full: true, actorId: 101);
        owner.QueueSnapshot(in baseline);
        member.QueueSnapshot(in baseline);
        owner.Tick(1f / 30f);
        member.Tick(1f / 30f);
        Assert.Equal(10, ownerView.LastFrame);
        Assert.Equal(10, memberView.LastFrame);

        owner.NotifyConnectionLost();
        var delta = Decode(frame: 11, full: false, actorId: 101);
        owner.QueueSnapshot(in delta);
        member.QueueSnapshot(in delta);
        owner.Tick(1f / 30f);
        member.Tick(1f / 30f);
        Assert.Equal(10, ownerView.LastFrame);
        Assert.Equal(11, memberView.LastFrame);

        var recovery = Decode(frame: 12, full: true, actorId: 202);
        owner.QueueSnapshot(in recovery);
        owner.Tick(1f / 30f);
        Assert.Equal(12, ownerView.LastFrame);
        Assert.Equal(202, ownerView.LastActorId);
        Assert.Equal(BattleClientSessionStatus.Running, owner.Status);
        Assert.Equal(11, memberView.LastFrame);

        await Task.WhenAll(owner.StopAsync(), member.StopAsync());
        Assert.Equal(2, ownerView.ResetCount);
        Assert.Equal(2, memberView.ResetCount);
    }

    private static GatewayStateSyncSnapshot Decode(int frame, bool full, int actorId)
    {
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = 42,
            Frame = frame,
            IsFullSnapshot = full,
            SchemaVersion = GatewayStateSyncSnapshot.CurrentSchemaVersion,
            EventEpoch = "match-1",
            Actors =
            [
                new WireStateSyncActorSnapshot
                {
                    ActorId = actorId,
                    X = frame,
                    Hp = 100f,
                    HpMax = 100f,
                    TeamId = 1
                }
            ]
        };
        return GatewayStateSyncSnapshotDecoder.Decode(
            WireRoomGatewayBinary.Serialize(in wire));
    }

    private sealed class HeadlessHost : IBattleClientSessionHost
    {
        public Task StartAsync() => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public void Tick(float deltaTime) { }
    }

    private sealed class SnapshotProbe : IBattleClientProjection
    {
        public int LastFrame { get; private set; }
        public int LastActorId { get; private set; }
        public int ResetCount { get; private set; }

        public void Apply(in GatewayStateSyncSnapshot snapshot)
        {
            LastFrame = snapshot.Frame;
            LastActorId = Assert.Single(snapshot.Actors).ActorId;
        }

        public void Reset() => ResetCount++;
    }
}
