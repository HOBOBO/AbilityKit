using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

internal sealed class ScriptedShooterGatewayLaunchTransport : IShooterRoomGatewayRequestTransport
{
    public readonly List<uint> OpCodes = new List<uint>();

    public long StartServerTicks { get; set; } = 123456L;

    public long ServerTickFrequency { get; set; } = 10000000L;

    public int StartFrame { get; set; }

    public double FixedDeltaSeconds { get; set; } = 1d / 30d;

    public long JoinServerNowTicks { get; set; } = 123456L;

    public long StartServerNowTicks { get; set; } = 123456L;

    public string JoinBattleId { get; set; } = "battle-prelaunch";

    public ulong JoinWorldId { get; set; }

    public bool JoinCanStart { get; set; } = true;
 
    public uint JoinCurrentPlayerId { get; set; } = 121u;
 
    public WireRoomJoinKind JoinKind { get; set; } = WireRoomJoinKind.TeamLobby;

    public WireReportAssetsLoadedReq LastReportAssetsLoadedRequest { get; private set; }

    public ArraySegment<byte> LastPayload { get; private set; }

    public Task<ArraySegment<byte>> SendRequestAsync(uint opCode, ArraySegment<byte> payload, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        OpCodes.Add(opCode);
        LastPayload = TestByteSegments.Copy(payload);

        switch (opCode)
        {
            case RoomGatewayOpCodes.CreateRoom:
                return Task.FromResult(WireRoomGatewayBinary.Serialize(new WireCreateRoomRes
                {
                    Success = true,
                    RoomId = "room-launch",
                    NumericRoomId = 1041ul,
                    Message = "created"
                }));
            case RoomGatewayOpCodes.JoinRoom:
                return Task.FromResult(WireRoomGatewayBinary.Serialize(new WireJoinRoomRes
                {
                    Success = true,
                    RoomId = "room-launch",
                    NumericRoomId = 1041ul,
                    Snapshot = new WireRoomSnapshot { BattleId = JoinBattleId, CanStart = JoinCanStart, WorldId = JoinWorldId },
                    WorldStartAnchor = CreateAnchor(),
                    Message = "joined",
                    JoinKind = JoinKind,
                    ServerNowTicks = JoinServerNowTicks,
                    CurrentPlayerId = JoinCurrentPlayerId
                }));
            case RoomGatewayOpCodes.SetReady:
                return Task.FromResult(WireRoomGatewayBinary.Serialize(new WireRoomSnapshotRes
                {
                    Success = true,
                    RoomId = "room-launch",
                    NumericRoomId = 1041ul,
                    Snapshot = new WireRoomSnapshot { BattleId = "battle-ready", CanStart = true },
                    Message = "ready"
                }));
            case RoomGatewayOpCodes.BeginLoading:
                return Task.FromResult(WireRoomGatewayBinary.Serialize(new WireRoomOperationRes
                {
                    Success = true,
                    Applied = true,
                    RoomRevision = 3L,
                    Snapshot = CreateStagedSnapshot(phase: 1, battleId: string.Empty, worldId: 0ul),
                    Message = "loading"
                }));
            case RoomGatewayOpCodes.ReportLoadingProgress:
                return Task.FromResult(WireRoomGatewayBinary.Serialize(new WireRoomOperationRes
                {
                    Success = true,
                    Applied = true,
                    RoomRevision = 3L,
                    Snapshot = CreateStagedSnapshot(phase: 1, battleId: string.Empty, worldId: 0ul),
                    Message = "progress"
                }));
            case RoomGatewayOpCodes.ReportAssetsLoaded:
                LastReportAssetsLoadedRequest = WireRoomGatewayBinary.Deserialize<WireReportAssetsLoadedReq>(payload);
                return Task.FromResult(WireRoomGatewayBinary.Serialize(new WireRoomOperationRes
                {
                    Success = true,
                    Applied = true,
                    RoomRevision = 4L,
                    Snapshot = CreateStagedSnapshot(phase: 3, battleId: "battle-launch", worldId: 9041ul),
                    Message = "loaded"
                }));
            case RoomGatewayOpCodes.GetSnapshot:
                return Task.FromResult(WireRoomGatewayBinary.Serialize(new WireRoomSnapshotRes
                {
                    Success = true,
                    RoomId = "room-launch",
                    NumericRoomId = 1041ul,
                    Snapshot = CreateStagedSnapshot(phase: 3, battleId: "battle-launch", worldId: 9041ul),
                    Message = "running",
                    ServerNowTicks = StartServerNowTicks
                }));
            case RoomGatewayOpCodes.SubscribeStateSync:
                return Task.FromResult(WireRoomGatewayBinary.Serialize(new WireSubscribeStateSyncRes
                {
                    Success = true,
                    Message = "subscribed"
                }));
            case RoomGatewayOpCodes.SubmitBattleInput:
                return Task.FromResult(WireRoomGatewayBinary.Serialize(new WireSubmitBattleInputRes
                {
                    Success = true,
                    AcceptedFrame = 0,
                    Message = "accepted"
                }));
            default:
                throw new InvalidOperationException("Unexpected room gateway opCode: " + opCode);
        }
    }

    private WireRoomSnapshot CreateStagedSnapshot(int phase, string battleId, ulong worldId)
    {
        return new WireRoomSnapshot
        {
            Summary = new WireRoomSummary { RoomId = "room-launch" },
            CanStart = true,
            BattleId = battleId,
            WorldId = worldId,
            WorldStartAnchor = CreateAnchor(),
            Phase = phase,
            LaunchGeneration = 7L,
            LaunchManifestVersion = 3,
            LaunchManifestHash = "manifest-shooter-v3",
            RoomRevision = 4L,
            LastEventSequence = 4L
        };
    }

    private WireWorldStartAnchor CreateAnchor()
    {
        return new WireWorldStartAnchor
        {
            StartServerTicks = StartServerTicks,
            ServerTickFrequency = ServerTickFrequency,
            StartFrame = StartFrame,
            FixedDeltaSeconds = FixedDeltaSeconds
        };
    }
}
