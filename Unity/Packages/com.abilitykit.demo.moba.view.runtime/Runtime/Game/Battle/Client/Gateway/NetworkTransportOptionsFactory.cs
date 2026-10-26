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
    public sealed class NetworkTransportOptionsFactoryOptions
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public Func<ITransport>? TransportFactory { get; set; }
        public NetworkIdCodec IdCodec { get; set; } = NetworkIdCodec.Numeric;
        public ulong RoomId { get; set; }
        public string SessionToken { get; set; } = string.Empty;
        public string BattleId { get; set; } = string.Empty;
        public string PublicRoomId { get; set; } = string.Empty;
        public bool UseFrameSyncInput { get; set; }
        public Func<string>? GetReliableEventEpoch { get; set; }
        public Func<long>? GetReliableEventLastAcknowledgedSequence { get; set; }
    }

    /// <summary>
    /// Builds <see cref="NetworkTransportOptions"/> for the MOBA demo. The shared room-gateway
    /// protocol prefill (gateway/session/protocol preset) comes from
    /// <see cref="MultiplayerClientSession.BuildBattleOptions"/> — the same assembly step the shooter
    /// and console demos use — so all three converge on one protocol-setup source of truth. Only the
    /// MOBA-specific callbacks (framesync/statesync input, snapshot+frame deserializers, reliable-event
    /// cursor) are added here. The shared host builds MOBA's transport with dual dispatchers;
    /// the battle session owns the returned transport.
    /// </summary>
    public static class NetworkTransportOptionsFactory
    {
        public static NetworkTransportOptions Create(NetworkTransportOptionsFactoryOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var host = options.Host;
            var port = options.Port;
            var transportFactory = options.TransportFactory;
            var idCodec = options.IdCodec ?? throw new ArgumentException("Network ID codec is required.", nameof(options));
            var roomId = options.RoomId;
            var sessionToken = options.SessionToken;
            var battleId = options.BattleId;
            var publicRoomId = options.PublicRoomId;
            var useFrameSyncInput = options.UseFrameSyncInput;
            var getReliableEventEpoch = options.GetReliableEventEpoch;
            var getReliableEventLastAcknowledgedSequence = options.GetReliableEventLastAcknowledgedSequence;
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
            if (port <= 0) throw new ArgumentOutOfRangeException(nameof(port));
            if (transportFactory == null) throw new ArgumentNullException(nameof(transportFactory));
            if (string.IsNullOrWhiteSpace(battleId))
            {
                throw new ArgumentException("Battle id is required for authoritative input submission.", nameof(battleId));
            }

            // Shared prefill (gateway address + session identity + room-gateway protocol preset) via the
            // same static assembly step the other demos use — see MultiplayerClientSession.BuildBattleOptions.
            var session = new MultiplayerRoomSessionResult(
                sessionToken, publicRoomId, battleId, roomId, playerId: 0u, roomSnapshot: default, subscribed: false);

            return MultiplayerClientSession.BuildBattleOptions(
                in session,
                host,
                port,
                battleTransportFactory: transportFactory,
                configureBattle: (config, s) =>
                {
                    if (useFrameSyncInput)
                    {
                        config
                            .WithInputOpCode(OpCodes.SubmitFrameInput)
                            .WithInputSerializer(
                            serializeSubmitInput: requestObj =>
                            {
                                if (requestObj is not SequencedInput sequenced) return default;
                                var req = sequenced.Request;
                                var frameWire = new WireSubmitFrameInputReq(
                                    s.NumericRoomId,
                                    idCodec.Encode(req.WorldId),
                                    idCodec.Encode(req.Input.Player),
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
                        // StateSync treats ShouldResync as a full-state recovery signal, not as an
                        // authoritative-frame retry request. Engine-level input retry stays disabled here.
                        config.UseRoomGatewayStateSyncInput(
                            s.BattleId,
                            idCodec.Encode,
                            idCodec.Encode);
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
                            var worldId = idCodec.DecodeWorld(push.WorldId);
                            var frame = new FrameIndex(push.Frame);
                            var inputs = (IReadOnlyList<PlayerInputCommand>)(push.Inputs == null || push.Inputs.Length == 0
                                ? Array.Empty<PlayerInputCommand>()
                                : ConvertInputs(frame, push.Inputs, idCodec));
                            return new FramePacket(worldId, frame, inputs, snapshot: null);
                        })
                        .WithReliableEventCursor(getReliableEventEpoch, getReliableEventLastAcknowledgedSequence);
                });
        }

        private static PlayerInputCommand[] ConvertInputs(FrameIndex frame, WireInputItem[] inputs, NetworkIdCodec idCodec)
        {
            var arr = new PlayerInputCommand[inputs.Length];
            for (int i = 0; i < inputs.Length; i++)
            {
                var it = inputs[i];
                arr[i] = new PlayerInputCommand(frame, idCodec.DecodePlayer(it.PlayerId), it.OpCode, it.Payload);
            }
            return arr;
        }
    }
}
