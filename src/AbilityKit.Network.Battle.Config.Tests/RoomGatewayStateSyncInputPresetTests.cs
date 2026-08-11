using System;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Battle.Config;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Network.Battle.Config.Tests;

/// <summary>
/// Contract tests for <see cref="NetworkBattleConfig.UseRoomGatewayStateSyncInput"/> — the shared
/// room-gateway StateSync input uplink preset consumed by all three demos (moba/shooter/console).
/// </summary>
public sealed class RoomGatewayStateSyncInputPresetTests
{
    private static NetworkTransportOptions BuildOptions(
        Func<WireSubmitBattleInputRes, bool> retryPolicy = null,
        string sessionToken = "token-1")
    {
        return new NetworkBattleConfig()
            .WithGateway("test", 9999)
            .WithTransportFactory(() => throw new NotImplementedException())
            .WithSession(sessionToken, "battle-1", "room-1")
            .UseRoomGatewayProtocol("battle-1", "room-1")
            .UseRoomGatewayStateSyncInput(
                "battle-1",
                playerIdToUInt: p => uint.Parse(p.Value),
                worldIdToUlong: w => ulong.Parse(w.Value),
                retryAtAuthoritativeFrame: retryPolicy)
            .WithSnapshotDeserializer(_ => new object())
            .Build();
    }

    private static SubmitInputRequest SampleInput()
    {
        return new SubmitInputRequest(
            new AbilityKit.Ability.World.Abstractions.WorldId("987654321"),
            new AbilityKit.Ability.Host.PlayerInputCommand(
                new AbilityKit.Ability.FrameSync.FrameIndex(7),
                new AbilityKit.Ability.Host.PlayerId("42"),
                1001,
                new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public void Serialize_ProducesWireSubmitBattleInputReq_WithAllFieldsMapped()
    {
        var options = BuildOptions();

        var prepared = options.PrepareSubmitInput!(SampleInput());
        var payload = options.SerializeSubmitInput!(prepared);
        var wire = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputReq>(payload);

        Assert.Equal("token-1", wire.SessionToken);
        Assert.Equal("battle-1", wire.BattleId);
        Assert.Equal(987654321ul, wire.WorldId);
        Assert.Equal(7, wire.Frame);
        Assert.Equal(42u, wire.PlayerId);
        Assert.Equal(1001, wire.InputOpCode);
        Assert.Equal(new byte[] { 1, 2, 3 }, wire.Payload);
        Assert.Equal(1ul, wire.CommandSequence);
    }

    [Fact]
    public void DeserializeResponse_MapsAllFields_IncludingExtendedPerSubmitData()
    {
        var options = BuildOptions();
        var wire = new WireSubmitBattleInputRes
        {
            Success = true,
            AcceptedFrame = 11,
            Message = "ok",
            CurrentFrame = 12,
            Status = "Accepted",
            ShouldResync = true,
            ServerTicks = 123456789L
        };
        var payload = WireRoomGatewayBinary.Serialize(in wire);

        var response = options.DeserializeSubmitInputResponse!(payload);

        Assert.True(response.Accepted);
        Assert.Equal(12, response.ServerFrame);
        Assert.Equal(0, response.ReasonCode);
        Assert.Equal("Accepted", response.Status);
        Assert.Equal("ok", response.Message);
        Assert.Equal(11, response.AcceptedFrame);
        Assert.Equal(123456789L, response.ServerTicks);
        Assert.True(response.ShouldResync);
    }

    [Fact]
    public void DeserializeResponse_DefaultRetryPolicy_IsNeverRetry()
    {
        var options = BuildOptions();
        var wire = new WireSubmitBattleInputRes { Success = false, ShouldResync = true };
        var payload = WireRoomGatewayBinary.Serialize(in wire);

        var response = options.DeserializeSubmitInputResponse!(payload);

        Assert.False(response.RetryAtAuthoritativeFrame);
        Assert.Equal(1, response.ReasonCode);
    }

    [Fact]
    public void DeserializeResponse_CustomRetryPolicy_IsHonored()
    {
        var options = BuildOptions(retryPolicy: wire => wire.ShouldResync);
        var wire = new WireSubmitBattleInputRes { Success = false, ShouldResync = true };
        var payload = WireRoomGatewayBinary.Serialize(in wire);

        Assert.True(options.DeserializeSubmitInputResponse!(payload).RetryAtAuthoritativeFrame);
    }

    [Fact]
    public void Preset_RequiresBattleIdAndConverters()
    {
        var config = new NetworkBattleConfig();
        Assert.Throws<ArgumentException>(() =>
            config.UseRoomGatewayStateSyncInput("", _ => 0u, _ => 0ul));
        Assert.Throws<ArgumentNullException>(() =>
            config.UseRoomGatewayStateSyncInput("battle-1", null, _ => 0ul));
        Assert.Throws<ArgumentNullException>(() =>
            config.UseRoomGatewayStateSyncInput("battle-1", _ => 0u, null));
    }
}
