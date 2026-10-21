using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

public sealed class ShieldStateSnapshotRouteTests
{
    [Fact]
    public void RoundTripsShieldStateAndUsesShieldOpcode()
    {
        var payload = MobaShieldStateSnapshotCodec.Serialize(new[]
        {
            new MobaShieldStateSnapshotEntry
            {
                ActorId = 7,
                CurrentShield = 80f,
                Capacity = 100f,
                PersonalShield = 50f,
                SharedShield = 30f,
                ActiveLayerCount = 2,
                HasShared = true,
            },
        });
        var snapshot = new WorldStateSnapshot(ShieldStateSnapshotRoute.OpCode, payload);

        Assert.True(ShieldStateSnapshotRoute.TryDecode(in snapshot, out var states));
        Assert.Single(states);
        Assert.Equal(4015, ShieldStateSnapshotRoute.OpCode);
        Assert.Equal(7, states[0].ActorId);
        Assert.Equal(80f, states[0].CurrentShield);
        Assert.True(states[0].HasShared);
    }

    [Fact]
    public void EmptyPayloadIsRejected()
    {
        var snapshot = new WorldStateSnapshot(ShieldStateSnapshotRoute.OpCode, null);
        Assert.False(ShieldStateSnapshotRoute.TryDecode(in snapshot, out var states));
        Assert.Empty(states);
    }

    [Fact]
    public void Shield_and_gameplay_snapshot_routes_register_without_opcode_collision()
    {
        using var dispatcher = new FrameSnapshotDispatcher();

        ShieldStateSnapshotRoute.RegisterDecoder(dispatcher);
        GameplayClockSnapshotRoute.RegisterDecoder(dispatcher);
        GameplayMatchSnapshotRoute.RegisterDecoder(dispatcher);
        GameplayRespawnSnapshotRoute.RegisterDecoder(dispatcher);
        CharacterActionSnapshotRoute.RegisterDecoder(dispatcher);

        Assert.NotEqual(ShieldStateSnapshotRoute.OpCode, GameplayMatchSnapshotRoute.OpCode);
        Assert.Equal(4015, ShieldStateSnapshotRoute.OpCode);
        Assert.Equal(4018, GameplayMatchSnapshotRoute.OpCode);
    }
}
