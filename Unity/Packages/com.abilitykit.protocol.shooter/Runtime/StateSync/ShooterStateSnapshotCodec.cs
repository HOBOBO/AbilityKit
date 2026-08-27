using System;
using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Protocol.Shooter
{
    public partial struct ShooterPlayerSnapshot
    {
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

    public partial struct ShooterBulletSnapshot
    {
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

    public partial struct ShooterEnemySnapshot
    {
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

    public partial struct ShooterEventSnapshot
    {
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

    public partial struct ShooterStateSnapshotPayload
    {
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
