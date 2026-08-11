using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Network.Battle.Config;
using AbilityKit.Network.Client;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Room;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Game.Battle
{
    /// <summary>
    /// Builds <see cref="NetworkTransportOptions"/> for the MOBA demo. The shared room-gateway
    /// protocol prefill (gateway/session/protocol preset) comes from
    /// <see cref="GatewayBattleClientHost.BuildBattleOptions"/> — the same assembly step the shooter
    /// and console demos use — so all three converge on one protocol-setup source of truth. Only the
    /// MOBA-specific callbacks (framesync/statesync input, snapshot+frame deserializers, reliable-event
    /// cursor) are added here. MOBA keeps its own <see cref="NetworkTransport"/> construction with dual
    /// dispatchers (its battle session owns the lifecycle), so only the static options-assembly step is
    /// shared, not the host's lifecycle.
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

            // Shared prefill (gateway address + session identity + room-gateway protocol preset) via the
            // same static assembly step the other demos use — see GatewayBattleClientHost.BuildBattleOptions.
            var session = new GatewaySessionResult(
                sessionToken, publicRoomId, battleId, roomId, playerId: 0u, roomSnapshot: default, subscribed: false);

            return GatewayBattleClientHost.BuildBattleOptions(
                in session,
                host,
                port,
                battleTransportFactory: transportFactory,
                configureBattle: (config, s) =>
                {
                    if (useFrameSyncInput)
                    {
                        config.WithInputSerializer(
                            serializeSubmitInput: requestObj =>
                            {
                                if (requestObj is not SequencedInput sequenced) return default;
                                var req = sequenced.Request;
                                var frameWire = new WireSubmitFrameInputReq(
                                    s.NumericRoomId,
                                    worldIdToUlong(req.WorldId),
                                    playerIdToUInt(req.Input.Player),
                                    req.Input.Frame.Value,
                                    req.Input.OpCode,
                                    req.Input.Payload ?? Array.Empty<byte>());
                                return WireCustomBinary.Serialize(in frameWire);
                            },
                            deserializeSubmitInputResponse: payload =>
                            {
                                var frameWire = WireCustomBinary.DeserializeSubmitFrameInputRes(payload);
                                var retryAtAuthoritativeFrame = !frameWire.Accepted &&
                                    (frameWire.ReasonCode == 3 || frameWire.ReasonCode == 4);
                                return new NetworkSubmitInputResponse(
                                    frameWire.Accepted, frameWire.ServerFrame, frameWire.ReasonCode,
                                    retryAtAuthoritativeFrame, $"FrameInputReason({frameWire.ReasonCode})");
                            });
                    }
                    else
                    {
                        // Standard room-gateway StateSync input preset. NOTE: retryAtAuthoritativeFrame is
                        // mapped from ShouldResync for historical compatibility; new consumers usually
                        // leave the engine retry off.
                        config.UseRoomGatewayStateSyncInput(
                            s.BattleId,
                            playerIdToUInt,
                            worldIdToUlong,
                            retryAtAuthoritativeFrame: wire => wire.ShouldResync);
                    }

                    config
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
                        .WithReliableEventCursor(getReliableEventEpoch, getReliableEventLastAcknowledgedSequence);
                });
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
