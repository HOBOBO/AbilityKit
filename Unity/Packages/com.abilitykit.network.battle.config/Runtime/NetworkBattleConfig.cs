using System;
using System.Threading;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Network.Battle.Config
{
    /// <summary>
    /// High-level fluent builder that produces a <see cref="NetworkTransportOptions"/> with sensible defaults
    /// and the standard room-gateway protocol preset (opcodes + auth + ack + resync). The game provides ONLY
    /// its game-specific callbacks (input serialize/deserialize + snapshot deserialize). Everything else is
    /// handled by the builder.
    ///
    /// Reduces a ~30-line options initializer to ~7 lines:
    /// <code>
    /// var options = new NetworkBattleConfig()
    ///     .WithGateway(host, port)
    ///     .WithTcpTransport()
    ///     .WithSession(token, battleId, roomId, worldId)
    ///     .UseRoomGatewayProtocol()
    ///     .WithInputSerializer(serialize, deserializeResponse)
    ///     .WithSnapshotDeserializer(deserializeSnapshot)
    ///     .Build();
    /// </code>
    /// </summary>
    public sealed class NetworkBattleConfig
    {
        private readonly NetworkTransportOptions _o = new()
        {
            FrameCodec = LengthPrefixedFrameCodec.Instance,
            SubmitInputRetryFrameLead = 2,
        };

        private long _nextCommandSequence;
        private bool _protocolPresetApplied;

        // ============== Common ==============

        /// <summary>Sets the gateway host + port.</summary>
        public NetworkBattleConfig WithGateway(string host, int port)
        {
            _o.Host = host ?? throw new ArgumentNullException(nameof(host));
            _o.Port = port;
            return this;
        }

        /// <summary>Uses a fresh TcpTransport as the transport factory.</summary>
        public NetworkBattleConfig WithTcpTransport()
        {
            _o.TransportFactory = () => new TcpTransport();
            return this;
        }

        /// <summary>Uses a custom transport factory.</summary>
        public NetworkBattleConfig WithTransportFactory(Func<ITransport> factory)
        {
            _o.TransportFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            return this;
        }

        /// <summary>Injects an existing IConnection (1-connection topology; TransportFactory is ignored).</summary>
        public NetworkBattleConfig WithInjectedConnection(Func<IConnection> factory)
        {
            _o.ConnectionFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            return this;
        }

        /// <summary>Sets session identity (token + battle/room/world for routing).</summary>
        public NetworkBattleConfig WithSession(string sessionToken, string battleId, string roomId = "", ulong worldId = 0)
        {
            _o.SessionToken = sessionToken ?? throw new ArgumentNullException(nameof(sessionToken));
            _o.OpRenewSession = RoomGatewayOpCodes.RenewSession;
            _o.SerializeRenewSession = token => WireRoomGatewayBinary.Serialize(
                new WireRenewSessionReq { SessionToken = token, ExtendSeconds = 0, RotateToken = false });
            return this;
        }

        // ============== Protocol Preset ==============

        /// <summary>
        /// Applies the standard room-gateway protocol: all opcodes + auth handshake (RenewSession→SubscribeStateSync)
        /// + reliable-event ack + full-state-sync request + reliable-event push deserialize.
        /// The game only needs to add input serialize/deserialize + snapshot deserialize.
        /// </summary>
        public NetworkBattleConfig UseRoomGatewayProtocol(string battleId, string roomId = "")
        {
            // Opcodes
            _o.OpSubmitInput = RoomGatewayOpCodes.SubmitBattleInput;
            _o.OpSnapshotPushed = RoomGatewayOpCodes.SnapshotPushed;
            _o.OpDeltaSnapshotPushed = RoomGatewayOpCodes.DeltaSnapshotPushed;
            _o.OpReliableEventsPushed = RoomGatewayOpCodes.ReliableBattleEventsPushed;
            _o.OpAcknowledgeReliableEvents = RoomGatewayOpCodes.AckReliableBattleEvents;
            _o.OpRequestFullStateSync = RoomGatewayOpCodes.RequestFullStateSync;
            _o.OpRenewSession = RoomGatewayOpCodes.RenewSession;
            _o.OpPostAuthentication = RoomGatewayOpCodes.SubscribeStateSync;

            // Auth handshake (standard room-gateway)
            _o.SerializeRenewSession = token => WireRoomGatewayBinary.Serialize(
                new WireRenewSessionReq { SessionToken = token, ExtendSeconds = 0, RotateToken = false });
            _o.SerializePostAuthenticationWithReliableEventCursor = (epoch, lastAck) => WireRoomGatewayBinary.Serialize(
                new WireSubscribeStateSyncReq
                {
                    SessionToken = _o.SessionToken,
                    BattleId = battleId,
                    RoomId = roomId,
                    EventEpoch = epoch ?? string.Empty,
                    LastEventAck = Math.Max(0L, lastAck)
                });

            // Reliable-event ack (standard)
            _o.SerializeAcknowledgeReliableEvents = (epoch, sequence) => WireRoomGatewayBinary.Serialize(
                new WireAckReliableBattleEventsReq
                {
                    SessionToken = _o.SessionToken,
                    BattleId = battleId,
                    RoomId = roomId,
                    Epoch = epoch ?? string.Empty,
                    AckSequence = Math.Max(0L, sequence)
                });
            _o.DeserializeAcknowledgeReliableEventsResponse = payload =>
            {
                var wire = WireRoomGatewayBinary.Deserialize<WireAckReliableBattleEventsRes>(payload);
                return wire.Success ? wire.AcceptedAckSequence : -1L;
            };

            // Reliable-event push deserialize (standard)
            _o.DeserializeReliableEventsPushed = payload =>
                WireRoomGatewayBinary.Deserialize<WireReliableBattleEventPush>(payload);

            // Full-state-sync request (standard)
            _o.SerializeRequestFullStateSync = (reason, lastAuthoritativeFrame) => WireRoomGatewayBinary.Serialize(
                new WireRequestFullStateSyncReq
                {
                    SessionToken = _o.SessionToken,
                    BattleId = battleId,
                    RoomId = roomId,
                    WorldId = 0,
                    ClientFrame = lastAuthoritativeFrame,
                    LastAuthoritativeFrame = lastAuthoritativeFrame,
                    ClientStateHash = 0,
                    AuthoritativeStateHash = 0,
                    Reason = reason ?? string.Empty
                });
            _o.DeserializeRequestFullStateSyncResponse = payload =>
            {
                var wire = WireRoomGatewayBinary.Deserialize<WireRequestFullStateSyncRes>(payload);
                return wire.Success && wire.Accepted;
            };

            // Command-sequence wrapping for input (standard)
            _o.PrepareSubmitInput = requestObj =>
            {
                if (requestObj is not SubmitInputRequest req)
                    throw new ArgumentException("Expected SubmitInputRequest.", nameof(requestObj));
                return new SequencedSubmitInputRequest(req, unchecked((ulong)Interlocked.Increment(ref _nextCommandSequence)));
            };
            _o.RewriteSubmitInputFrame = (requestObj, frame) =>
            {
                if (requestObj is not SequencedSubmitInputRequest seq)
                    throw new ArgumentException("Expected SequencedSubmitInputRequest.", nameof(requestObj));
                var r = seq.Request;
                return new SequencedSubmitInputRequest(
                    new SubmitInputRequest(r.WorldId,
                        new AbilityKit.Ability.Host.PlayerInputCommand(
                            new AbilityKit.Ability.FrameSync.FrameIndex(frame),
                            r.Input.Player, r.Input.OpCode, r.Input.Payload)),
                    seq.CommandSequence);
            };

            _protocolPresetApplied = true;
            return this;
        }

        // ============== Game-specific callbacks ==============

        /// <summary>Sets the game-specific input serialize + response deserialize.</summary>
        public NetworkBattleConfig WithInputSerializer(
            Func<object, ArraySegment<byte>> serializeSubmitInput,
            Func<ArraySegment<byte>, NetworkSubmitInputResponse> deserializeSubmitInputResponse)
        {
            _o.SerializeSubmitInput = serializeSubmitInput ?? throw new ArgumentNullException(nameof(serializeSubmitInput));
            _o.DeserializeSubmitInputResponse = deserializeSubmitInputResponse
                ?? throw new ArgumentNullException(nameof(deserializeSubmitInputResponse));
            return this;
        }

        /// <summary>Sets the game-specific snapshot push deserializer (returns a decoded object for StateSyncSnapshotPushed).</summary>
        public NetworkBattleConfig WithSnapshotDeserializer(Func<ArraySegment<byte>, object> deserializeSnapshotPushed)
        {
            _o.DeserializeSnapshotPushed = deserializeSnapshotPushed ?? throw new ArgumentNullException(nameof(deserializeSnapshotPushed));
            return this;
        }

        /// <summary>Sets the reliable-event cursor callbacks (for reconnect resubscribe).</summary>
        public NetworkBattleConfig WithReliableEventCursor(Func<string> getEpoch, Func<long> getLastAck)
        {
            _o.GetReliableEventEpoch = getEpoch;
            _o.GetReliableEventLastAcknowledgedSequence = getLastAck;
            return this;
        }

        // ============== Build ==============

        /// <summary>Validates required fields and returns the assembled <see cref="NetworkTransportOptions"/>.</summary>
        public NetworkTransportOptions Build()
        {
            if (_o.ConnectionFactory == null && _o.TransportFactory == null)
                throw new InvalidOperationException("Set a transport (WithTcpTransport/WithTransportFactory) or injected connection (WithInjectedConnection).");
            if (string.IsNullOrWhiteSpace(_o.SessionToken))
                throw new InvalidOperationException("Set session identity (WithSession).");
            if (!_protocolPresetApplied)
                throw new InvalidOperationException("Apply the protocol preset (UseRoomGatewayProtocol).");
            if (_o.SerializeSubmitInput == null)
                throw new InvalidOperationException("Set input serializer (WithInputSerializer).");
            return _o;
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
