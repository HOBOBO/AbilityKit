using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Core;
using AbilityKit.Orleans.Gateway.Handlers;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using Xunit;

namespace AbilityKit.Orleans.Gateway.Tests;

public sealed class SubmitFrameInputHandlerTests
{
    [Fact]
    public void StateSyncSubscription_does_not_enable_frame_sync_subscription()
    {
        var parameterTypes = Assert.Single(typeof(SubscribeStateSyncHandler).GetConstructors())
            .GetParameters()
            .Select(parameter => parameter.ParameterType);

        Assert.DoesNotContain(typeof(GatewayFrameSyncSubscriptionManager), parameterTypes);
    }

    [Fact]
    public void FrameInput_submission_retains_frame_sync_subscription()
    {
        var parameterTypes = Assert.Single(typeof(SubmitFrameInputHandler).GetConstructors())
            .GetParameters()
            .Select(parameter => parameter.ParameterType);

        Assert.Contains(typeof(GatewayFrameSyncSubscriptionManager), parameterTypes);
    }

    private static readonly BattleInputSecurityOptions SecurityOptions = new()
    {
        MaxPayloadBytes = 4,
        MaxOpCode = 10
    };

    [Fact]
    public void IsValidRequest_accepts_valid_frame_input()
    {
        var request = new WireSubmitFrameInputReq(100, 200, 1, 0, 10, new byte[4]);

        Assert.True(SubmitFrameInputHandler.IsValidRequest(request, SecurityOptions));
    }

    [Theory]
    [InlineData(0ul, 200ul, 1u, 0, 1, 0)]
    [InlineData(100ul, 0ul, 1u, 0, 1, 0)]
    [InlineData(100ul, 200ul, 0u, 0, 1, 0)]
    [InlineData(100ul, 200ul, 1u, -1, 1, 0)]
    [InlineData(100ul, 200ul, 1u, 0, 0, 0)]
    [InlineData(100ul, 200ul, 1u, 0, 11, 0)]
    [InlineData(100ul, 200ul, 1u, 0, 1, 5)]
    public void IsValidRequest_rejects_invalid_identity_frame_opcode_or_payload(
        ulong roomId,
        ulong worldId,
        uint playerId,
        int frame,
        int opCode,
        int payloadLength)
    {
        var request = new WireSubmitFrameInputReq(
            roomId,
            worldId,
            playerId,
            frame,
            opCode,
            new byte[payloadLength]);

        Assert.False(SubmitFrameInputHandler.IsValidRequest(request, SecurityOptions));
    }

    [Fact]
    public void CanSubmitInput_accepts_matching_in_battle_member()
    {
        Assert.True(SubmitFrameInputHandler.CanSubmitInput(
            CreateSnapshot(),
            worldId: 200,
            accountId: "account-a",
            playerId: 1));
    }

    [Theory]
    [InlineData(RoomPhase.Lobby, 200ul, "account-a", 1u)]
    [InlineData(RoomPhase.InBattle, 201ul, "account-a", 1u)]
    [InlineData(RoomPhase.InBattle, 200ul, "account-a", 2u)]
    [InlineData(RoomPhase.InBattle, 200ul, "missing", 1u)]
    public void CanSubmitInput_rejects_wrong_phase_world_or_player(
        RoomPhase phase,
        ulong worldId,
        string accountId,
        uint playerId)
    {
        Assert.False(SubmitFrameInputHandler.CanSubmitInput(
            CreateSnapshot(phase),
            worldId,
            accountId,
            playerId));
    }

    [Fact]
    public void SerializeFrame_round_trips_authoritative_inputs()
    {
        var evt = new FramePushedEvent(
            100,
            200,
            12,
            new List<FrameInputItem>
            {
                new(1, 7, new byte[] { 1, 2 }),
                new(2, 8, Array.Empty<byte>())
            });

        var wire = WireCustomBinary.DeserializeFramePushedPush(
            GatewayFrameSyncSubscriptionManager.SerializeFrame(evt));

        Assert.Equal(100ul, wire.RoomId);
        Assert.Equal(200ul, wire.WorldId);
        Assert.Equal(12, wire.Frame);
        Assert.Equal(2, wire.Inputs.Length);
        Assert.Equal(1u, wire.Inputs[0].PlayerId);
        Assert.Equal(7, wire.Inputs[0].OpCode);
        Assert.Equal(new byte[] { 1, 2 }, wire.Inputs[0].Payload);
        Assert.Equal(2u, wire.Inputs[1].PlayerId);
        Assert.Empty(wire.Inputs[1].Payload);
    }

    [Fact]
    public void SerializeFrame_preserves_empty_authoritative_frame()
    {
        var evt = new FramePushedEvent(100, 200, 13, new List<FrameInputItem>());

        var wire = WireCustomBinary.DeserializeFramePushedPush(
            GatewayFrameSyncSubscriptionManager.SerializeFrame(evt));

        Assert.Equal(13, wire.Frame);
        Assert.Empty(wire.Inputs);
    }

    private static RoomSnapshot CreateSnapshot(RoomPhase phase = RoomPhase.InBattle)
    {
        return new RoomSnapshot(
            new RoomSummary("cn", "server-a", "room-a", "moba", "Room A", true, 2, 2, "account-a", 1, null),
            new List<string> { "account-a", "account-b" },
            new List<RoomPlayerSnapshot>
            {
                new("account-a", 1, true, 1001, 1, 1, 1, 1, null, 1),
                new("account-b", 2, true, 1002, 2, 1, 1, 1, null, 2)
            },
            false,
            "battle-a",
            null,
            200,
            null,
            Phase: phase);
    }
}
