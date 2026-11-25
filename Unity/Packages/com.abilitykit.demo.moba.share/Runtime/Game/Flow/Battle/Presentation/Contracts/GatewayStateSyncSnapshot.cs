using System;

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
        public readonly bool UsesFixedBaseline;
        public readonly System.Guid StateStreamGeneration;
        public readonly long SnapshotId;
        public readonly long BaseSnapshotId;
        /// <summary>
        /// 本帧完整活动 Cue 集；没有负载与携带空活动集的结束标记不同。
        /// </summary>
        public readonly byte[] PresentationCueBaseline;
        /// <summary>
        /// 与权威状态同帧的完整头顶指示器、护盾表现基线；缺失不等于空集合。
        /// </summary>
        public readonly byte[] ActorIndicatorBaseline, ShieldStateBaseline;
        /// <summary>
        /// 同修订的主控移动证明；存在不表示世界已经提交。
        /// </summary>
        public readonly GatewayStateSyncMovementProof? MovementProof;

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
            long serverTicks = 0L,
            bool usesFixedBaseline = false,
            System.Guid stateStreamGeneration = default,
            long snapshotId = 0L,
            long baseSnapshotId = 0L,
            byte[] presentationCueBaseline = null,
            byte[] actorIndicatorBaseline = null,
            byte[] shieldStateBaseline = null,
            GatewayStateSyncMovementProof? movementProof = null)
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
            UsesFixedBaseline = usesFixedBaseline;
            StateStreamGeneration = stateStreamGeneration;
            SnapshotId = snapshotId;
            BaseSnapshotId = baseSnapshotId;
            PresentationCueBaseline = presentationCueBaseline;
            ActorIndicatorBaseline = actorIndicatorBaseline; ShieldStateBaseline = shieldStateBaseline;
            MovementProof = movementProof;
        }
    }

    /// <summary>
    /// 供状态同步会话使用的类型化权威锚点，不含插值或帧同步游标。
    /// </summary>
    public readonly struct GatewayStateSyncMovementProof
    {
        public readonly Guid InputGeneration, StreamGeneration;
        public readonly uint PlayerId;
        public readonly ulong WorldId;
        public readonly long SnapshotId, BaseSnapshotId, TerminalThrough;
        public readonly int Tick, ActorId, EntityVersion;
        public readonly float X, Y, Z, RotationX, RotationY, RotationZ, RotationW, MoveX, MoveZ, MoveSpeed;
        public readonly bool CanMove, HasIsolatedLocomotion;

        /// <summary>
        /// 保存复制自同一权威截面的值，不产生世界写入或输入确认副作用。
        /// </summary>
        public GatewayStateSyncMovementProof(Guid inputGeneration, uint playerId, ulong worldId,
            Guid streamGeneration, long snapshotId, long baseSnapshotId, int tick, long terminalThrough,
            int actorId, int entityVersion, float x, float y, float z,
            float rotationX, float rotationY, float rotationZ, float rotationW,
            float moveX, float moveZ, float moveSpeed, bool canMove, bool hasIsolatedLocomotion)
        {
            InputGeneration = inputGeneration; PlayerId = playerId; WorldId = worldId;
            StreamGeneration = streamGeneration; SnapshotId = snapshotId; BaseSnapshotId = baseSnapshotId;
            Tick = tick; TerminalThrough = terminalThrough; ActorId = actorId; EntityVersion = entityVersion;
            X = x; Y = y; Z = z; RotationX = rotationX; RotationY = rotationY; RotationZ = rotationZ; RotationW = rotationW;
            MoveX = moveX; MoveZ = moveZ; MoveSpeed = moveSpeed; CanMove = canMove; HasIsolatedLocomotion = hasIsolatedLocomotion;
        }

        /// <summary>
        /// 只验证证明与当前固定基线修订绑定；调用方仍需确认会话身份和提交顺序。
        /// </summary>
        public bool Matches(in GatewayStateSyncSnapshot snapshot) => snapshot.UsesFixedBaseline && WorldId != 0 &&
            InputGeneration != Guid.Empty && PlayerId != 0 && StreamGeneration != Guid.Empty && SnapshotId > 0 &&
            BaseSnapshotId >= 0 && BaseSnapshotId < SnapshotId && TerminalThrough > 0 && ActorId > 0 && EntityVersion > 0 &&
            snapshot.WorldId == WorldId && snapshot.Frame == Tick && snapshot.StateStreamGeneration == StreamGeneration &&
            snapshot.SnapshotId == SnapshotId && snapshot.BaseSnapshotId == BaseSnapshotId &&
            Finite(X) && Finite(Y) && Finite(Z) && Finite(RotationX) && Finite(RotationY) && Finite(RotationZ) &&
            Finite(RotationW) && Finite(MoveX) && Finite(MoveZ) && Finite(MoveSpeed) && MoveSpeed >= 0;

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
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
