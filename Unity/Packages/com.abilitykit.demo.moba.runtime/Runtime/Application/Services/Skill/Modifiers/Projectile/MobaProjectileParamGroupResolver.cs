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

        public int ResolveLauncherId(int actorId, int launcherId, IModifierContext context = null)
        {
            return ResolveLauncherId(MobaModifierOwnerRef.Actor(actorId), launcherId, context);
        }

        public int ResolveLauncherId(MobaModifierOwnerRef owner, int launcherId, IModifierContext context = null)
        {
            return _service.ResolveInt(owner, MobaSkillParamModifierKeys.Projectile.LauncherId, launcherId, context);
        }

        public int ResolveLauncherId(MobaModifierResolveContext resolveContext, int launcherId, IModifierContext context = null)
        {
            Span<MobaModifierOwnerRef> owners = stackalloc MobaModifierOwnerRef[1];
            var count = resolveContext.WriteActorChain(owners);
            return _service.ResolveInt(owners.Slice(0, count), MobaSkillParamModifierKeys.Projectile.LauncherId, launcherId, context);
        }

        public int ResolveProjectileId(int actorId, int projectileId, IModifierContext context = null)
        {
            return ResolveProjectileId(MobaModifierOwnerRef.Actor(actorId), projectileId, context);
        }

        public int ResolveProjectileId(MobaModifierOwnerRef owner, int projectileId, IModifierContext context = null)
        {
            return _service.ResolveInt(owner, MobaSkillParamModifierKeys.Projectile.ProjectileId, projectileId, context);
        }

        public int ResolveProjectileId(MobaModifierResolveContext resolveContext, int projectileId, IModifierContext context = null)
        {
            Span<MobaModifierOwnerRef> owners = stackalloc MobaModifierOwnerRef[1];
            var count = resolveContext.WriteActorChain(owners);
            return _service.ResolveInt(owners.Slice(0, count), MobaSkillParamModifierKeys.Projectile.ProjectileId, projectileId, context);
        }

        public int ResolveCountPerShot(int actorId, int countPerShot, IModifierContext context = null)
        {
            return ResolveCountPerShot(MobaModifierOwnerRef.Actor(actorId), countPerShot, context);
        }

        public int ResolveCountPerShot(MobaModifierOwnerRef owner, int countPerShot, IModifierContext context = null)
        {
            return _service.ResolveInt(owner, MobaSkillParamModifierKeys.Projectile.CountPerShot, countPerShot, context);
        }

        public int ResolveCountPerShot(MobaModifierResolveContext resolveContext, int countPerShot, IModifierContext context = null)
        {
            return ResolveCountPerShotFromLauncher(resolveContext, countPerShot, context);
        }

        public int ResolveCountPerShotFromLauncher(MobaModifierResolveContext resolveContext, int countPerShot, IModifierContext context = null)
        {
            Span<MobaModifierOwnerRef> owners = stackalloc MobaModifierOwnerRef[2];
            var count = resolveContext.WriteLauncherThenActorChain(owners);
            return _service.ResolveInt(owners.Slice(0, count), MobaSkillParamModifierKeys.Projectile.CountPerShot, countPerShot, context);
        }

        public int ResolveCountPerShotFromProjectile(MobaModifierResolveContext resolveContext, int countPerShot, IModifierContext context = null)
        {
            Span<MobaModifierOwnerRef> owners = stackalloc MobaModifierOwnerRef[3];
            var count = resolveContext.WriteProjectileThenLauncherThenActorChain(owners);
            return _service.ResolveInt(owners.Slice(0, count), MobaSkillParamModifierKeys.Projectile.CountPerShot, countPerShot, context);
        }

        public float ResolveFanAngleDeg(int actorId, float fanAngleDeg, IModifierContext context = null)
        {
            return ResolveFanAngleDeg(MobaModifierOwnerRef.Actor(actorId), fanAngleDeg, context);
        }

        public float ResolveFanAngleDeg(MobaModifierOwnerRef owner, float fanAngleDeg, IModifierContext context = null)
        {
            return _service.ResolveFloat(owner, MobaSkillParamModifierKeys.Projectile.FanAngleDeg, fanAngleDeg, context);
        }

        public float ResolveFanAngleDeg(MobaModifierResolveContext resolveContext, float fanAngleDeg, IModifierContext context = null)
        {
            return ResolveFanAngleDegFromLauncher(resolveContext, fanAngleDeg, context);
        }

        public float ResolveFanAngleDegFromLauncher(MobaModifierResolveContext resolveContext, float fanAngleDeg, IModifierContext context = null)
        {
            Span<MobaModifierOwnerRef> owners = stackalloc MobaModifierOwnerRef[2];
            var count = resolveContext.WriteLauncherThenActorChain(owners);
            return _service.ResolveFloat(owners.Slice(0, count), MobaSkillParamModifierKeys.Projectile.FanAngleDeg, fanAngleDeg, context);
        }

        public float ResolveFanAngleDegFromProjectile(MobaModifierResolveContext resolveContext, float fanAngleDeg, IModifierContext context = null)
        {
            Span<MobaModifierOwnerRef> owners = stackalloc MobaModifierOwnerRef[3];
            var count = resolveContext.WriteProjectileThenLauncherThenActorChain(owners);
            return _service.ResolveFloat(owners.Slice(0, count), MobaSkillParamModifierKeys.Projectile.FanAngleDeg, fanAngleDeg, context);
        }

        public int ResolveDurationMs(int actorId, int durationMs, IModifierContext context = null)
        {
            return ResolveDurationMs(MobaModifierOwnerRef.Actor(actorId), durationMs, context);
        }

        public int ResolveDurationMs(MobaModifierOwnerRef owner, int durationMs, IModifierContext context = null)
        {
            return _service.ResolveInt(owner, MobaSkillParamModifierKeys.Projectile.DurationMs, durationMs, context);
        }

        public int ResolveDurationMs(MobaModifierResolveContext resolveContext, int durationMs, IModifierContext context = null)
        {
            return ResolveDurationMsFromLauncher(resolveContext, durationMs, context);
        }

        public int ResolveDurationMsFromLauncher(MobaModifierResolveContext resolveContext, int durationMs, IModifierContext context = null)
        {
            Span<MobaModifierOwnerRef> owners = stackalloc MobaModifierOwnerRef[2];
            var count = resolveContext.WriteLauncherThenActorChain(owners);
            return _service.ResolveInt(owners.Slice(0, count), MobaSkillParamModifierKeys.Projectile.DurationMs, durationMs, context);
        }

        public int ResolveDurationMsFromProjectile(MobaModifierResolveContext resolveContext, int durationMs, IModifierContext context = null)
        {
            Span<MobaModifierOwnerRef> owners = stackalloc MobaModifierOwnerRef[3];
            var count = resolveContext.WriteProjectileThenLauncherThenActorChain(owners);
            return _service.ResolveInt(owners.Slice(0, count), MobaSkillParamModifierKeys.Projectile.DurationMs, durationMs, context);
        }

        public MobaResolvedShootProjectileParams ResolveShootProjectile(
            int actorId,
            int launcherId,
            int projectileId,
            int countPerShot,
            float fanAngleDeg,
            int durationMs,
            IModifierContext context = null)
        {
            return ResolveShootProjectile(
                new MobaModifierResolveContext(actorId: actorId),
                launcherId,
                projectileId,
                countPerShot,
                fanAngleDeg,
                durationMs,
                context);
        }

        public MobaResolvedShootProjectileParams ResolveShootProjectile(
            MobaModifierResolveContext resolveContext,
            int launcherId,
            int projectileId,
            int countPerShot,
            float fanAngleDeg,
            int durationMs,
            IModifierContext context = null)
        {
            return new MobaResolvedShootProjectileParams(
                ResolveLauncherId(resolveContext, launcherId, context),
                ResolveProjectileId(resolveContext, projectileId, context),
                ResolveCountPerShotFromLauncher(resolveContext, countPerShot, context),
                ResolveFanAngleDegFromLauncher(resolveContext, fanAngleDeg, context),
                ResolveDurationMsFromLauncher(resolveContext, durationMs, context));
        }
    }
}
