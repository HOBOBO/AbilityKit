using System;
using System.Threading.Tasks;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Room;
using AbilityKit.Network.Transport.InMemory;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Network.Room.Tests;

/// <summary>
/// End-to-end contract for <see cref="GatewayMultiplayerSession.CreateAsync"/> over an in-process
/// gateway: a <see cref="NetworkSession"/> wraps the server half of an
/// <see cref="InMemoryTransport"/> pair (frame codec reused, not reimplemented) and answers the
/// room-gateway wire protocol — the transport/login/assembly段 that the injectable
/// <see cref="GatewayMultiplayerSession.RunRoomFlowAsync"/> seam cannot cover.
/// </summary>
public sealed class GatewayMultiplayerSessionCreateTests
{
    private const string Host = "127.0.0.1";
    private const int Port = 1;
    private const string Account = "player-1";
    private static readonly RoomGatewayLaunchSpec Spec =
        new("region", "server", "test", "title", 2, 1, 1, 1, 1, "test-world", "client-1");

    [Fact]
    public async Task CreateAsync_HappyPath_ConnectsLogsInAndCompletesRoomFlow()
    {
        using var gateway = new MiniGateway();

        using var session = await GatewayMultiplayerSession.CreateAsync(
            Host, Port, Account, Spec,
            transportFactory: () => gateway.ClientTransport,
            timeout: TimeSpan.FromSeconds(10),
            waitForBattleStart: false);

        Assert.Equal(1, gateway.GuestLogins);
        Assert.Equal("wire-token", session.Result.SessionToken);
        Assert.Equal("room-1", session.Result.RoomId);
        Assert.Equal(1ul, session.Result.NumericRoomId);
        Assert.True(session.Result.Subscribed);
        Assert.NotNull(session.SdkClient);
        Assert.NotNull(session.RoomClient);
    }

    [Fact]
    public async Task CreateAsync_LoginRejected_ThrowsAndDisposesTransport()
    {
        using var gateway = new MiniGateway { LoginSucceeds = false };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GatewayMultiplayerSession.CreateAsync(
                Host, Port, Account, Spec,
                transportFactory: () => gateway.ClientTransport,
                timeout: TimeSpan.FromSeconds(10)));

        // Failure inside CreateAsync must dispose the SDK client (and with it the transport) —
        // InMemoryTransport.Dispose closes the pair, observable as !IsConnected.
        Assert.False(gateway.ClientTransport.IsConnected, "client transport should be closed after login failure");
        Assert.Equal(1, gateway.GuestLogins);
    }

    /// <summary>
    /// In-process room gateway: wraps the server half of an InMemory pair in a real
    /// <see cref="NetworkSession"/> (same frame codec as the client) and answers the
    /// guest-login + room-flow wire opcodes. Requests carry seq; responses echo it with a
    /// [int32 LE status=0][body] frame, exactly what RequestClient/TcpGatewayResponseCodec expect.
    /// </summary>
    private sealed class MiniGateway : IDisposable
    {
        private readonly NetworkSession _server;

        public InMemoryTransport ClientTransport { get; }

        public bool LoginSucceeds = true;

        public int GuestLogins;

        public MiniGateway()
        {
            (ClientTransport, var serverTransport) = InMemoryTransport.CreateConnectedPair();
            _server = new NetworkSession(serverTransport);
            _server.PacketReceived += OnPacketReceived;
            _server.Start();
        }

        private void OnPacketReceived(uint opCode, uint seq, ArraySegment<byte> payload)
        {
            switch (opCode)
            {
                case RoomGatewayOpCodes.GuestLogin:
                    GuestLogins++;
                    Reply(seq, WireRoomGatewayBinary.Serialize(new WireRoomGuestLoginRes
                    {
                        Success = LoginSucceeds,
                        SessionToken = "wire-token",
                        AccountId = Account,
                        Message = string.Empty,
                    }));
                    break;

                case RoomGatewayOpCodes.CreateRoom:
                    Reply(seq, WireRoomGatewayBinary.Serialize(new WireCreateRoomRes
                    {
                        Success = true,
                        RoomId = "room-1",
                        NumericRoomId = 1,
                        Message = string.Empty,
                    }));
                    break;

                case RoomGatewayOpCodes.JoinRoom:
                    Reply(seq, WireRoomGatewayBinary.Serialize(new WireJoinRoomRes
                    {
                        Success = true,
                        RoomId = "room-1",
                        NumericRoomId = 1,
                        Snapshot = RoomSnapshot(),
                        WorldStartAnchor = new WireWorldStartAnchor(),
                        Message = string.Empty,
                        JoinKind = WireRoomJoinKind.TeamLobby,
                        ServerNowTicks = 0,
                        CurrentPlayerId = 1,
                    }));
                    break;

                case RoomGatewayOpCodes.SetReady:
                    Reply(seq, WireRoomGatewayBinary.Serialize(new WireRoomSnapshotRes
                    {
                        Success = true,
                        RoomId = "room-1",
                        NumericRoomId = 1,
                        Snapshot = RoomSnapshot(),
                        Message = string.Empty,
                    }));
                    break;

                case RoomGatewayOpCodes.SubscribeStateSync:
                    Reply(seq, WireRoomGatewayBinary.Serialize(new WireSubscribeStateSyncRes
                    {
                        Success = true,
                        Message = string.Empty,
                    }));
                    break;

                default:
                    // Heartbeats and anything unknown: ignore (no response, no seq pairing).
                    break;
            }
        }

        private static WireRoomSnapshot RoomSnapshot() => new()
        {
            CanStart = true,
            BattleId = string.Empty,
            WorldId = 1,
        };

        private void Reply(uint seq, ArraySegment<byte> body)
        {
            var frame = new byte[4 + body.Count];
            // first 4 bytes: TcpGatewayStatusCode.Ok (little-endian int32, already zero-initialized)
            body.CopyTo(frame, 4);
            _server.Send(seq, new ArraySegment<byte>(frame), 0, seq);
        }

        public void Dispose()
        {
            _server.PacketReceived -= OnPacketReceived;
            _server.Dispose();
        }
    }
}
