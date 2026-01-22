using System;
using AbilityKit.Ability.Share.Common.Projectile;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO
{
    public sealed class ProjectileMO
    {
        public int Id { get; }
        public string Name { get; }

        public int VfxId { get; }

        public float Speed { get; }
        public int LifetimeMs { get; }
        public float MaxDistance { get; }

        public ProjectileHitPolicyKind HitPolicyKind { get; }
        public int HitsRemaining { get; }
        public int HitCooldownMs { get; }
        public int TickIntervalMs { get; }

        public int OnHitEffectId { get; }
        public int OnSpawnVfxId { get; }
        public int OnHitVfxId { get; }
        public int OnExpireVfxId { get; }

        public int ReturnAfterMs { get; }
        public float ReturnSpeed { get; }
        public float ReturnStopDistance { get; }

        public ProjectileMO(global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.ProjectileDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;

            VfxId = dto.VfxId;

            Speed = dto.Speed;
            LifetimeMs = dto.LifetimeMs;
            MaxDistance = dto.MaxDistance;

            HitPolicyKind = (ProjectileHitPolicyKind)dto.HitPolicyKind;
            HitsRemaining = dto.HitsRemaining;
            HitCooldownMs = dto.HitCooldownMs;
            TickIntervalMs = dto.TickIntervalMs;

            OnHitEffectId = dto.OnHitEffectId;
            OnSpawnVfxId = dto.OnSpawnVfxId;
            OnHitVfxId = dto.OnHitVfxId;
            OnExpireVfxId = dto.OnExpireVfxId;

            ReturnAfterMs = dto.ReturnAfterMs;
            ReturnSpeed = dto.ReturnSpeed;
            ReturnStopDistance = dto.ReturnStopDistance;
        }
    }
}
