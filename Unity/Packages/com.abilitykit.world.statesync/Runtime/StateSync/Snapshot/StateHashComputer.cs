using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Linq;

namespace AbilityKit.Ability.StateSync.Snapshot
{
    public static class StateHashComputer
    {
        private const uint HASH_SEED = 0x9E3779B9u;

        public static StateHash Compute(WorldStateSnapshot snapshot)
        {
            if (snapshot == null) return StateHash.Invalid;

            unchecked
            {
                ulong hash = (ulong)HASH_SEED;
                hash = HashCombine(hash, snapshot.Version);
                hash = HashCombine(hash, snapshot.Frame);
                hash = HashCombine(hash, snapshot.Timestamp);
                hash = HashCombine(hash, snapshot.WorldFlags);
                hash = HashCombine(hash, snapshot.ActiveTriggerCount);

                hash = HashCombine(hash, snapshot.Entities?.Count ?? 0);
                if (snapshot.Entities != null)
                {
                    foreach (var entity in snapshot.Entities.OrderBy(e => e.EntityId))
                    {
                        hash = ComputeEntityHash(hash, entity);
                    }
                }

                hash = HashCombine(hash, snapshot.Projectiles?.Count ?? 0);
                if (snapshot.Projectiles != null)
                {
                    foreach (var proj in snapshot.Projectiles.OrderBy(p => p.ProjectileId))
                    {
                        hash = ComputeProjectileHash(hash, proj);
                    }
                }

                return new StateHash(hash);
            }
        }

        private static ulong ComputeEntityHash(ulong hash, EntityStateSnapshot entity)
        {
            hash = HashCombine(hash, entity.EntityId);
            hash = HashCombine(hash, entity.position.X);
            hash = HashCombine(hash, entity.position.Y);
            hash = HashCombine(hash, entity.position.Z);
            hash = HashCombine(hash, entity.rotation.X);
            hash = HashCombine(hash, entity.rotation.Y);
            hash = HashCombine(hash, entity.rotation.Z);
            hash = HashCombine(hash, entity.rotation.W);
            hash = HashCombine(hash, entity.velocity.X);
            hash = HashCombine(hash, entity.velocity.Y);
            hash = HashCombine(hash, entity.velocity.Z);
            hash = HashCombine(hash, entity.healthPercent);
            hash = HashCombine(hash, entity.StateFlags);
            hash = HashCombine(hash, entity.ActiveAbilityMask);
            hash = HashCombine(hash, entity.TeamId);
            hash = HashCombine(hash, entity.ControlFlags);
            return hash;
        }

        private static ulong ComputeProjectileHash(ulong hash, ProjectileStateSnapshot proj)
        {
            hash = HashCombine(hash, proj.ProjectileId);
            hash = HashCombine(hash, proj.OwnerId);
            hash = HashCombine(hash, proj.CurrentPosition.X);
            hash = HashCombine(hash, proj.CurrentPosition.Y);
            hash = HashCombine(hash, proj.CurrentPosition.Z);
            hash = HashCombine(hash, proj.Direction.X);
            hash = HashCombine(hash, proj.Direction.Y);
            hash = HashCombine(hash, proj.Direction.Z);
            hash = HashCombine(hash, proj.Speed);
            hash = HashCombine(hash, proj.RemainingLifetime);
            hash = HashCombine(hash, proj.State);
            return hash;
        }

        private static ulong HashCombine(ulong hash, long value)
        {
            return hash ^ (ulong)value * 0x9E3779B97F4A7C15UL + (hash << 15) + (hash >> 2);
        }

        private static ulong HashCombine(ulong hash, int value)
        {
            return HashCombine(hash, (long)value);
        }

        private static ulong HashCombine(ulong hash, uint value)
        {
            return HashCombine(hash, (ulong)value);
        }

        private static ulong HashCombine(ulong hash, float value)
        {
            return HashCombine(hash, (long)BitConverter.SingleToInt32Bits(value));
        }

        private static ulong HashCombine(ulong hash, ulong value)
        {
            return hash ^ value * 0x9E3779B97F4A7C15UL + (hash << 15) + (hash >> 2);
        }

        public static byte[] ComputeFingerprint(byte[] data)
        {
            if (data == null || data.Length == 0) return Array.Empty<byte>();

            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(data);
        }
    }
}
