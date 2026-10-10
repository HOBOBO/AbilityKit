using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba.Share;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.SnapshotAdapter.Tests;

public sealed class GameplayRespawnSnapshotRouteTests
{
    [Fact]
    public void Respawn_state_round_trips_through_generic_snapshot_envelope()
    {
        var pending = new[] { new GameplayRespawnData(11, 5000), new GameplayRespawnData(12, 1200) };
        var snapshot = new WorldStateSnapshot(GameplayRespawnSnapshotCodec.OpCode,
            GameplayRespawnSnapshotCodec.Encode(pending));

        Assert.True(GameplayRespawnSnapshotRoute.TryDecode(in snapshot, out var decoded));
        Assert.Equal(2, decoded.Length);
        Assert.Equal(11, decoded[0].ActorId);
        Assert.Equal(1200, decoded[1].RemainingMilliseconds);
    }

    [Fact]
    public void Respawn_state_rejects_wrong_type_and_duplicate_actors()
    {
        var duplicate = new[] { new GameplayRespawnData(11, 5000), new GameplayRespawnData(11, 1200) };
        var payload = GameplayRespawnSnapshotCodec.Encode(duplicate);
        Assert.False(GameplayRespawnSnapshotRoute.TryDecode(new WorldStateSnapshot(1, payload), out _));
        Assert.False(GameplayRespawnSnapshotRoute.TryDecode(
            new WorldStateSnapshot(GameplayRespawnSnapshotRoute.OpCode, payload), out _));
    }
}
