#nullable enable

using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Battle.Config;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Shooter.View
{
    /// <summary>
    /// Builds the <see cref="NetworkTransportOptions"/> that drive shooter's server-authoritative
    /// StateSync battle data-plane over the shared room-gateway wire protocol (<c>protocol.room</c>).
    /// Mirrors moba's <c>NetworkTransportOptionsFactory</c>: the standard room-gateway protocol
    /// (opcodes + auth + ack + resync + command-sequence) comes from
    /// <see cref="NetworkBattleConfig.UseRoomGatewayProtocol"/>; only the shooter-specific input
    /// serialize/deserialize callbacks are passed in.
    /// </summary>
    /// <remarks>
    /// Shooter is a <b>raw downlink consumer</b>: it subscribes <see cref="NetworkTransport.RawServerPushReceived"/>
    /// and feeds the original (opCode, payload) straight into <c>ShooterClientSession.ApplyGatewayPush</c>.
    /// The typed snapshot/reliable-event deserializers are therefore left null so the engine's typed
    /// handlers short-circuit (no double-decode). Input uplink uses the awaitable
    /// <see cref="NetworkTransport.SendInputAsync"/> form because shooter's lag-compensation health
    /// events consume the per-submit <c>AcceptedFrame</c>/<c>ServerTicks</c>/<c>ShouldResync</c>.
    /// Engine-level input retry is disabled (<c>RetryAtAuthoritativeFrame=false</c>); shooter keeps its
    /// own <c>RejectedTooFarFuture</c> retry as the sole retry.
    /// </remarks>
    public static class ShooterNetworkTransportOptionsFactory
    {
        public static NetworkTransportOptions Create(
            string host,
            int port,
            Func<ITransport> transportFactory,
            Func<PlayerId, uint> playerIdToUInt,
            Func<WorldId, ulong> worldIdToUlong,
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

            var options = new NetworkBattleConfig()
                .WithGateway(host, port)
                .WithTransportFactory(transportFactory)
                .WithSession(sessionToken, battleId, publicRoomId)
                .UseRoomGatewayProtocol(battleId, publicRoomId)
                .WithInputSerializer(
                    serializeSubmitInput: requestObj =>
                    {
                        if (requestObj is not SequencedInput sequenced) return default;
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
                    deserializeSubmitInputResponse: payload =>
                    {
                        var wire = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputRes>(payload);
                        // Engine retry disabled (RetryAtAuthoritativeFrame=false): shooter keeps its own
                        // RejectedTooFarFuture retry. ShouldResync is carried as a pure data field for the
                        // client's MarkGatewayInputResyncRequested path.
                        return new NetworkSubmitInputResponse(
                            wire.Success,
                            wire.CurrentFrame,
                            wire.Success ? 0 : 1,
                            retryAtAuthoritativeFrame: false,
                            wire.Status,
                            wire.Message,
                            acceptedFrame: wire.AcceptedFrame,
                            serverTicks: wire.ServerTicks,
                            shouldResync: wire.ShouldResync);
                    })
                .WithReliableEventCursor(getReliableEventEpoch, getReliableEventLastAcknowledgedSequence)
                .Build();

            // Raw downlink consumer: clear the preset's typed reliable-event deserializer so the engine's
            // typed handler short-circuits. Shooter routes all pushes through RawServerPushReceived.
            options.DeserializeReliableEventsPushed = null;

            return options;
        }
    }
}
