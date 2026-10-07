using System;
using AbilityKit.Ability.World.Services;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;

namespace AbilityKit.Demo.Moba.Services.Projectile.Launch
{
    public sealed class MobaModifierProjectileSpawnPatternProvider : IProjectileSpawnPatternProvider
    {
        private readonly MobaSkillParamModifierService _paramModifiers;
        private readonly ProjectileMO _projectile;
        private readonly int _launcherTemplateId;
        private readonly int _slot;
        private readonly int _baseSkillId;
        private readonly int _effectiveSkillId;
        private readonly float _frameDurationMs;
        private readonly IWorldRandom _random;
        private readonly int _baseCountPerShot;
        private readonly float _baseFanAngleDeg;
        private readonly IProjectileSpawnPattern _basePattern;

        public MobaModifierProjectileSpawnPatternProvider(
            MobaSkillParamModifierService paramModifiers,
            ProjectileMO projectile,
            int launcherTemplateId,
            IWorldRandom random,
            int requestCountPerShot,
            float requestFanAngleDeg,
            int slot = 0,
            int effectiveSkillId = 0,
            float frameDurationMs = 0f,
            int baseSkillId = 0)
        {
            if (requestCountPerShot <= 0) throw new System.ArgumentOutOfRangeException(nameof(requestCountPerShot));
            if (requestFanAngleDeg < 0f) throw new System.ArgumentOutOfRangeException(nameof(requestFanAngleDeg));

            _paramModifiers = paramModifiers;
            _projectile = projectile;
            _launcherTemplateId = launcherTemplateId;
            _slot = slot;
            _baseSkillId = baseSkillId;
            _effectiveSkillId = effectiveSkillId;
            _frameDurationMs = frameDurationMs;
            _random = random;
            _baseCountPerShot = requestCountPerShot;
            _baseFanAngleDeg = requestFanAngleDeg;
            _basePattern = CreatePattern(requestCountPerShot, requestFanAngleDeg, projectile.Speed);
        }

        [MobaModifierConsumes(MobaSkillParamModifierKeys.Emitter.Ids.CountPerShot,
            MobaParameterStage.Emit, MobaModifierSampleTiming.EventSample)]
        [MobaModifierConsumes(MobaSkillParamModifierKeys.Emitter.Ids.FanAngleDeg,
            MobaParameterStage.Emit, MobaModifierSampleTiming.EventSample)]
        [MobaModifierConsumes(MobaSkillParamModifierKeys.Projectile.Ids.Speed,
            MobaParameterStage.ProjectileSpawn, MobaModifierSampleTiming.CreateSnapshot)]
        [MobaModifierConsumes(MobaSkillParamModifierKeys.Projectile.Ids.LifetimeMs,
            MobaParameterStage.ProjectileSpawn, MobaModifierSampleTiming.CreateSnapshot)]
        [MobaModifierConsumes(MobaSkillParamModifierKeys.Projectile.Ids.PierceHits,
            MobaParameterStage.ProjectileSpawn, MobaModifierSampleTiming.CreateSnapshot)]
        public IProjectileSpawnPattern GetPattern(in ProjectileSpawnParams baseSpawn, int frame)
        {
            if (_paramModifiers == null)
            {
                return _basePattern;
            }

            if (baseSpawn.RootActorId <= 0)
            {
                throw new System.InvalidOperationException($"Projectile modifier resolution requires a valid root actor id. rootActorId={baseSpawn.RootActorId}");
            }

            var emitRecord = new MobaParameterRecord(MobaParameterStage.Emit,
                slot: _slot, baseSkillId: _baseSkillId, effectiveSkillId: _effectiveSkillId,
                launcherTemplateId: _launcherTemplateId, projectileTemplateId: baseSpawn.TemplateId);
            var countPerShot = _paramModifiers.Projectile.ResolveCountPerShot(
                baseSpawn.RootActorId, _baseCountPerShot, in emitRecord);
            var fanAngleDeg = _paramModifiers.Projectile.ResolveFanAngleDeg(
                baseSpawn.RootActorId, _baseFanAngleDeg, in emitRecord);
            var spawnRecord = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn,
                slot: _slot, baseSkillId: _baseSkillId, effectiveSkillId: _effectiveSkillId,
                launcherTemplateId: _launcherTemplateId, projectileTemplateId: baseSpawn.TemplateId);
            var speed = _paramModifiers.Projectile.ResolveSpeed(
                baseSpawn.RootActorId, _projectile.Speed, in spawnRecord);
            if (speed <= 0f || float.IsNaN(speed) || float.IsInfinity(speed))
                throw new System.InvalidOperationException($"Invalid projectile spawn speed {speed}.");

            var lifetimeFrames = baseSpawn.LifetimeFrames;
            if (_frameDurationMs > 0f && !float.IsNaN(_frameDurationMs) &&
                !float.IsInfinity(_frameDurationMs))
            {
                var lifetimeMs = _paramModifiers.Projectile.ResolveLifetimeMs(
                    baseSpawn.RootActorId, _projectile.LifetimeMs, in spawnRecord);
                if (lifetimeMs < 0)
                    throw new InvalidOperationException($"Invalid projectile lifetime {lifetimeMs}.");
                var frames = lifetimeMs / (double)_frameDurationMs;
                if (frames > int.MaxValue)
                    throw new InvalidOperationException($"Projectile lifetime exceeds frame range {lifetimeMs}.");
                lifetimeFrames = lifetimeMs == 0 ? 0 : Math.Max(1, (int)Math.Round(frames));
            }

            var pierceHits = int.MinValue;
            if (baseSpawn.HitPolicyKind == ProjectileHitPolicyKind.Pierce)
                pierceHits = _paramModifiers.Projectile.ResolvePierceHits(
                    baseSpawn.RootActorId, baseSpawn.HitPolicyParam, in spawnRecord);
            return CreatePattern(countPerShot, fanAngleDeg, speed, lifetimeFrames, pierceHits);
        }

        private IProjectileSpawnPattern CreatePattern(int countPerShot, float fanAngleDeg, float speed,
            int lifetimeFrames = -1, int pierceHits = int.MinValue)
        {
            if (countPerShot <= 0) throw new System.ArgumentOutOfRangeException(nameof(countPerShot), countPerShot, "Projectile count per shot must be positive.");
            if (fanAngleDeg < 0f) throw new System.ArgumentOutOfRangeException(nameof(fanAngleDeg), fanAngleDeg, "Projectile fan angle cannot be negative.");

            var bulletsPerShot = countPerShot;
            var resolvedFanAngleDeg = fanAngleDeg;
            IProjectileSpawnPattern pattern;

            if (bulletsPerShot <= 1)
            {
                pattern = new SingleShotPattern();
            }
            else
            {
                pattern = resolvedFanAngleDeg > 0f
                    ? (IProjectileSpawnPattern)new FanPattern(bulletsPerShot, resolvedFanAngleDeg)
                    : new BurstPattern(bulletsPerShot);
            }

            return new MobaRandomizedProjectileSpawnPattern(pattern, _projectile, _random,
                speed, lifetimeFrames, pierceHits);
        }
    }
}
