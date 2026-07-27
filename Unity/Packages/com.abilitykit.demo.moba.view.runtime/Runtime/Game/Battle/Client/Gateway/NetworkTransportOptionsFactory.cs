using System;
using System.Collections.Generic;
using System.Threading;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Logging;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Game.Battle.Transport;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using AbilityKit.Protocol.Room;
using AbilityKit.Game.Battle.Agent;
using StateSyncOpCodes = AbilityKit.Protocol.Moba.StateSync.OpCodes;

namespace AbilityKit.Game.Battle
{
    public static class NetworkTransportOptionsFactory
    {
        public static NetworkTransportOptions Create(
            string host,
            int port,
            Func<ITransport> transportFactory,
            Func<PlayerId, uint> playerIdToUInt,
            Func<uint, PlayerId> playerIdFromUInt,
            Func<WorldId, ulong> worldIdToUlong,
            Func<ulong, WorldId> worldIdFromUlong,
            ulong roomId,
            string sessionToken,
            string battleId = "",
            string publicRoomId = "")
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
            if (port <= 0) throw new ArgumentOutOfRangeException(nameof(port));
            if (transportFactory == null) throw new ArgumentNullException(nameof(transportFactory));
            if (playerIdToUInt == null) throw new ArgumentNullException(nameof(playerIdToUInt));
            if (playerIdFromUInt == null) throw new ArgumentNullException(nameof(playerIdFromUInt));
            if (worldIdToUlong == null) throw new ArgumentNullException(nameof(worldIdToUlong));
            if (worldIdFromUlong == null) throw new ArgumentNullException(nameof(worldIdFromUlong));

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

                OpPostAuthentication = string.IsNullOrWhiteSpace(battleId)
                    ? 0u
                    : RoomGatewayOpCodes.SubscribeStateSync,
                SerializePostAuthentication = string.IsNullOrWhiteSpace(battleId)
                    ? null
                    : () =>
                    {
                        var wire = new WireSubscribeStateSyncReq
                        {
                            SessionToken = sessionToken,
                            BattleId = battleId,
                            RoomId = publicRoomId ?? string.Empty
                        };
                        return WireRoomGatewayBinary.Serialize(in wire);
                    },
                SerializePostAuthenticationWithReliableEventCursor =
                    string.IsNullOrWhiteSpace(battleId)
                        ? null
                        : (eventEpoch, lastEventAck) =>
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

                OpSubmitInput = RoomGatewayOpCodes.SubmitBattleInput,
                SubmitInputRetryFrameLead = 2,
                PrepareSubmitInput = requestObj =>
                {
                    if (requestObj is not SubmitInputRequest request)
                    {
                        throw new ArgumentException(
                            "Expected SubmitInputRequest.",
                            nameof(requestObj));
                    }

                    return new SequencedSubmitInputRequest(
                        request,
                        unchecked((ulong)Interlocked.Increment(ref nextCommandSequence)));
                },
                OpFramePushed = OpCodes.FramePushed,
                OpSnapshotPushed = StateSyncOpCodes.SnapshotPushed,
                OpDeltaSnapshotPushed = StateSyncOpCodes.DeltaSnapshotPushed,
                OpReliableEventsPushed = RoomGatewayOpCodes.ReliableBattleEventsPushed,
                OpAcknowledgeReliableEvents = RoomGatewayOpCodes.AckReliableBattleEvents,
                DeserializeReliableEventsPushed = payload =>
                    WireRoomGatewayBinary.Deserialize<WireReliableBattleEventPush>(payload),
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
                OpRequestFullStateSync = RoomGatewayOpCodes.RequestFullStateSync,
                SerializeRequestFullStateSync = (reason, lastAuthoritativeFrame) =>
                {
                    var wire = new WireRequestFullStateSyncReq
                    {
                        SessionToken = sessionToken,
                        BattleId = battleId,
                        RoomId = publicRoomId ?? string.Empty,
                        WorldId = roomId,
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
                RewriteSubmitInputFrame = (requestObj, frame) =>
                {
                    if (requestObj is not SequencedSubmitInputRequest sequenced)
                    {
                        throw new ArgumentException(
                            "Expected SequencedSubmitInputRequest.",
                            nameof(requestObj));
                    }

                    var request = sequenced.Request;
                    return new SequencedSubmitInputRequest(
                        new SubmitInputRequest(
                            request.WorldId,
                            new PlayerInputCommand(
                                new FrameIndex(frame),
                                request.Input.Player,
                                request.Input.OpCode,
                                request.Input.Payload)),
                        sequenced.CommandSequence);
                },
                DeserializeSubmitInputResponse = payload =>
                {
                    var wire = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputRes>(payload);
                    return new NetworkSubmitInputResponse(
                        wire.Success,
                        wire.CurrentFrame,
                        wire.Success ? 0 : 1,
                        wire.ShouldResync);
                },

                // 尚未接线，后续由 room flow 持有这些操作。
                OpCreateWorld = 0,
                OpJoin = 0,
                OpLeave = 0,

                SerializeSubmitInput = requestObj =>
                {
                    if (requestObj is not SequencedSubmitInputRequest sequenced) return default;

                    var req = sequenced.Request;
                    var pid = playerIdToUInt(req.Input.Player);
                    var wid = worldIdToUlong(req.WorldId);

                    var wire = new WireSubmitBattleInputReq
                    {
                        SessionToken = sessionToken,
                        BattleId = battleId,
                        WorldId = wid,
                        Frame = req.Input.Frame.Value,
                        PlayerId = pid,
                        InputOpCode = req.Input.OpCode,
                        Payload = req.Input.Payload ?? Array.Empty<byte>(),
                        CommandSequence = sequenced.CommandSequence
                    };

                    return WireRoomGatewayBinary.Serialize(in wire);
                },

                DeserializeFramePushed = payload =>
                {
                    var push = WireCustomBinary.DeserializeFramePushedPush(payload);

                    var worldId = worldIdFromUlong(push.WorldId);
                    var frame = new FrameIndex(push.Frame);

                    var inputs = (IReadOnlyList<PlayerInputCommand>)(push.Inputs == null || push.Inputs.Length == 0
                        ? Array.Empty<PlayerInputCommand>()
                        : ConvertInputs(frame, push.Inputs, playerIdFromUInt));
                    if (inputs.Count > 0)
                    {
                        Log.Info($"[NetworkTransportOptionsFactory] Decoded authoritative inputs. worldId={worldId.Value}, frame={frame.Value}, count={inputs.Count}, firstOpCode={inputs[0].OpCode}");
                    }

                    return new FramePacket(worldId, frame, inputs, snapshot: null);
                },

                DeserializeSnapshotPushed = payload =>
                {
                    var wire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(payload);
                    return GatewayRoomClient.ToGatewaySnapshot(in wire);
                }
            };
        }

        private readonly struct SequencedSubmitInputRequest
        {
            public readonly SubmitInputRequest Request;
            public readonly ulong CommandSequence;

            public SequencedSubmitInputRequest(
                in SubmitInputRequest request,
                ulong commandSequence)
            {
                Request = request;
                CommandSequence = commandSequence;
            }
        }

        private static PlayerInputCommand[] ConvertInputs(FrameIndex frame, WireInputItem[] inputs, Func<uint, PlayerId> playerIdFromUInt)
        {
            var arr = new PlayerInputCommand[inputs.Length];
            for (int i = 0; i < inputs.Length; i++)
            {
                var it = inputs[i];
                var pid = playerIdFromUInt(it.PlayerId);

                arr[i] = new PlayerInputCommand(
                    frame: frame,
                    player: pid,
                    opCode: it.OpCode,
                    payload: it.Payload);
            }
            return arr;
        }
    }
}
