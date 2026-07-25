using System;
using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Protocol.Shooter
{
    [MemoryPackable]
    public partial struct ShooterPlayerSnapshot
    {
        [MemoryPackOrder(0)] public int PlayerId;
        [MemoryPackOrder(1)] public float X;
        [MemoryPackOrder(2)] public float Y;
        [MemoryPackOrder(3)] public float AimX;
        [MemoryPackOrder(4)] public float AimY;
        [MemoryPackOrder(5)] public int Hp;
        [MemoryPackOrder(6)] public int Score;
        [MemoryPackOrder(7)] public bool Alive;

        public ShooterPlayerSnapshot(int playerId, float x, float y, float aimX, float aimY, int hp, int score, bool alive)
        {
            PlayerId = playerId;
            X = x;
            Y = y;
            AimX = aimX;
            AimY = aimY;
            Hp = hp;
            Score = score;
            Alive = alive;
        }
    }

    [MemoryPackable]
    public partial struct ShooterBulletSnapshot
    {
        [MemoryPackOrder(0)] public int BulletId;
        [MemoryPackOrder(1)] public int OwnerPlayerId;
        [MemoryPackOrder(2)] public float X;
        [MemoryPackOrder(3)] public float Y;
        [MemoryPackOrder(4)] public float VelocityX;
        [MemoryPackOrder(5)] public float VelocityY;
        [MemoryPackOrder(6)] public int RemainingFrames;

        public ShooterBulletSnapshot(int bulletId, int ownerPlayerId, float x, float y, float velocityX, float velocityY, int remainingFrames)
        {
            BulletId = bulletId;
            OwnerPlayerId = ownerPlayerId;
            X = x;
            Y = y;
            VelocityX = velocityX;
            VelocityY = velocityY;
            RemainingFrames = remainingFrames;
        }
    }

    [MemoryPackable]
    public partial struct ShooterEnemySnapshot
    {
        [MemoryPackOrder(0)] public int EnemyId;
        [MemoryPackOrder(1)] public float X;
        [MemoryPackOrder(2)] public float Y;
        [MemoryPackOrder(3)] public float FacingX;
        [MemoryPackOrder(4)] public float FacingY;
        [MemoryPackOrder(5)] public int Hp;
        [MemoryPackOrder(6)] public int MaxHp;
        [MemoryPackOrder(7)] public bool Alive;

        public ShooterEnemySnapshot(int enemyId, float x, float y, float facingX, float facingY, int hp, int maxHp, bool alive)
        {
            EnemyId = enemyId;
            X = x;
            Y = y;
            FacingX = facingX;
            FacingY = facingY;
            Hp = hp;
            MaxHp = maxHp;
            Alive = alive;
        }
    }

    public enum ShooterEventType
    {
        Hit = 1,
        Fire = 2,
        MatchVictory = 3,
        MatchDefeat = 4,
        MatchEnded = 5
    }

    [MemoryPackable]
    public partial struct ShooterEventSnapshot
    {
        [MemoryPackOrder(0)] public int EventType;
        [MemoryPackOrder(1)] public int SourcePlayerId;
        [MemoryPackOrder(2)] public int TargetPlayerId;
        [MemoryPackOrder(3)] public int BulletId;
        [MemoryPackOrder(4)] public float X;
        [MemoryPackOrder(5)] public float Y;
        [MemoryPackOrder(6)] public int Value;

        public ShooterEventSnapshot(int eventType, int sourcePlayerId, int targetPlayerId, int bulletId, float x, float y, int value)
        {
            EventType = eventType;
            SourcePlayerId = sourcePlayerId;
            TargetPlayerId = targetPlayerId;
            BulletId = bulletId;
            X = x;
            Y = y;
            Value = value;
        }

        public ShooterEventSnapshot(ShooterEventType eventType, int sourcePlayerId, int targetPlayerId, int bulletId, float x, float y, int value)
        {
            EventType = (int)eventType;
            SourcePlayerId = sourcePlayerId;
            TargetPlayerId = targetPlayerId;
            BulletId = bulletId;
            X = x;
            Y = y;
            Value = value;
        }
    }

    [MemoryPackable]
    public partial struct ShooterStateSnapshotPayload
    {
        [MemoryPackOrder(0)] public int Frame;
        [MemoryPackOrder(1)] public ShooterPlayerSnapshot[] Players;
        [MemoryPackOrder(2)] public ShooterBulletSnapshot[] Bullets;
        [MemoryPackOrder(3)] public ShooterEventSnapshot[] Events;
        [MemoryPackOrder(4)] public int MatchState;
        [MemoryPackOrder(5)] public int TimeLimitFrames;
        [MemoryPackOrder(6)] public int RemainingTimeFrames;
        [MemoryPackOrder(7)] public ShooterEnemySnapshot[] Enemies;

        public ShooterStateSnapshotPayload(int frame, ShooterPlayerSnapshot[] players, ShooterBulletSnapshot[] bullets, ShooterEventSnapshot[] events)
            : this(frame, players, bullets, events, matchState: 0, timeLimitFrames: 0, remainingTimeFrames: 0, enemies: Array.Empty<ShooterEnemySnapshot>())
        {
        }

        public ShooterStateSnapshotPayload(
            int frame,
            ShooterPlayerSnapshot[] players,
            ShooterBulletSnapshot[] bullets,
            ShooterEventSnapshot[] events,
            int matchState,
            int timeLimitFrames,
            int remainingTimeFrames)
            : this(frame, players, bullets, events, matchState, timeLimitFrames, remainingTimeFrames, Array.Empty<ShooterEnemySnapshot>())
        {
        }

        [MemoryPackConstructor]
        public ShooterStateSnapshotPayload(
            int frame,
            ShooterPlayerSnapshot[] players,
            ShooterBulletSnapshot[] bullets,
            ShooterEventSnapshot[] events,
            int matchState,
            int timeLimitFrames,
            int remainingTimeFrames,
            ShooterEnemySnapshot[] enemies)
        {
            Frame = frame;
            Players = players;
            Bullets = bullets;
            Events = events;
            MatchState = matchState;
            TimeLimitFrames = timeLimitFrames < 0 ? 0 : timeLimitFrames;
            RemainingTimeFrames = remainingTimeFrames < 0 ? 0 : remainingTimeFrames;
            Enemies = enemies;
        }
    }

    public static class ShooterStateSnapshotCodec
    {
        public static byte[] Serialize(in ShooterStateSnapshotPayload snapshot)
        {
            return MemoryPackSerializer.Serialize(snapshot);
        }

        public static byte[] SerializeEvent(in ShooterEventSnapshot battleEvent)
        {
            return MemoryPackSerializer.Serialize(battleEvent);
        }

        public static ShooterEventSnapshot DeserializeEvent(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                throw new ArgumentException("Reliable battle event payload is required.", nameof(payload));
            }

            return MemoryPackSerializer.Deserialize<ShooterEventSnapshot>(payload);
        }

        public static ShooterStateSnapshotPayload Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return new ShooterStateSnapshotPayload(0, Array.Empty<ShooterPlayerSnapshot>(), Array.Empty<ShooterBulletSnapshot>(), Array.Empty<ShooterEventSnapshot>());
            }

            var value = MemoryPackSerializer.Deserialize<ShooterStateSnapshotPayload>(payload);
            return new ShooterStateSnapshotPayload(
                value.Frame,
                value.Players ?? Array.Empty<ShooterPlayerSnapshot>(),
                value.Bullets ?? Array.Empty<ShooterBulletSnapshot>(),
                value.Events ?? Array.Empty<ShooterEventSnapshot>(),
                value.MatchState,
                value.TimeLimitFrames,
                value.RemainingTimeFrames,
                value.Enemies ?? Array.Empty<ShooterEnemySnapshot>());
        }
    }
}
