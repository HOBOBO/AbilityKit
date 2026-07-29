using System;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    public sealed class ProjectileMO
    {
        public int Id { get; }
        public string Name { get; }

        public int VfxId { get; }

        public float Speed { get; }
        public int LifetimeMs { get; }
        public float MaxDistance { get; }
        public float CollisionWidth { get; }
        public float CollisionHeight { get; }
        public float CollisionLength { get; }

        public ProjectileHitPolicyKind HitPolicyKind { get; }
        public int HitsRemaining { get; }
        public int HitCooldownMs { get; }
        public int TickIntervalMs { get; }

        public int OnHitEffectId { get; }
        public int[] OnSpawnTriggerIds { get; }
        public int[] OnHitTriggerIds { get; }
        public int[] OnTickTriggerIds { get; }
        public int[] OnExitTriggerIds { get; }
        public int OnSpawnVfxId { get; }
        public int OnHitVfxId { get; }
        public int OnExpireVfxId { get; }

        public int ReturnAfterMs { get; }
        public float ReturnSpeed { get; }
        public float ReturnStopDistance { get; }

        public string StateMachineProfileId { get; }
        public float SpawnRandomOffsetX { get; }
        public float SpawnRandomOffsetY { get; }
        public float SpawnRandomOffsetZ { get; }

        public ProjectileMO(ProjectileDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;

            VfxId = dto.VfxId;

            Speed = dto.Speed;
            LifetimeMs = dto.LifetimeMs;
            MaxDistance = dto.MaxDistance;
            CollisionWidth = dto.CollisionWidth;
            CollisionHeight = dto.CollisionHeight;
            CollisionLength = dto.CollisionLength;

            HitPolicyKind = (ProjectileHitPolicyKind)dto.HitPolicyKind;
            HitsRemaining = dto.HitsRemaining;
            HitCooldownMs = dto.HitCooldownMs;
            TickIntervalMs = dto.TickIntervalMs;

            OnHitEffectId = dto.OnHitEffectId;
            OnSpawnTriggerIds = dto.OnSpawnTriggerIds ?? Array.Empty<int>();
            OnHitTriggerIds = dto.OnHitTriggerIds ?? Array.Empty<int>();
            OnTickTriggerIds = dto.OnTickTriggerIds ?? Array.Empty<int>();
            OnExitTriggerIds = dto.OnExitTriggerIds ?? Array.Empty<int>();
            OnSpawnVfxId = dto.OnSpawnVfxId;
            OnHitVfxId = dto.OnHitVfxId;
            OnExpireVfxId = dto.OnExpireVfxId;

            ReturnAfterMs = dto.ReturnAfterMs;
            ReturnSpeed = dto.ReturnSpeed;
            ReturnStopDistance = dto.ReturnStopDistance;

            StateMachineProfileId = dto.StateMachineProfileId ?? string.Empty;
            SpawnRandomOffsetX = dto.SpawnRandomOffsetX;
            SpawnRandomOffsetY = dto.SpawnRandomOffsetY;
            SpawnRandomOffsetZ = dto.SpawnRandomOffsetZ;
        }
    }
}
