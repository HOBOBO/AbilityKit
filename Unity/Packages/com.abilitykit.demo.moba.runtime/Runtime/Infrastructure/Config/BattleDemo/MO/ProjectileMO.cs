using System;
using System.Collections.Generic;
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
        public ProjectileMaxDistanceResponse MaxDistanceResponse { get; }
        public int[] OnArrivalTriggerIds { get; }
        public float CollisionWidth { get; }
        public float CollisionHeight { get; }
        public float CollisionLength { get; }

        public ProjectileHitPolicyKind HitPolicyKind { get; }
        public int HitsRemaining { get; }
        public int HitUnitSubType { get; }
        /// <summary>Zero preserves the default unit and world collision layers.</summary>
        public int CollisionLayerMask { get; }
        public float BounceSearchRadius { get; }
        public float TrackingArcHeight { get; }
        public float TrackingArcHeightPerUnit { get; }
        public float TrackingArcDurationSeconds { get; }
        public int HitCooldownMs { get; }
        public ProjectileHitIdentityPolicy HitIdentityPolicy { get; }
        public IReadOnlyList<ProjectileCollisionRule> CollisionRules { get; }
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

        /// <summary>
        /// Resolves the authored hit settings into runtime state. HitsRemaining is a legacy
        /// table field: for Pierce it means the maximum hit count (-1 is unlimited); for
        /// Bounce it means the maximum distinct target hits including the first target.
        /// </summary>
        public bool TryResolveRuntimeSettings(out ProjectileHitPolicyKind kind, out int hitPolicyParam,
            out int hitsRemaining, out string error)
        {
            kind = HitPolicyKind;
            hitPolicyParam = 0;
            hitsRemaining = 1;
            error = null;

            if (MaxDistanceResponse != ProjectileMaxDistanceResponse.Exit &&
                MaxDistanceResponse != ProjectileMaxDistanceResponse.SuspendAtDestination)
            { error = "Unknown projectile maximum-distance response."; return false; }
            if (OnArrivalTriggerIds.Length > 0 && MaxDistanceResponse != ProjectileMaxDistanceResponse.SuspendAtDestination)
            { error = "Arrival triggers require SuspendAtDestination."; return false; }

            if (CollisionRules.Count > 256)
            {
                error = "Projectile collision rules exceed the limit of 256.";
                return false;
            }
            for (var i = 0; i < CollisionRules.Count; i++)
                if (!CollisionRules[i].TryValidate(out error)) return false;

            if (HitIdentityPolicy != ProjectileHitIdentityPolicy.Collider &&
                HitIdentityPolicy != ProjectileHitIdentityPolicy.OncePerActor)
            {
                error = $"Unknown projectile hit identity policy {(int)HitIdentityPolicy}.";
                return false;
            }

            if (!IsFiniteNonNegative(BounceSearchRadius))
            {
                error = $"BounceSearchRadius must be finite and non-negative; actual={BounceSearchRadius}.";
                return false;
            }
            if (!IsFiniteNonNegative(TrackingArcHeight))
            {
                error = $"TrackingArcHeight must be finite and non-negative; actual={TrackingArcHeight}.";
                return false;
            }
            if (!IsFiniteNonNegative(TrackingArcHeightPerUnit))
            {
                error = $"TrackingArcHeightPerUnit must be finite and non-negative; actual={TrackingArcHeightPerUnit}.";
                return false;
            }
            if (!IsFiniteNonNegative(TrackingArcDurationSeconds))
            {
                error = $"TrackingArcDurationSeconds must be finite and non-negative; actual={TrackingArcDurationSeconds}.";
                return false;
            }
            if (HitUnitSubType < 0)
            {
                error = $"HitUnitSubType must be zero (any subtype) or a positive UnitSubType value; actual={HitUnitSubType}.";
                return false;
            }

            switch (HitPolicyKind)
            {
                case ProjectileHitPolicyKind.ExitOnHit:
                    return true;
                case ProjectileHitPolicyKind.Pierce:
                    if (HitsRemaining == -1)
                    {
                        hitPolicyParam = -1;
                        hitsRemaining = 0;
                        return true;
                    }
                    if (HitsRemaining <= 0)
                    {
                        error = $"Pierce HitsRemaining must be positive or -1 for unlimited hits; actual={HitsRemaining}.";
                        return false;
                    }
                    hitPolicyParam = HitsRemaining;
                    hitsRemaining = 0;
                    return true;
                case ProjectileHitPolicyKind.Bounce:
                    if (HitsRemaining <= 0)
                    {
                        error = $"Bounce HitsRemaining must be positive and counts distinct target hits including the initial target; actual={HitsRemaining}.";
                        return false;
                    }
                    hitPolicyParam = HitsRemaining;
                    hitsRemaining = HitsRemaining;
                    return true;
                default:
                    error = $"Unknown projectile hit policy kind {(int)HitPolicyKind}.";
                    return false;
            }
        }

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

        public ProjectileMO(ProjectileDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;

            VfxId = dto.VfxId;

            Speed = dto.Speed;
            LifetimeMs = dto.LifetimeMs;
            MaxDistance = dto.MaxDistance;
            MaxDistanceResponse = (ProjectileMaxDistanceResponse)dto.MaxDistanceResponse;
            OnArrivalTriggerIds = dto.OnArrivalTriggerIds ?? Array.Empty<int>();
            CollisionWidth = dto.CollisionWidth;
            CollisionHeight = dto.CollisionHeight;
            CollisionLength = dto.CollisionLength;

            HitPolicyKind = (ProjectileHitPolicyKind)dto.HitPolicyKind;
            HitsRemaining = dto.HitsRemaining;
            HitUnitSubType = dto.HitUnitSubType;
            CollisionLayerMask = dto.CollisionLayerMask;
            BounceSearchRadius = dto.BounceSearchRadius;
            TrackingArcHeight = dto.TrackingArcHeight;
            TrackingArcHeightPerUnit = dto.TrackingArcHeightPerUnit;
            TrackingArcDurationSeconds = dto.TrackingArcDurationSeconds;
            HitCooldownMs = dto.HitCooldownMs;
            HitIdentityPolicy = (ProjectileHitIdentityPolicy)dto.HitIdentityPolicy;
            var collisionRules = new ProjectileCollisionRule[dto.CollisionRules?.Length ?? 0];
            for (var i = 0; i < collisionRules.Length; i++)
                collisionRules[i] = new ProjectileCollisionRule(dto.CollisionRules[i]);
            CollisionRules = Array.AsReadOnly(collisionRules);
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
