using System.Text;
using AbilityKit.Orleans.Contracts.Accounts;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Handlers;
using Xunit;

namespace AbilityKit.Orleans.Gateway.Tests;

public sealed class JoinRoomHandlerTests
{
    [Fact]
    public void JoinRoom_flow_contract_should_preserve_account_and_room_identity()
    {
        var accountId = "account-a";
        var login = new CreateSessionForAccountResponse("session-a", 3600, null);
        var validation = new ValidateSessionResponse(true, accountId, login.ExpireAtUnixMs);
        var request = new AbilityKit.Orleans.Contracts.Rooms.JoinRoomRequest(
            validation.AccountId!,
            "cn",
            "server-a",
            "room-a");

        Assert.True(validation.IsValid);
        Assert.Equal("account-a", request.AccountId);
        Assert.Equal("cn", request.Region);
        Assert.Equal("server-a", request.ServerId);
        Assert.Equal("room-a", request.RoomId);
    }

    [Fact]
    public void Gateway_error_mapper_should_preserve_classified_room_failure_message()
    {
        var response = RoomGatewayErrorMapper.ToResponse(7, new InvalidOperationException("Room is full."));

        Assert.Equal(7u, response.Seq);
        Assert.Equal(GatewayStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Room is full.", Encoding.UTF8.GetString(response.Payload));
    }

    [Fact]
    public void Gateway_error_mapper_should_hide_unknown_exception_details()
    {
        var response = RoomGatewayErrorMapper.ToResponse(8, new Exception("database secret"));

        Assert.Equal(GatewayStatusCode.InternalError, response.StatusCode);
        Assert.Equal("Room operation failed.", Encoding.UTF8.GetString(response.Payload));
    }
}
