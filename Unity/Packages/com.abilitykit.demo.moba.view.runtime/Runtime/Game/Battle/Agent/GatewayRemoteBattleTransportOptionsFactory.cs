using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Game.Battle.Transport;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;

namespace AbilityKit.Game.Battle
{
    public static class GatewayRemoteBattleTransportOptionsFactory
    {
        public static GatewayBattleLogicTransportOptions Create(
            string host,
            int port,
            Func<ITransport> transportFactory,
            Func<PlayerId, uint> playerIdToUInt,
            Func<uint, PlayerId> playerIdFromUInt,
            Func<WorldId, ulong> worldIdToUlong,
            Func<ulong, WorldId> worldIdFromUlong,
            ulong roomId,
            string sessionToken)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
            if (port <= 0) throw new ArgumentOutOfRangeException(nameof(port));
            if (transportFactory == null) throw new ArgumentNullException(nameof(transportFactory));
            if (playerIdToUInt == null) throw new ArgumentNullException(nameof(playerIdToUInt));
            if (playerIdFromUInt == null) throw new ArgumentNullException(nameof(playerIdFromUInt));
            if (worldIdToUlong == null) throw new ArgumentNullException(nameof(worldIdToUlong));
            if (worldIdFromUlong == null) throw new ArgumentNullException(nameof(worldIdFromUlong));

            return new GatewayBattleLogicTransportOptions
            {
                Host = host,
                Port = port,
                TransportFactory = transportFactory,
                FrameCodec = LengthPrefixedFrameCodec.Instance,

                OpRenewSession = 120,
                SessionToken = sessionToken,

                OpSubmitInput = OpCodes.SubmitFrameInput,
                OpFramePushed = OpCodes.FramePushed,

                // Not wired yet (room flow will own these later).
                OpCreateWorld = 0,
                OpJoin = 0,
                OpLeave = 0,

                SerializeSubmitInput = requestObj =>
                {
                    if (requestObj is not SubmitInputRequest req) return default;

                    var pid = playerIdToUInt(req.Input.Player);
                    var wid = worldIdToUlong(req.WorldId);

                    var wire = new WireSubmitFrameInputReq(
                        roomId: roomId,
                        worldId: wid,
                        playerId: pid,
                        frame: req.Input.Frame.Value,
                        inputOpCode: req.Input.OpCode,
                        inputPayload: req.Input.Payload);

                    return WireCustomBinary.Serialize(in wire);
                },

                DeserializeFramePushed = payload =>
                {
                    var push = WireCustomBinary.DeserializeFramePushedPush(payload);

                    var worldId = worldIdFromUlong(push.WorldId);
                    var frame = new FrameIndex(push.Frame);

                    var inputs = (IReadOnlyList<PlayerInputCommand>)(push.Inputs == null || push.Inputs.Length == 0
                        ? Array.Empty<PlayerInputCommand>()
                        : ConvertInputs(frame, push.Inputs, playerIdFromUInt));

                    return new FramePacket(worldId, frame, inputs, snapshot: null);
                }
            };
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
