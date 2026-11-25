using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Handlers;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Orleans.Gateway.Tests;

public sealed class SubmitBattleInputHandlerTests
{
    [Fact]
    public void ConnectionGeneration_does_not_reuse_a_reconnected_session_number()
    {
        var original = new GatewaySessionContext(42);
        var reconnect = new GatewaySessionContext(42);

        Assert.Equal(original.SessionId, reconnect.SessionId);
        Assert.NotEqual(Guid.Empty, original.ConnectionGeneration);
        Assert.NotEqual(original.ConnectionGeneration, reconnect.ConnectionGeneration);
    }

    [Fact]
    public void ScopedInputRequiresStateSyncCapabilityAndAuthenticatedController()
    {
        var snapshot = CreateSnapshot();
        var moba = snapshot with { Summary = snapshot.Summary with { RoomType = GameplayRoomTypes.Moba } };
        var caps = new NetworkSyncCapabilityMetadata(1, "moba", 0, FixedBaselineStateSyncProtocol.SchemaVersion,
            0, (int)InputPolicy.ImmediateSubmit, 0, 0, 0, 0);
        var state = moba with { SyncCapabilities = caps };

        Assert.True(SubmitBattleInputHandler.CanUseScopedInput(state, "battle-a", 9001, "account-a", 1));
        Assert.False(SubmitBattleInputHandler.CanUseScopedInput(state, "battle-a", 9001, "account-a", 2));
        Assert.False(SubmitBattleInputHandler.CanUseScopedInput(moba, "battle-a", 9001, "account-a", 1));
        Assert.False(SubmitBattleInputHandler.CanUseScopedInput(snapshot with { SyncCapabilities = caps },
            "battle-a", 9001, "account-a", 1));
        Assert.False(SubmitBattleInputHandler.CanUseScopedInput(state with
        {
            SyncCapabilities = caps with { Input = (int)InputPolicy.DeterministicBroadcast }
        }, "battle-a", 9001, "account-a", 1));
    }

    [Fact]
    public void CanSubmitInput_accepts_matching_account_room_battle_world_and_player()
    {
        var snapshot = CreateSnapshot();

        Assert.True(SubmitBattleInputHandler.CanSubmitInput(snapshot, "battle-a", 9001ul, "account-a", 1u));
    }

    [Fact]
    public void CanSubmitInput_rejects_player_id_that_belongs_to_another_account()
    {
        var snapshot = CreateSnapshot();

        Assert.False(SubmitBattleInputHandler.CanSubmitInput(snapshot, "battle-a", 9001ul, "account-a", 2u));
    }

    [Theory]
    [InlineData("battle-other", 9001ul)]
    [InlineData("battle-a", 9002ul)]
    public void CanSubmitInput_rejects_mismatched_battle_or_world(string battleId, ulong worldId)
    {
        var snapshot = CreateSnapshot();

        Assert.False(SubmitBattleInputHandler.CanSubmitInput(snapshot, battleId, worldId, "account-a", 1u));
    }

    [Theory]
    [InlineData(null, 9001ul, "account-a", 1u)]
    [InlineData("battle-a", 0ul, "account-a", 1u)]
    [InlineData("battle-a", 9001ul, null, 1u)]
    [InlineData("battle-a", 9001ul, "account-a", 0u)]
    public void CanSubmitInput_rejects_missing_identity_fields(string? battleId, ulong worldId, string? accountId, uint playerId)
    {
        var snapshot = CreateSnapshot();

        Assert.False(SubmitBattleInputHandler.CanSubmitInput(snapshot, battleId, worldId, accountId, playerId));
    }

    private static RoomSnapshot CreateSnapshot()
    {
        return new RoomSnapshot(
            new RoomSummary("cn", "server-a", "room-a", "shooter", "Room A", true, 2, 2, "account-a", 1, null),
            new List<string> { "account-a", "account-b" },
            new List<RoomPlayerSnapshot>
            {
                new("account-a", 1, true, 1001, 1, 1, 1, 1, null, 1u),
                new("account-b", 1, true, 1002, 2, 1, 1, 1, null, 2u)
            },
            false,
            "battle-a",
            null,
            9001ul,
            new Dictionary<string, RoomMemberState>
            {
                ["account-a"] = new(true, 1, 0),
                ["account-b"] = new(true, 2, 0)
            });
    }
}
