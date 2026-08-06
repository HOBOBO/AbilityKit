#nullable enable

using System;
using System.Threading;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Protocol;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Shooter.View
{
    /// <summary>
    /// Builds the <see cref="NetworkTransportOptions"/> that drive shooter's server-authoritative
    /// StateSync battle data-plane over the shared room-gateway wire protocol (<c>protocol.room</c>).
    /// Mirrors moba's <c>NetworkTransportOptionsFactory</c> (room-gateway path); shooter has no
    /// FrameSync path. Downlink deserializers return a <see cref="ShooterBattlePushEnvelope"/> carrying
    /// the original (opCode, payload) so the existing <c>ShooterClientSession.ApplyGatewayPush</c>
    /// pipeline is fed unchanged.
    /// </summary>
    public static class ShooterNetworkTransportOptionsFactory
    {
        public static NetworkTransportOptions Create(
            string host,
            int port,
            Func<ITransport> transportFactory,
            Func<PlayerId, uint> playerIdToUInt,
            Func<WorldId, ulong> worldIdToUlong,
            ulong worldId,
            string sessionToken,
            string battleId,
            string publicRoomId,
            Func<string>? getReliableEventEpoch = null,
            Func<long>? getReliableEventLastAcknowledgedSequence = null)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
            if (port <= 0) throw new ArgumentOutOfRangeException(nameof(port));
            if (transportFactory == null) throw new ArgumentNullException(nameof(transportFactory));
            if (playerIdToUInt == null) throw new ArgumentNullException(nameof(playerIdToUInt));
            if (worldIdToUlong == null) throw new ArgumentNullException(nameof(worldIdToUlong));
            if (string.IsNullOrWhiteSpace(battleId))
            {
                throw new ArgumentException("Battle id is required for authoritative input submission.", nameof(battleId));
            }

            long nextCommandSequence = 0;
            return new NetworkTransportOptions
            {
                Host = host,
                Port = port,
                TransportFactory = transportFactory,
                FrameCodec = LengthPrefixedFrameCodec.Instance,

                // 建连/重连握手：把新战斗连接绑定到 session 并订阅状态同步推送。
                OpRenewSession = RoomGatewayOpCodes.RenewSession,
                SessionToken = sessionToken,
                SerializeRenewSession = token =>
                {
                    var wire = new WireRenewSessionReq
                    {
                        SessionToken = token,
                        ExtendSeconds = 0,
                        RotateToken = false
                    };
                    return WireRoomGatewayBinary.Serialize(in wire);
                },
                OpPostAuthentication = RoomGatewayOpCodes.SubscribeStateSync,
                SerializePostAuthenticationWithReliableEventCursor = (eventEpoch, lastEventAck) =>
                {
                    var wire = new WireSubscribeStateSyncReq
                    {
                        SessionToken = sessionToken,
                        BattleId = battleId,
                        RoomId = publicRoomId ?? string.Empty,
                        EventEpoch = eventEpoch ?? string.Empty,
                        LastEventAck = Math.Max(0L, lastEventAck)
                    };
                    return WireRoomGatewayBinary.Serialize(in wire);
                },

                // 输入上行（op 107）
                OpSubmitInput = RoomGatewayOpCodes.SubmitBattleInput,
                SubmitInputRetryFrameLead = 2,
                PrepareSubmitInput = requestObj =>
                {
                    if (requestObj is not SubmitInputRequest request)
                    {
                        throw new ArgumentException("Expected SubmitInputRequest.", nameof(requestObj));
                    }
                    return new SequencedSubmitInputRequest(
                        request,
                        unchecked((ulong)Interlocked.Increment(ref nextCommandSequence)));
                },
                SerializeSubmitInput = requestObj =>
                {
                    if (requestObj is not SequencedSubmitInputRequest sequenced) return default;
                    var req = sequenced.Request;
                    var wire = new WireSubmitBattleInputReq
                    {
                        SessionToken = sessionToken,
                        BattleId = battleId,
                        WorldId = worldIdToUlong(req.WorldId),
                        Frame = req.Input.Frame.Value,
                        PlayerId = playerIdToUInt(req.Input.Player),
                        InputOpCode = req.Input.OpCode,
                        Payload = req.Input.Payload ?? Array.Empty<byte>(),
                        CommandSequence = sequenced.CommandSequence
                    };
                    return WireRoomGatewayBinary.Serialize(in wire);
                },
                DeserializeSubmitInputResponse = payload =>
                {
                    var wire = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputRes>(payload);
                    // 与 moba 一致：服务端 ShouldResync → 引擎按权威帧重试一次。
                    return new NetworkSubmitInputResponse(
                        wire.Success,
                        wire.CurrentFrame,
                        wire.Success ? 0 : 1,
                        wire.ShouldResync,
                        wire.Status,
                        wire.Message);
                },
                RewriteSubmitInputFrame = (requestObj, frame) =>
                {
                    if (requestObj is not SequencedSubmitInputRequest sequenced)
                    {
                        throw new ArgumentException("Expected SequencedSubmitInputRequest.", nameof(requestObj));
                    }
                    var req = sequenced.Request;
                    return new SequencedSubmitInputRequest(
                        new SubmitInputRequest(
                            req.WorldId,
                            new PlayerInputCommand(
                                new FrameIndex(frame),
                                req.Input.Player,
                                req.Input.OpCode,
                                req.Input.Payload)),
                        sequenced.CommandSequence);
                },

                // 下行：快照/增量快照/可靠事件。反序列化器返回携带原始 (opCode, payload) 的信封，
                // 交给 ShooterClientSession.ApplyGatewayPush 走既有完整 apply 管线（3 套同步策略不变）。
                OpSnapshotPushed = RoomGatewayOpCodes.SnapshotPushed,
                OpDeltaSnapshotPushed = RoomGatewayOpCodes.DeltaSnapshotPushed,
                OpReliableEventsPushed = RoomGatewayOpCodes.ReliableBattleEventsPushed,
                DeserializeSnapshotPushed = payload =>
                {
                    var wire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(payload);
                    var opCode = wire.IsFullSnapshot
                        ? RoomGatewayOpCodes.SnapshotPushed
                        : RoomGatewayOpCodes.DeltaSnapshotPushed;
                    return new ShooterBattlePushEnvelope(opCode, payload);
                },
                DeserializeReliableEventsPushed = payload =>
                    new ShooterBattlePushEnvelope(RoomGatewayOpCodes.ReliableBattleEventsPushed, payload),

                // 可靠事件 ack（op 116）
                OpAcknowledgeReliableEvents = RoomGatewayOpCodes.AckReliableBattleEvents,
                SerializeAcknowledgeReliableEvents = (epoch, sequence) =>
                {
                    var wire = new WireAckReliableBattleEventsReq
                    {
                        SessionToken = sessionToken,
                        BattleId = battleId,
                        RoomId = publicRoomId ?? string.Empty,
                        Epoch = epoch ?? string.Empty,
                        AckSequence = Math.Max(0L, sequence)
                    };
                    return WireRoomGatewayBinary.Serialize(in wire);
                },
                DeserializeAcknowledgeReliableEventsResponse = payload =>
                {
                    var wire = WireRoomGatewayBinary.Deserialize<WireAckReliableBattleEventsRes>(payload);
                    return wire.Success ? wire.AcceptedAckSequence : -1L;
                },

                // 全量 resync（op 108）
                OpRequestFullStateSync = RoomGatewayOpCodes.RequestFullStateSync,
                SerializeRequestFullStateSync = (reason, lastAuthoritativeFrame) =>
                {
                    var wire = new WireRequestFullStateSyncReq
                    {
                        SessionToken = sessionToken,
                        BattleId = battleId,
                        RoomId = publicRoomId ?? string.Empty,
                        WorldId = worldId,
                        ClientFrame = lastAuthoritativeFrame,
                        LastAuthoritativeFrame = lastAuthoritativeFrame,
                        ClientStateHash = 0,
                        AuthoritativeStateHash = 0,
                        Reason = reason ?? string.Empty
                    };
                    return WireRoomGatewayBinary.Serialize(in wire);
                },
                DeserializeRequestFullStateSyncResponse = payload =>
                {
                    var wire = WireRoomGatewayBinary.Deserialize<WireRequestFullStateSyncRes>(payload);
                    return wire.Success && wire.Accepted;
                },

                // 可靠事件游标（续接/重连后续 ack 用）
                GetReliableEventEpoch = getReliableEventEpoch,
                GetReliableEventLastAcknowledgedSequence = getReliableEventLastAcknowledgedSequence,

                // 房间生命周期由房间连接的 ShooterRoomGatewayFlow 持有，战斗 transport 不接线。
                OpCreateWorld = 0,
                OpJoin = 0,
                OpLeave = 0,
            };
        }

        /// <summary>Carries the original push (opCode, payload) through NetworkTransport's typed downlink events.</summary>
        public readonly struct ShooterBattlePushEnvelope
        {
            public readonly uint OpCode;
            public readonly ArraySegment<byte> Payload;

            public ShooterBattlePushEnvelope(uint opCode, ArraySegment<byte> payload)
            {
                OpCode = opCode;
                Payload = payload;
            }
        }

        private readonly struct SequencedSubmitInputRequest
        {
            public readonly SubmitInputRequest Request;
            public readonly ulong CommandSequence;

            public SequencedSubmitInputRequest(in SubmitInputRequest request, ulong commandSequence)
            {
                Request = request;
                CommandSequence = commandSequence;
            }
        }
    }
}
