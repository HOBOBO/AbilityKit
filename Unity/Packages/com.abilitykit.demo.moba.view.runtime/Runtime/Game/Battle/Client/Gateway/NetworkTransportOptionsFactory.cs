using System;
using System.Collections.Generic;
using System.Threading;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Network.Battle.Config;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using AbilityKit.Protocol.Room;
using AbilityKit.Game.Battle.Agent;
using StateSyncOpCodes = AbilityKit.Protocol.Moba.StateSync.OpCodes;

namespace AbilityKit.Game.Battle
{
    /// <summary>
    /// Builds <see cref="NetworkTransportOptions"/> for the MOBA demo via <see cref="NetworkBattleConfig"/>.
    /// The standard room-gateway protocol (opcodes + auth + ack + resync + command-sequence) is handled by
    /// <see cref="NetworkBattleConfig.UseRoomGatewayProtocol"/>; only the MOBA-specific input/snapshot/frame
    /// serialize/deserialize callbacks are passed as lambdas.
    /// </summary>
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
            string publicRoomId = "",
            bool useFrameSyncInput = false,
            Func<string>? getReliableEventEpoch = null,
            Func<long>? getReliableEventLastAcknowledgedSequence = null)
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

            return new NetworkBattleConfig()
                .WithGateway(host, port)
                .WithTransportFactory(transportFactory)
                .WithSession(sessionToken, battleId, publicRoomId)
                .UseRoomGatewayProtocol(battleId, publicRoomId)
                .WithInputSerializer(
                    serializeSubmitInput: requestObj =>
                    {
                        if (requestObj is not SequencedInput sequenced) return default;
                        var req = sequenced.Request;
                        var pid = playerIdToUInt(req.Input.Player);
                        var wid = worldIdToUlong(req.WorldId);

                        if (useFrameSyncInput)
                        {
                            var frameWire = new WireSubmitFrameInputReq(
                                roomId, wid, pid,
                                req.Input.Frame.Value, req.Input.OpCode,
                                req.Input.Payload ?? Array.Empty<byte>());
                            return WireCustomBinary.Serialize(in frameWire);
                        }

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
                    deserializeSubmitInputResponse: payload =>
                    {
                        if (useFrameSyncInput)
                        {
                            var frameWire = WireCustomBinary.DeserializeSubmitFrameInputRes(payload);
                            var retryAtAuthoritativeFrame = !frameWire.Accepted &&
                                (frameWire.ReasonCode == 3 || frameWire.ReasonCode == 4);
                            return new NetworkSubmitInputResponse(
                                frameWire.Accepted, frameWire.ServerFrame, frameWire.ReasonCode,
                                retryAtAuthoritativeFrame, $"FrameInputReason({frameWire.ReasonCode})");
                        }

                        var wire = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputRes>(payload);
                        return new NetworkSubmitInputResponse(
                            wire.Success, wire.CurrentFrame, wire.Success ? 0 : 1,
                            wire.ShouldResync, wire.Status, wire.Message);
                    })
                .WithSnapshotDeserializer(payload =>
                    {
                        var wire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(payload);
                        return GatewayRoomClient.ToGatewaySnapshot(in wire);
                    })
                .WithFrameDeserializer(payload =>
                    {
                        var push = WireCustomBinary.DeserializeFramePushedPush(payload);
                        var worldId = worldIdFromUlong(push.WorldId);
                        var frame = new FrameIndex(push.Frame);
                        var inputs = (IReadOnlyList<PlayerInputCommand>)(push.Inputs == null || push.Inputs.Length == 0
                            ? Array.Empty<PlayerInputCommand>()
                            : ConvertInputs(frame, push.Inputs, playerIdFromUInt));
                        return new FramePacket(worldId, frame, inputs, snapshot: null);
                    })
                .WithReliableEventCursor(getReliableEventEpoch, getReliableEventLastAcknowledgedSequence)
                .Build();
        }

        private static PlayerInputCommand[] ConvertInputs(FrameIndex frame, WireInputItem[] inputs, Func<uint, PlayerId> playerIdFromUInt)
        {
            var arr = new PlayerInputCommand[inputs.Length];
            for (int i = 0; i < inputs.Length; i++)
            {
                var it = inputs[i];
                arr[i] = new PlayerInputCommand(frame, playerIdFromUInt(it.PlayerId), it.OpCode, it.Payload);
            }
            return arr;
        }
    }
}
