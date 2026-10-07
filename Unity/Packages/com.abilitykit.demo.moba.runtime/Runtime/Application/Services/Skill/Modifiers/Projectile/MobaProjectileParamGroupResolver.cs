using System;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaProjectileParamGroupResolver
    {
        private readonly MobaSkillParamModifierService _service;

        internal MobaProjectileParamGroupResolver(MobaSkillParamModifierService service)
        {
            _service = service;
        }

        public int ResolveDurationMs(int ownerActorId, int durationMs,
            in MobaParameterRecord record, IModifierContext context = null)
        {
            if (record.Stage != MobaParameterStage.LauncherCreate)
                throw new ArgumentException("Emitter duration requires LauncherCreate stage.", nameof(record));
            return _service.ResolveInt(MobaModifierOwnerRef.Actor(ownerActorId),
                MobaSkillParamModifierKeys.Emitter.DurationMs, durationMs, in record, context);
        }

        public int ResolveIntervalMs(int ownerActorId, int intervalMs,
            in MobaParameterRecord record, IModifierContext context = null)
        {
            if (record.Stage != MobaParameterStage.LauncherCreate)
                throw new ArgumentException("Emitter interval requires LauncherCreate stage.", nameof(record));
            return _service.ResolveInt(MobaModifierOwnerRef.Actor(ownerActorId),
                MobaSkillParamModifierKeys.Emitter.IntervalMs, intervalMs, in record, context);
        }

        public int ResolveLifetimeMs(int ownerActorId, int lifetimeMs,
            in MobaParameterRecord record, IModifierContext context = null)
        {
            if (record.Stage != MobaParameterStage.ProjectileSpawn)
                throw new ArgumentException("Projectile lifetime requires ProjectileSpawn stage.", nameof(record));
            return _service.ResolveInt(MobaModifierOwnerRef.Actor(ownerActorId),
                MobaSkillParamModifierKeys.Projectile.LifetimeMs, lifetimeMs, in record, context);
        }

        public int ResolvePierceHits(int ownerActorId, int pierceHits,
            in MobaParameterRecord record, IModifierContext context = null)
        {
            if (record.Stage != MobaParameterStage.ProjectileSpawn)
                throw new ArgumentException("Projectile pierce hits require ProjectileSpawn stage.", nameof(record));
            var result = _service.ResolveInt(MobaModifierOwnerRef.Actor(ownerActorId),
                MobaSkillParamModifierKeys.Projectile.PierceHits, pierceHits, in record, context);
            if (result != -1 && result < 1)
                throw new InvalidOperationException($"Invalid projectile pierce hits {result}.");
            return result;
        }

        public float ResolveSpeed(int ownerActorId, float speed, in MobaParameterRecord record,
            IModifierContext context = null)
        {
            if (record.Stage != MobaParameterStage.ProjectileSpawn)
                throw new ArgumentException("Projectile speed requires ProjectileSpawn stage.", nameof(record));
            return _service.ResolveFloat(MobaModifierOwnerRef.Actor(ownerActorId),
                MobaSkillParamModifierKeys.Projectile.Speed, speed, in record, context);
        }

        public int ResolveCountPerShot(int ownerActorId, int countPerShot,
            in MobaParameterRecord record, IModifierContext context = null)
        {
            if (record.Stage != MobaParameterStage.Emit)
                throw new ArgumentException("Projectile count requires Emit stage.", nameof(record));
            return _service.ResolveInt(MobaModifierOwnerRef.Actor(ownerActorId),
                MobaSkillParamModifierKeys.Emitter.CountPerShot, countPerShot, in record, context);
        }

        public float ResolveFanAngleDeg(int ownerActorId, float fanAngleDeg,
            in MobaParameterRecord record, IModifierContext context = null)
        {
            if (record.Stage != MobaParameterStage.Emit)
                throw new ArgumentException("Projectile fan angle requires Emit stage.", nameof(record));
            return _service.ResolveFloat(MobaModifierOwnerRef.Actor(ownerActorId),
                MobaSkillParamModifierKeys.Emitter.FanAngleDeg, fanAngleDeg, in record, context);
        }
    }
}
