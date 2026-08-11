using System;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Battle.Config;
using AbilityKit.Network.Client;
using AbilityKit.Network.Room;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Network.Client.Tests;

/// <summary>
/// Contract tests for <see cref="GatewayBattleClientHost.BuildBattleOptions"/> — the battle
/// data-plane assembly step (gateway + session identity + room-gateway preset pre-filled,
/// game callback applied on top).
/// </summary>
public sealed class GatewayBattleClientHostTests
{
    private static readonly GatewaySessionResult Session = new(
        sessionToken: "token-1",
        roomId: "room-1",
        battleId: "battle-1",
        numericRoomId: 42ul,
        playerId: 1u,
        roomSnapshot: default,
        subscribed: false);

    [Fact]
    public void BuildBattleOptions_PrefillsGatewaySessionAndProtocolPreset()
    {
        var options = GatewayBattleClientHost.BuildBattleOptions(
            in Session,
            "gw.example",
            41001,
            battleTransportFactory: null,
            configureBattle: (config, _) => config.WithInputSerializer(
                _ => default,
                _ => new NetworkSubmitInputResponse(true, 1, 0, false, "ok")));

        Assert.Equal("gw.example", options.Host);
        Assert.Equal(41001, options.Port);
        Assert.Equal("token-1", options.SessionToken);
        Assert.Equal((uint)RoomGatewayOpCodes.RenewSession, options.OpRenewSession);
        Assert.Equal((uint)RoomGatewayOpCodes.SubscribeStateSync, options.OpPostAuthentication);
        Assert.Equal((uint)RoomGatewayOpCodes.SubmitBattleInput, options.OpSubmitInput);
        Assert.NotNull(options.TransportFactory); // default TCP when no factory given
        Assert.NotNull(options.SerializeSubmitInput); // game callback survived
    }

    [Fact]
    public void BuildBattleOptions_SubscribePayloadCarriesSessionIdentity()
    {
        var options = GatewayBattleClientHost.BuildBattleOptions(
            in Session,
            "gw.example",
            41001,
            battleTransportFactory: null,
            configureBattle: (config, _) => config.WithInputSerializer(
                _ => default,
                _ => new NetworkSubmitInputResponse(true, 1, 0, false, "ok")));

        var payload = options.SerializePostAuthenticationWithReliableEventCursor!("epoch-1", 7L);
        var wire = WireRoomGatewayBinary.Deserialize<WireSubscribeStateSyncReq>(payload);

        Assert.Equal("token-1", wire.SessionToken);
        Assert.Equal("battle-1", wire.BattleId);
        Assert.Equal("room-1", wire.RoomId);
        Assert.Equal("epoch-1", wire.EventEpoch);
        Assert.Equal(7L, wire.LastEventAck);
    }

    [Fact]
    public void BuildBattleOptions_NullConfigure_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            GatewayBattleClientHost.BuildBattleOptions(in Session, "gw", 1, null, null!));
    }
}
