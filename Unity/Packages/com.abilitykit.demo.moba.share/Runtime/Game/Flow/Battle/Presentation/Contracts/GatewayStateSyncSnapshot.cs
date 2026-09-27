namespace AbilityKit.Game.Battle.Agent
{
    /// <summary>Platform-neutral authoritative state snapshot received from a gateway.</summary>
    public readonly struct GatewayStateSyncSnapshot
    {
        public const int CurrentSchemaVersion = 3;

        public readonly ulong WorldId;
        public readonly int Frame;
        public readonly double Timestamp;
        public readonly bool IsFullSnapshot;
        public readonly GatewayStateSyncActorSnapshot[] Actors;
        public readonly int SchemaVersion;
        public readonly int[] RemovedActorIds;
        public readonly long EventWatermark;
        public readonly string EventEpoch;
        public readonly int PayloadOpCode;
        public readonly byte[] Payload;
        public readonly long ServerTicks;

        public GatewayStateSyncSnapshot(
            ulong worldId,
            int frame,
            double timestamp,
            bool isFullSnapshot,
            GatewayStateSyncActorSnapshot[] actors,
            int schemaVersion = 0,
            int[] removedActorIds = null,
            long eventWatermark = 0L,
            string eventEpoch = null,
            int payloadOpCode = 0,
            byte[] payload = null,
            long serverTicks = 0L)
        {
            WorldId = worldId;
            Frame = frame;
            Timestamp = timestamp;
            IsFullSnapshot = isFullSnapshot;
            Actors = actors;
            SchemaVersion = schemaVersion;
            RemovedActorIds = removedActorIds ?? System.Array.Empty<int>();
            EventWatermark = System.Math.Max(0L, eventWatermark);
            EventEpoch = eventEpoch ?? string.Empty;
            PayloadOpCode = payloadOpCode;
            Payload = payload ?? System.Array.Empty<byte>();
            ServerTicks = serverTicks;
        }
    }

    /// <summary>Platform-neutral authoritative presentation state for one actor.</summary>
    public readonly struct GatewayStateSyncActorSnapshot
    {
        public readonly int ActorId;
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly float Rotation;
        public readonly float VelocityX;
        public readonly float VelocityZ;
        public readonly float Hp;
        public readonly float HpMax;
        public readonly int TeamId;
        public readonly int Kind;
        public readonly int Code;
        public readonly int OwnerNetId;

        public GatewayStateSyncActorSnapshot(
            int actorId,
            float x,
            float y,
            float z,
            float rotation,
            float velocityX,
            float velocityZ,
            float hp,
            float hpMax,
            int teamId,
            int kind = 0,
            int code = 0,
            int ownerNetId = 0)
        {
            ActorId = actorId;
            X = x;
            Y = y;
            Z = z;
            Rotation = rotation;
            VelocityX = velocityX;
            VelocityZ = velocityZ;
            Hp = hp;
            HpMax = hpMax;
            TeamId = teamId;
            Kind = kind;
            Code = code;
            OwnerNetId = ownerNetId;
        }
    }
}
