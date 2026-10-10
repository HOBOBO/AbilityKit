using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba.Share;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.SnapshotAdapter.Tests;

public sealed class GameplayClockSnapshotRouteTests
{
    [Fact]
    public void Clock_round_trips_through_generic_snapshot_envelope()
    {
        var clock = new GameplayClockData(1001, GameplayClockPhase.Running, 600000, 599001);
        var snapshot = new WorldStateSnapshot(GameplayClockSnapshotCodec.OpCode,
            GameplayClockSnapshotCodec.Encode(in clock));

        Assert.True(GameplayClockSnapshotRoute.TryDecode(in snapshot, out var decoded));
        Assert.Equal(clock.GameplayId, decoded.GameplayId);
        Assert.Equal(clock.Phase, decoded.Phase);
        Assert.Equal(clock.DurationMilliseconds, decoded.DurationMilliseconds);
        Assert.Equal(clock.RemainingMilliseconds, decoded.RemainingMilliseconds);
        Assert.True(decoded.IsVisible);
    }

    [Fact]
    public void Clock_rejects_wrong_type_and_malformed_payload()
    {
        var clock = new GameplayClockData(1001, GameplayClockPhase.Running, 600000, 599001);
        var payload = GameplayClockSnapshotCodec.Encode(in clock);
        Assert.False(GameplayClockSnapshotRoute.TryDecode(
            new WorldStateSnapshot(1, payload), out _));
        Assert.False(GameplayClockSnapshotRoute.TryDecode(
            new WorldStateSnapshot(GameplayClockSnapshotRoute.OpCode, new byte[2]), out _));

        payload[16] = 0xff;
        payload[17] = 0xff;
        payload[18] = 0xff;
        payload[19] = 0x7f;
        Assert.False(GameplayClockSnapshotRoute.TryDecode(
            new WorldStateSnapshot(GameplayClockSnapshotRoute.OpCode, payload), out _));
    }
}
