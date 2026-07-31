namespace AbilityKit.Game.Battle.Agent
{
    public readonly struct GatewayCreateRoomResult
    {
        public readonly string RoomId;
        public readonly ulong NumericRoomId;

        public GatewayCreateRoomResult(string roomId, ulong numericRoomId)
        {
            RoomId = roomId;
            NumericRoomId = numericRoomId;
        }
    }

    public readonly struct GatewayJoinRoomResult
    {
        public readonly ulong NumericRoomId;
        public readonly string SnapshotJson;
        public readonly GatewayWorldStartAnchor WorldStartAnchor;

        public GatewayJoinRoomResult(ulong numericRoomId, string snapshotJson, in GatewayWorldStartAnchor worldStartAnchor)
        {
            NumericRoomId = numericRoomId;
            SnapshotJson = snapshotJson;
            WorldStartAnchor = worldStartAnchor;
        }
    }

    public readonly struct GatewayWorldStartAnchor
    {
        public readonly long StartServerTicks;
        public readonly long ServerTickFrequency;
        public readonly int StartFrame;
        public readonly double FixedDeltaSeconds;

        public GatewayWorldStartAnchor(long startServerTicks, long serverTickFrequency, int startFrame, double fixedDeltaSeconds)
        {
            StartServerTicks = startServerTicks;
            ServerTickFrequency = serverTickFrequency;
            StartFrame = startFrame;
            FixedDeltaSeconds = fixedDeltaSeconds;
        }
    }

    public readonly struct GatewayRoomSnapshotResult
    {
        public readonly bool Success;
        public readonly bool Applied;
        public readonly int ErrorCode;
        public readonly string Message;
        public readonly string RoomId;
        public readonly ulong NumericRoomId;

        public GatewayRoomSnapshotResult(string roomId, ulong numericRoomId)
            : this(true, true, 0, string.Empty, roomId, numericRoomId)
        {
        }

        public GatewayRoomSnapshotResult(
            bool success,
            bool applied,
            int errorCode,
            string message,
            string roomId,
            ulong numericRoomId)
        {
            Success = success;
            Applied = applied;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
        }
    }

    public readonly struct GatewayStartBattleResult
    {
        public readonly string BattleId;
        public readonly ulong WorldId;
        public readonly bool Started;

        public GatewayStartBattleResult(string battleId, ulong worldId, bool started)
        {
            BattleId = battleId;
            WorldId = worldId;
            Started = started;
        }
    }

    public readonly struct GatewayBattleInputResult
    {
        public readonly int AcceptedFrame;
        public readonly bool Success;
        public readonly int CurrentFrame;
        public readonly string Status;
        public readonly string Message;
        public readonly bool ShouldResync;
        public readonly long ServerTicks;
        public readonly ulong CommandSequence;

        public GatewayBattleInputResult(int acceptedFrame, bool success)
            : this(acceptedFrame, success, acceptedFrame, string.Empty, string.Empty, !success, 0L, 0UL)
        {
        }

        public GatewayBattleInputResult(
            int acceptedFrame,
            bool success,
            int currentFrame,
            string status,
            string message,
            bool shouldResync,
            long serverTicks,
            ulong commandSequence)
        {
            AcceptedFrame = acceptedFrame;
            Success = success;
            CurrentFrame = currentFrame;
            Status = status ?? string.Empty;
            Message = message ?? string.Empty;
            ShouldResync = shouldResync;
            ServerTicks = serverTicks;
            CommandSequence = commandSequence;
        }
    }

    public readonly struct GatewayStateSyncSubscriptionResult
    {
        public readonly bool Success;

        public GatewayStateSyncSubscriptionResult(bool success)
        {
            Success = success;
        }
    }

    public readonly struct GatewayStateSyncSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        public readonly ulong WorldId;
        public readonly int Frame;
        public readonly double Timestamp;
        public readonly bool IsFullSnapshot;
        public readonly GatewayStateSyncActorSnapshot[] Actors;
        public readonly int SchemaVersion;
        public readonly int[] RemovedActorIds;
        public readonly long EventWatermark;
        public readonly string EventEpoch;

        public GatewayStateSyncSnapshot(
            ulong worldId,
            int frame,
            double timestamp,
            bool isFullSnapshot,
            GatewayStateSyncActorSnapshot[] actors,
            int schemaVersion = 0,
            int[] removedActorIds = null,
            long eventWatermark = 0L,
            string eventEpoch = null)
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
        }
    }

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

        public GatewayStateSyncActorSnapshot(int actorId, float x, float y, float z, float rotation, float velocityX, float velocityZ, float hp, float hpMax, int teamId, int kind = 0, int code = 0, int ownerNetId = 0)
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

    public readonly struct GatewayTimeSyncResult
    {
        public readonly long ClientSendTicks;
        public readonly long ServerNowTicks;
        public readonly long ServerTickFrequency;

        public GatewayTimeSyncResult(long clientSendTicks, long serverNowTicks, long serverTickFrequency)
        {
            ClientSendTicks = clientSendTicks;
            ServerNowTicks = serverNowTicks;
            ServerTickFrequency = serverTickFrequency;
        }
    }
}
