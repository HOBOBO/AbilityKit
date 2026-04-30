using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.StateSync.Snapshot
{
    [Serializable]
    public struct EntityStateSnapshot
    {
        public long EntityId;
        public Vec3 position;
        public Quat rotation;
        public Vec3 velocity;
        public byte healthPercent;
        public uint StateFlags;
        public long ActiveAbilityMask;
        public Dictionary<int, float> Cooldowns;
        public Dictionary<int, float> BuffTimers;
        public int TeamId;
        public byte ControlFlags;

        public EntityStateSnapshot(long entityId)
        {
            EntityId = entityId;
            position = Vec3.Zero;
            rotation = Quat.Identity;
            velocity = Vec3.Zero;
            healthPercent = 100;
            StateFlags = 0;
            ActiveAbilityMask = 0;
            Cooldowns = new Dictionary<int, float>();
            BuffTimers = new Dictionary<int, float>();
            TeamId = 0;
            ControlFlags = 0;
        }

        public bool HasStateFlag(uint flag) => (StateFlags & flag) != 0;
        public bool HasAbility(long abilityMask) => (ActiveAbilityMask & abilityMask) != 0;
        public bool IsAlive => healthPercent > 0;
        public bool IsImmobile => (ControlFlags & (byte)EntityControlFlags.Immobile) != 0;
        public bool IsStunned => (ControlFlags & (byte)EntityControlFlags.Stunned) != 0;
        public bool IsInvulnerable => (ControlFlags & (byte)EntityControlFlags.Invulnerable) != 0;
    }

    [Flags]
    public enum EntityControlFlags : byte
    {
        None = 0,
        Immobile = 1 << 0,
        Stunned = 1 << 1,
        Invulnerable = 1 << 2,
        Silenced = 1 << 3,
        Disarmed = 1 << 4,
        Rooted = 1 << 5,
        Feared = 1 << 6,
        Sleeping = 1 << 7,
    }

    [Serializable]
    public struct Vec3
    {
        public float X, Y, Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly Vec3 Zero = new Vec3(0f, 0f, 0f);
        public static readonly Vec3 One = new Vec3(1f, 1f, 1f);
        public static readonly Vec3 Up = new Vec3(0f, 1f, 0f);

        public float MagnitudeSquared() => X * X + Y * Y + Z * Z;
        public float Magnitude() => (float)System.Math.Sqrt(MagnitudeSquared());

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator /(Vec3 a, float s) => new Vec3(a.X / s, a.Y / s, a.Z / s);

        public bool ApproximatelyEquals(Vec3 other, float epsilon = 0.0001f)
        {
            return System.Math.Abs(X - other.X) < epsilon &&
                   System.Math.Abs(Y - other.Y) < epsilon &&
                   System.Math.Abs(Z - other.Z) < epsilon;
        }
    }

    [Serializable]
    public struct Quat
    {
        public float X, Y, Z, W;

        public Quat(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static readonly Quat Identity = new Quat(0f, 0f, 0f, 1f);

        public bool ApproximatelyEquals(Quat other, float epsilon = 0.0001f)
        {
            return System.Math.Abs(X - other.X) < epsilon &&
                   System.Math.Abs(Y - other.Y) < epsilon &&
                   System.Math.Abs(Z - other.Z) < epsilon &&
                   System.Math.Abs(W - other.W) < epsilon;
        }
    }

    [Serializable]
    public struct ProjectileStateSnapshot
    {
        public long ProjectileId;
        public long OwnerId;
        public Vec3 StartPosition;
        public Vec3 CurrentPosition;
        public Vec3 Direction;
        public float Speed;
        public float RemainingLifetime;
        public int ConfigId;
        public byte State;
    }

    [Serializable]
    public struct AbilityStateSnapshot
    {
        public long EntityId;
        public int AbilityId;
        public byte BehaviorState;
        public float ElapsedMs;
        public float CooldownRemaining;
        public bool IsActive;
        public byte[] EffectData;
    }

    [Serializable]
    public class WorldStateSnapshot
    {
        public int Version;
        public int Frame;
        public long Timestamp;
        public List<EntityStateSnapshot> Entities;
        public List<ProjectileStateSnapshot> Projectiles;
        public List<AbilityStateSnapshot> Abilities;
        public uint WorldFlags;
        public int ActiveTriggerCount;

        public WorldStateSnapshot()
        {
            Version = CurrentVersion;
            Entities = new List<EntityStateSnapshot>();
            Projectiles = new List<ProjectileStateSnapshot>();
            Abilities = new List<AbilityStateSnapshot>();
        }

        public const int CurrentVersion = 1;

        public byte[] ToBytes()
        {
            return BinarySerializer.Serialize(this);
        }

        public static WorldStateSnapshot FromBytes(byte[] data)
        {
            return BinarySerializer.Deserialize<WorldStateSnapshot>(data);
        }

        public StateHash ComputeHash()
        {
            return StateHashComputer.Compute(this);
        }

        public WorldStateSnapshot Clone()
        {
            var clone = new WorldStateSnapshot
            {
                Version = Version,
                Frame = Frame,
                Timestamp = Timestamp,
                WorldFlags = WorldFlags,
                ActiveTriggerCount = ActiveTriggerCount,
                Entities = new List<EntityStateSnapshot>(Entities.Count),
                Projectiles = new List<ProjectileStateSnapshot>(Projectiles.Count),
                Abilities = new List<AbilityStateSnapshot>(Abilities.Count)
            };

            foreach (var entity in Entities)
            {
                clone.Entities.Add(new EntityStateSnapshot(entity.EntityId)
                {
                    position = entity.position,
                    rotation = entity.rotation,
                    velocity = entity.velocity,
                    healthPercent = entity.healthPercent,
                    StateFlags = entity.StateFlags,
                    ActiveAbilityMask = entity.ActiveAbilityMask,
                    Cooldowns = new Dictionary<int, float>(entity.Cooldowns),
                    BuffTimers = new Dictionary<int, float>(entity.BuffTimers),
                    TeamId = entity.TeamId,
                    ControlFlags = entity.ControlFlags
                });
            }

            foreach (var projectile in Projectiles)
            {
                clone.Projectiles.Add(projectile);
            }

            foreach (var ability in Abilities)
            {
                clone.Abilities.Add(new AbilityStateSnapshot
                {
                    EntityId = ability.EntityId,
                    AbilityId = ability.AbilityId,
                    BehaviorState = ability.BehaviorState,
                    ElapsedMs = ability.ElapsedMs,
                    CooldownRemaining = ability.CooldownRemaining,
                    IsActive = ability.IsActive,
                    EffectData = ability.EffectData != null ? (byte[])ability.EffectData.Clone() : null
                });
            }

            return clone;
        }
    }
}
