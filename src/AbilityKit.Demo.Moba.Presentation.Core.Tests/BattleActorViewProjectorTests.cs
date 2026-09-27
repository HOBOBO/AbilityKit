using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Demo.Moba.Presentation.GodotSample;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Core.Tests;

public sealed class BattleActorViewProjectorTests
{
    [Fact]
    public void FullSnapshotUpsertsPresentActorsAndRemovesOnlyMissingRemoteActors()
    {
        var port = new RecordingPort();
        port.Actors[1] = Actor(1, 0);
        port.Actors[2] = Actor(2, 0);
        port.Actors[3] = Actor(3, 0);
        var snapshot = new GatewayStateSyncSnapshot(7, 12, 0, true, [Actor(1, 10), Actor(2, 20)]);

        var batch = BattleActorViewProjector.Apply(port, in snapshot, excludedActorId: 1);

        Assert.Equal(7UL, batch.WorldId);
        Assert.Equal(12, batch.Frame);
        Assert.Collection(batch.Commands,
            command => { Assert.Equal(BattleActorViewCommandKind.Upsert, command.Kind); Assert.Equal(2, command.ActorId); },
            command => { Assert.Equal(BattleActorViewCommandKind.Remove, command.Kind); Assert.Equal(3, command.ActorId); });
        Assert.Equal(0, port.Actors[1].X);
        Assert.Equal(20, port.Actors[2].X);
        Assert.False(port.Actors.ContainsKey(3));
    }

    [Fact]
    public void DeltaRemovesExplicitActorsButKeepsOmittedActors()
    {
        var port = new RecordingPort();
        port.Actors[2] = Actor(2, 0);
        port.Actors[3] = Actor(3, 0);
        port.Actors[4] = Actor(4, 0);
        var snapshot = new GatewayStateSyncSnapshot(7, 13, 0, false,
            [Actor(2, 25)], removedActorIds: [3, 3, 2]);

        var batch = BattleActorViewProjector.Apply(port, in snapshot, excludedActorId: 0);

        Assert.Equal(2, batch.Commands.Count);
        Assert.Equal(25, port.Actors[2].X);
        Assert.False(port.Actors.ContainsKey(3));
        Assert.True(port.Actors.ContainsKey(4));
    }

    [Fact]
    public void SameFixtureCanDriveIndependentWorldPorts()
    {
        var primary = new RecordingPort();
        var prediction = new RecordingPort();
        var initial = PresentationFixture.Initial();
        BattleActorViewProjector.Apply(primary, in initial, 0);
        BattleActorViewProjector.Apply(prediction, in initial, 0);

        var delta = PresentationFixture.Delta();
        BattleActorViewProjector.Apply(prediction, in delta, 0);

        Assert.Equal(2, primary.Actors.Count);
        Assert.Equal(2, primary.Actors[202].X);
        Assert.Single(prediction.Actors);
        Assert.Equal(-1, prediction.Actors[101].X);
    }

    [Fact]
    public void ReusableWorkspaceDoesNotCarryCommandsIntoTheNextFrame()
    {
        var port = new RecordingPort();
        var workspace = new BattleActorViewProjectionWorkspace();
        var full = new GatewayStateSyncSnapshot(7, 1, 0, true, [Actor(1, 10), Actor(2, 20)]);
        BattleActorViewProjector.ApplyWithoutBatch(port, in full, 0, workspace);

        var delta = new GatewayStateSyncSnapshot(7, 2, 0, false, [Actor(1, 11)], removedActorIds: [2]);
        BattleActorViewProjector.ApplyWithoutBatch(port, in delta, 0, workspace);
        BattleActorViewProjector.ApplyWithoutBatch(port, in delta, 0, workspace);

        Assert.Single(port.Actors);
        Assert.Equal(11, port.Actors[1].X);
        Assert.False(port.Actors.ContainsKey(2));
    }

    private static GatewayStateSyncActorSnapshot Actor(int id, float x) =>
        new(id, x, 0, 0, 0, 0, 0, 100, 100, 1);

    private sealed class RecordingPort : IBattleActorViewPort
    {
        public Dictionary<int, GatewayStateSyncActorSnapshot> Actors { get; } = [];
        public IEnumerable<int> GetActorIds() => Actors.Keys.ToArray();
        public void Upsert(in GatewayStateSyncActorSnapshot actor) => Actors[actor.ActorId] = actor;
        public void Remove(int actorId) => Actors.Remove(actorId);
    }
}
