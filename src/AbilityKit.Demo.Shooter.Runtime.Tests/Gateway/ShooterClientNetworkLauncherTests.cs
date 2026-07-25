using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.GameFramework.Network;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using GameFramework.Network;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterClientNetworkLauncherTests
{
    [Fact]
    public async Task ClientNetworkLauncherOpensConnectionLaunchesRoomAndDispatchesPushes()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var connection = new FakeGatewayConnection { AutoRespondRoomGateway = true, JoinCurrentPlayerId = 51u };
        connection.Close();
        using var launcher = new ShooterClientNetworkLauncher(connection);
        var start = new ShooterStartGamePayload(
            "network-launch-session",
            30,
            4904,
            new[]
            {
                new ShooterStartPlayer(51, "P51", 0f, 0f),
                new ShooterStartPlayer(52, "P52", 5f, 0f)
            });

        var launched = await launcher.CreateReadyStartAndSubscribeAsync(
            "127.0.0.1",
            17001,
            runtime,
            presentation,
            start,
            "session-token",
            ShooterRoomLaunchSpec.CreateDefault("client-network"),
            playerId: 51u);

        Assert.True(launcher.IsConnected);
        Assert.True(connection.IsConnected);
        Assert.Equal("127.0.0.1", connection.OpenHost);
        Assert.Equal(17001, connection.OpenPort);
        Assert.True(launched.Session.IsStarted);
        Assert.True(launched.Session.HasGateway);
        Assert.Equal(launched.Session, launched.Battle.Session);
        Assert.Equal("battle-launch", launched.Battle.BattleId);
        Assert.Equal(launcher.GatewayConnection, launched.GatewayConnection);
        Assert.Equal(7, connection.SentOpCodes.Count);
        Assert.Equal(RoomGatewayOpCodes.CreateRoom, connection.SentOpCodes[0]);
        Assert.Equal(RoomGatewayOpCodes.JoinRoom, connection.SentOpCodes[1]);
        Assert.Equal(RoomGatewayOpCodes.SetReady, connection.SentOpCodes[2]);
        Assert.Equal(RoomGatewayOpCodes.BeginLoading, connection.SentOpCodes[3]);
        Assert.Equal(RoomGatewayOpCodes.ReportAssetsLoaded, connection.SentOpCodes[4]);
        Assert.Equal(RoomGatewayOpCodes.GetSnapshot, connection.SentOpCodes[5]);
        Assert.Equal(RoomGatewayOpCodes.SubscribeStateSync, connection.SentOpCodes[6]);

        launcher.Tick(1f / 30f);
        var submit = await launched.Battle.SubmitLocalInputToGatewayAsync(moveX: 1f, moveY: 0f, aimX: 1f, aimY: 0f, fire: true);

        Assert.Equal(1, connection.TickCount);
        Assert.True(submit.Remote.Success);
        Assert.Equal(RoomGatewayOpCodes.SubmitBattleInput, connection.SentOpCodes[7]);
        var inputWire = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputReq>(connection.LastSentPayload);
        Assert.Equal("battle-launch", inputWire.BattleId);
        Assert.Equal(9041ul, inputWire.WorldId);
        Assert.Equal(51u, inputWire.PlayerId);

        var authority = new ShooterBattleRuntimePort();
        Assert.True(authority.StartGame(in start));
        authority.SubmitInput(0, new[] { new ShooterPlayerCommand(51, 0f, 1f, 1f, 0f, true) });
        Assert.True(authority.Tick(1f / 30f));
        var packed = authority.ExportPackedSnapshot(9041ul, isFullSnapshot: true, authorityOverride: true);
        var push = new WireStateSyncSnapshotPush
        {
            WorldId = packed.WorldId,
            Frame = packed.Frame,
            Timestamp = 4904.5,
            IsFullSnapshot = true,
            Actors = null,
            PayloadOpCode = ShooterOpCodes.Snapshot.PackedState,
            Payload = ShooterPackedSnapshotCodec.Serialize(in packed)
        };

        connection.Push(RoomGatewayOpCodes.SnapshotPushed, WireRoomGatewayBinary.Serialize(in push));

        Assert.Equal(ShooterSnapshotApplyResult.AppliedPackedSnapshot, launcher.GatewayConnection.LastPushResult);
        Assert.Equal(authority.CurrentFrame, launched.Session.CurrentFrame);
        Assert.Equal(authority.ComputeStateHash(), runtime.ComputeStateHash());
        Assert.Equal(authority.CurrentFrame, presentation.ViewModel.Frame);
    }

    [Fact]
    public async Task ClientNetworkLauncherCanBeCreatedFromConnectionFactoryAndEndpoint()
    {
        var connection = new FakeGatewayConnection { AutoRespondRoomGateway = true, JoinCurrentPlayerId = 61u };
        connection.Close();
        var factory = new ShooterClientConnectionFactory(() => connection);
        using var launcher = ShooterClientNetworkLauncher.Create(factory);
        var endpoint = ShooterClientNetworkEndpoint.Localhost(17002);
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var start = new ShooterStartGamePayload(
            "network-factory-session",
            30,
            4905,
            new[]
            {
                new ShooterStartPlayer(61, "P61", 0f, 0f),
                new ShooterStartPlayer(62, "P62", 5f, 0f)
            });

        var launched = await launcher.CreateReadyStartAndSubscribeAsync(
            endpoint,
            runtime,
            presentation,
            start,
            "session-token",
            ShooterRoomLaunchSpec.CreateDefault("client-factory"),
            playerId: 61u);

        Assert.Equal(connection, launcher.Connection);
        Assert.Equal(connection, launched.Connection);
        Assert.Equal("127.0.0.1", connection.OpenHost);
        Assert.Equal(17002, connection.OpenPort);
        Assert.True(launched.Session.IsStarted);
        Assert.Equal("battle-launch", launched.Battle.BattleId);
        Assert.Equal(9041ul, launched.Battle.WorldId);
        Assert.Equal(61u, launched.Battle.PlayerId);
    }

    [Fact]
    public void ClientConnectionFactoryCanWrapGameFrameworkNetworkChannel()
    {
        var channel = new FakeGameFrameworkNetworkChannel("ShooterGateway");
        var factory = ShooterClientConnectionFactory.FromGameFrameworkChannel(channel);

        using var connection = factory.CreateConnection();

        var gatewayConnection = Assert.IsType<GameFrameworkNetworkChannelConnection>(connection);
        Assert.False(gatewayConnection.IsConnected);

        gatewayConnection.Open("127.0.0.1", 17003);
        gatewayConnection.Tick(1f / 30f);

        Assert.True(gatewayConnection.IsConnected);
        Assert.True(channel.Connected);
        Assert.Equal(IPAddress.Loopback, channel.ConnectedAddress);
        Assert.Equal(17003, channel.ConnectedPort);

        gatewayConnection.Close();

        Assert.False(gatewayConnection.IsConnected);
        Assert.False(channel.Connected);
    }

    private sealed class FakeGameFrameworkNetworkChannel : INetworkChannel
    {
        public FakeGameFrameworkNetworkChannel(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public Socket Socket => throw new NotSupportedException();

        public bool Connected { get; private set; }

        public ServiceType ServiceType => ServiceType.Tcp;

        public global::GameFramework.Network.AddressFamily AddressFamily => global::GameFramework.Network.AddressFamily.IPv4;

        public int SendPacketCount => 0;

        public int SentPacketCount => 0;

        public int ReceivePacketCount => 0;

        public int ReceivedPacketCount => 0;

        public bool ResetHeartBeatElapseSecondsWhenReceivePacket { get; set; }

        public int MissHeartBeatCount => 0;

        public float HeartBeatInterval { get; set; }

        public float HeartBeatElapseSeconds => 0f;

        public IPAddress? ConnectedAddress { get; private set; }

        public int ConnectedPort { get; private set; }

        public EventHandler<Packet>? DefaultHandler { get; private set; }

        public List<Packet> SentPackets { get; } = new List<Packet>();

        public void RegisterHandler(IPacketHandler handler)
        {
        }

        public void SetDefaultHandler(EventHandler<Packet> handler)
        {
            DefaultHandler = handler;
        }

        public void Connect(IPAddress ipAddress, int port)
        {
            Connect(ipAddress, port, userData: null);
        }

        public void Connect(IPAddress ipAddress, int port, object? userData)
        {
            ConnectedAddress = ipAddress;
            ConnectedPort = port;
            Connected = true;
        }

        public void Close()
        {
            Connected = false;
        }

        public void Send<T>(T packet) where T : Packet
        {
            SentPackets.Add(packet);
        }
    }
}
