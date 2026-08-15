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
        Assert.Equal(RoomGatewayOpCodes.CreateRoom, connection.SentOpCodes[0]);
        Assert.Equal(RoomGatewayOpCodes.JoinRoom, connection.SentOpCodes[1]);
        Assert.Equal(RoomGatewayOpCodes.SetReady, connection.SentOpCodes[2]);
        Assert.Equal(RoomGatewayOpCodes.BeginLoading, connection.SentOpCodes[3]);
        var progressCount = connection.SentOpCodes.Count(opCode => opCode == RoomGatewayOpCodes.ReportLoadingProgress);
        Assert.True(progressCount >= 1);
        Assert.All(
            connection.SentOpCodes.GetRange(4, progressCount),
            opCode => Assert.Equal(RoomGatewayOpCodes.ReportLoadingProgress, opCode));
        var assetsIndex = 4 + progressCount;
        Assert.Equal(RoomGatewayOpCodes.ReportAssetsLoaded, connection.SentOpCodes[assetsIndex]);
        Assert.Equal(RoomGatewayOpCodes.GetSnapshot, connection.SentOpCodes[assetsIndex + 1]);
        Assert.Equal(RoomGatewayOpCodes.SubscribeStateSync, connection.SentOpCodes[assetsIndex + 2]);
        Assert.Equal(assetsIndex + 3, connection.SentOpCodes.Count);

        launcher.Tick(1f / 30f);
        Assert.Equal(1, connection.TickCount);
        Assert.DoesNotContain(RoomGatewayOpCodes.SubmitBattleInput, connection.SentOpCodes);

        // Room 连接只拥有控制面；战斗输入和快照由独立 BattleDataPlane 连接处理。
        var frameBeforeRoomPush = launched.Session.CurrentFrame;
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

        Assert.Equal(ShooterSnapshotApplyResult.Ignored, launcher.GatewayConnection.LastPushResult);
        Assert.Equal(frameBeforeRoomPush, launched.Session.CurrentFrame);
    }

    [Fact]
    public void ClientNetworkLauncherUsesSingleSdkRequestOwnerAndDisposesConnectionOnce()
    {
        var connection = new FakeGatewayConnection();
        var launcher = new ShooterClientNetworkLauncher(connection);

        Assert.Same(connection, launcher.Connection);
        Assert.Equal(1, connection.PacketReceivedSubscriberCount);
        Assert.Equal(1, connection.ServerPushReceivedSubscriberCount);

        launcher.Dispose();
        launcher.Dispose();

        Assert.Equal(0, connection.PacketReceivedSubscriberCount);
        Assert.Equal(0, connection.ServerPushReceivedSubscriberCount);
        Assert.Equal(1, connection.DisposeCount);
        Assert.Equal(2, connection.CloseCount);
    }

    [Fact]
    public void SdkBackedGatewayDisposeDoesNotOwnLauncherConnection()
    {
        var connection = new FakeGatewayConnection();
        using var launcher = new ShooterClientNetworkLauncher(connection);

        launcher.GatewayConnection.Dispose();

        Assert.Equal(1, connection.PacketReceivedSubscriberCount);
        Assert.Equal(1, connection.ServerPushReceivedSubscriberCount);
        Assert.Equal(0, connection.DisposeCount);
        Assert.True(connection.IsConnected);
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
