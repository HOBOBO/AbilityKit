using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Adapter.Tests;

public sealed class BattlePresentationCuePlacementResolverTests
{
    [Fact]
    public void ExplicitPositionWinsWhileFollowTargetUsesActorPriority()
    {
        var actors = new ActorPositions();
        actors.Positions[3] = new SnapshotVec3(30, 0, 0);
        actors.Positions[2] = new SnapshotVec3(20, 0, 0);
        var request = Request(true, new SnapshotVec3(4, 5, 6));

        var position = BattlePresentationCuePlacementResolver.ResolvePosition(in request, actors);
        var follow = BattlePresentationCuePlacementResolver.ResolveFollowActorId(in request, actors);

        Assert.Equal(5, position.X);
        Assert.Equal(5, position.Y);
        Assert.Equal(6, position.Z);
        Assert.Equal(2, follow);
    }

    [Fact]
    public void MissingTargetPositionFallsBackThroughFirstTargetAndSource()
    {
        var actors = new ActorPositions();
        actors.Positions[3] = new SnapshotVec3(30, 0, 0);
        var request = Request(false, default);

        var position = BattlePresentationCuePlacementResolver.ResolvePosition(in request, actors);

        Assert.Equal(31, position.X);
        actors.Positions[2] = new SnapshotVec3(20, 0, 0);
        position = BattlePresentationCuePlacementResolver.ResolvePosition(in request, actors);
        Assert.Equal(21, position.X);
    }

    private static BattlePresentationCueSpawnRequest Request(bool explicitPosition, SnapshotVec3 position) =>
        new(BattlePresentationCueRequestKey.FromExternal("cue"), 9, 3, 1, 2,
            explicitPosition, position, new SnapshotVec3(1, 0, 0), 0, 1, 1);

    private sealed class ActorPositions : IPresentationActorPlacementSource
    {
        public Dictionary<int, SnapshotVec3> Positions { get; } = [];
        public bool TryGetPosition(int actorId, out SnapshotVec3 position) => Positions.TryGetValue(actorId, out position);
        public bool HasActor(int actorId) => Positions.ContainsKey(actorId);
    }
}
