using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime.TcpGateway;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

internal sealed class FakeGatewayConnection : IConnection
{
    public readonly List<uint> SentOpCodes = new List<uint>();

    public ConnectionState State { get; private set; } = ConnectionState.Connected;

    public bool IsConnected => State == ConnectionState.Connected;

    public bool AutoRespondRoomGateway { get; set; }
 
    public uint JoinCurrentPlayerId { get; set; } = 121u;
 
    public string OpenHost { get; private set; } = string.Empty;

    public int OpenPort { get; private set; }

    public int TickCount { get; private set; }

    public int CloseCount { get; private set; }

    public int DisposeCount { get; private set; }

    public int PacketReceivedSubscriberCount => _packetReceived?.GetInvocationList().Length ?? 0;

    public int ServerPushReceivedSubscriberCount => _serverPushReceived?.GetInvocationList().Length ?? 0;

    private event Action? ConnectedHandlers;
    private event Action? DisconnectedHandlers;
    private event Action<Exception>? ErrorHandlers;
    private event Action<uint, uint, ArraySegment<byte>>? _packetReceived;
    private event Action<uint, ArraySegment<byte>>? _serverPushReceived;
    private event Action<string, string>? KickedHandlers;

    public event Action? Connected
    {
        add => ConnectedHandlers += value;
        remove => ConnectedHandlers -= value;
    }

    public event Action? Disconnected
    {
        add => DisconnectedHandlers += value;
        remove => DisconnectedHandlers -= value;
    }

    public event Action<Exception>? Error
    {
        add => ErrorHandlers += value;
        remove => ErrorHandlers -= value;
    }

    public event Action<uint, uint, ArraySegment<byte>>? PacketReceived
    {
        add => _packetReceived += value;
        remove => _packetReceived -= value;
    }

    public event Action<uint, ArraySegment<byte>>? ServerPushReceived
    {
        add => _serverPushReceived += value;
        remove => _serverPushReceived -= value;
    }

    public event Action<string, string>? Kicked
    {
        add => KickedHandlers += value;
        remove => KickedHandlers -= value;
    }

    public uint LastSentOpCode { get; private set; }

    public ArraySegment<byte> LastSentPayload { get; private set; }

    public NetworkPacketFlags LastSentFlags { get; private set; }

    public uint LastSentSeq { get; private set; }

    public void Open(string host, int port)
    {
        OpenHost = host ?? string.Empty;
        OpenPort = port;
        State = ConnectionState.Connected;
        ConnectedHandlers?.Invoke();
    }

    public void Close()
    {
        CloseCount++;
        State = ConnectionState.Disconnected;
        DisconnectedHandlers?.Invoke();
    }

    public void Tick(float deltaTime)
    {
        TickCount++;
    }

    public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0)
    {
        LastSentOpCode = opCode;
        LastSentPayload = TestByteSegments.Copy(payload);
        LastSentFlags = (NetworkPacketFlags)flags;
        LastSentSeq = seq;
        SentOpCodes.Add(opCode);

        if (AutoRespondRoomGateway && ((NetworkPacketFlags)flags & NetworkPacketFlags.Request) != 0)
        {
            CompleteRoomGatewayResponse(opCode, seq);
        }
    }

    public void CompleteResponse(uint opCode, uint seq, in WireSubmitBattleInputRes response)
    {
        var payload = WireRoomGatewayBinary.Serialize(in response);
        _packetReceived?.Invoke(opCode, seq, EncodeGatewayResponse(TcpGatewayStatusCode.Ok, payload));
    }

    private void CompleteRoomGatewayResponse(uint opCode, uint seq)
    {
        switch (opCode)
        {
            case RoomGatewayOpCodes.CreateRoom:
                CompleteResponse(opCode, seq, new WireCreateRoomRes
                {
                    Success = true,
                    RoomId = "room-launch",
                    NumericRoomId = 1041ul,
                    Message = "created"
                });
                break;
            case RoomGatewayOpCodes.JoinRoom:
                CompleteResponse(opCode, seq, new WireJoinRoomRes
                {
                    Success = true,
                    RoomId = "room-launch",
                    NumericRoomId = 1041ul,
                    Snapshot = new WireRoomSnapshot { BattleId = "battle-prelaunch", CanStart = true, WorldId = 0ul },
                    WorldStartAnchor = new WireWorldStartAnchor
                    {
                        StartServerTicks = 123456L,
                        ServerTickFrequency = 10000000L,
                        StartFrame = 0,
                        FixedDeltaSeconds = 1d / 30d
                    },
                    Message = "joined",
                    JoinKind = WireRoomJoinKind.TeamLobby,
                    ServerNowTicks = 123456L,
                    CurrentPlayerId = JoinCurrentPlayerId
                });
                break;
            case RoomGatewayOpCodes.SetReady:
                CompleteResponse(opCode, seq, new WireRoomSnapshotRes
                {
                    Success = true,
                    RoomId = "room-launch",
                    NumericRoomId = 1041ul,
                    Snapshot = new WireRoomSnapshot { BattleId = "battle-ready", CanStart = true },
                    Message = "ready"
                });
                break;
            case RoomGatewayOpCodes.StartBattle:
                CompleteResponse(opCode, seq, new WireStartRoomBattleRes
                {
                    Success = true,
                    BattleId = "battle-launch",
                    WorldId = 9041ul,
                    Started = true,
                    WorldStartAnchor = new WireWorldStartAnchor
                    {
                        StartServerTicks = 123456L,
                        ServerTickFrequency = 10000000L,
                        StartFrame = 0,
                        FixedDeltaSeconds = 1d / 30d
                    },
                    ServerNowTicks = 123456L,
                    Message = "started"
                });
                break;
            case RoomGatewayOpCodes.SubscribeStateSync:
                CompleteResponse(opCode, seq, new WireSubscribeStateSyncRes
                {
                    Success = true,
                    Message = "subscribed"
                });
                break;
            case RoomGatewayOpCodes.BeginLoading:
                CompleteResponse(opCode, seq, new WireRoomOperationRes
                {
                    Success = true,
                    Applied = true,
                    RoomRevision = 2,
                    Snapshot = new WireRoomSnapshot
                    {
                        Summary = new WireRoomSummary { RoomId = "room-launch", OwnerAccountId = "account-1" },
                        CanStart = true,
                        BattleId = "battle-launch",
                        WorldId = 9041ul,
                        Phase = 1, // Loading
                        LaunchGeneration = 1,
                        LaunchManifestVersion = 1,
                        LaunchManifestHash = "test-manifest",
                        WorldStartAnchor = new WireWorldStartAnchor
                        {
                            StartServerTicks = 123456L,
                            ServerTickFrequency = 10000000L,
                            StartFrame = 0,
                            FixedDeltaSeconds = 1d / 30d
                        }
                    },
                    Message = "loading"
                });
                break;
            case RoomGatewayOpCodes.ReportAssetsLoaded:
                CompleteResponse(opCode, seq, new WireRoomOperationRes
                {
                    Success = true,
                    Applied = true,
                    RoomRevision = 3,
                    Snapshot = new WireRoomSnapshot
                    {
                        Summary = new WireRoomSummary { RoomId = "room-launch", OwnerAccountId = "account-1" },
                        CanStart = true,
                        BattleId = "battle-launch",
                        WorldId = 9041ul,
                        Phase = 2, // Starting
                        LaunchGeneration = 1,
                        LaunchManifestVersion = 1,
                        LaunchManifestHash = "test-manifest"
                    },
                    Message = "assets-loaded"
                });
                break;
            case RoomGatewayOpCodes.ReportLoadingProgress:
                var progressRequest = WireRoomGatewayBinary.Deserialize<WireReportLoadingProgressReq>(LastSentPayload);
                CompleteResponse(opCode, seq, new WireRoomOperationRes
                {
                    Success = true,
                    Applied = true,
                    RoomRevision = 2,
                    Snapshot = new WireRoomSnapshot
                    {
                        Summary = new WireRoomSummary { RoomId = "room-launch", OwnerAccountId = "account-1" },
                        CanStart = true,
                        Phase = 1,
                        LaunchGeneration = 1,
                        LaunchManifestVersion = 1,
                        LaunchManifestHash = "test-manifest",
                        Players = new List<WireRoomPlayerSnapshot>
                        {
                            new WireRoomPlayerSnapshot
                            {
                                AccountId = "account-1",
                                PlayerId = 1,
                                IsOnline = true,
                                LobbyReady = true,
                                LoadingProgress = progressRequest.Progress
                            }
                        }
                    },
                    Message = "progress"
                });
                break;
            case RoomGatewayOpCodes.GetSnapshot:
                CompleteResponse(opCode, seq, new WireRoomSnapshotRes
                {
                    Success = true,
                    RoomId = "room-launch",
                    NumericRoomId = 1041ul,
                    Snapshot = new WireRoomSnapshot
                    {
                        Summary = new WireRoomSummary { RoomId = "room-launch", OwnerAccountId = "account-1" },
                        CanStart = true,
                        BattleId = "battle-launch",
                        WorldId = 9041ul,
                        Phase = 3, // InBattle —— WaitForBattleStart 立即返回
                        LaunchGeneration = 1,
                        WorldStartAnchor = new WireWorldStartAnchor
                        {
                            StartServerTicks = 123456L,
                            ServerTickFrequency = 10000000L,
                            StartFrame = 0,
                            FixedDeltaSeconds = 1d / 30d
                        }
                    },
                    ServerNowTicks = 123456L,
                    Message = "in-battle"
                });
                break;
            case RoomGatewayOpCodes.SubmitBattleInput:
                CompleteResponse(opCode, seq, new WireSubmitBattleInputRes
                {
                    Success = true,
                    AcceptedFrame = 0,
                    Message = "accepted"
                });
                break;
            default:
                throw new InvalidOperationException("Unexpected room gateway opCode: " + opCode);
        }
    }

    public void CompleteResponse<T>(uint opCode, uint seq, in T response)
    {
        var payload = WireRoomGatewayBinary.Serialize(in response);
        _packetReceived?.Invoke(opCode, seq, EncodeGatewayResponse(TcpGatewayStatusCode.Ok, payload));
    }

    public void Push(uint opCode, ArraySegment<byte> payload)
    {
        _serverPushReceived?.Invoke(opCode, TestByteSegments.Copy(payload));
    }

    public void Dispose()
    {
        DisposeCount++;
        Close();
    }

    public void RaiseError(Exception exception)
    {
        ErrorHandlers?.Invoke(exception);
    }

    public void Kick(string code, string reason)
    {
        KickedHandlers?.Invoke(code, reason);
    }

    private static ArraySegment<byte> EncodeGatewayResponse(TcpGatewayStatusCode statusCode, ArraySegment<byte> payload)
    {
        var length = 4 + payload.Count;
        var bytes = new byte[length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, 4), (int)statusCode);
        if (payload.Array != null && payload.Count > 0)
        {
            Buffer.BlockCopy(payload.Array, payload.Offset, bytes, 4, payload.Count);
        }

        return new ArraySegment<byte>(bytes);
    }
}
