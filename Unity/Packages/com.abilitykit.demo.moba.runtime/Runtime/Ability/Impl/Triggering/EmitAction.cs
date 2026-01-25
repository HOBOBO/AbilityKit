using System;
using AbilityKit.Ability;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Common.Projectile;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Services.Projectile;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Impl.Triggering
{
    public sealed class EmitAction : ITriggerRunningAction
    {
        private readonly int _emitterId;

        public EmitAction(int emitterId)
        {
            _emitterId = emitterId;
        }

        public static EmitAction FromDef(ActionDef def)
        {
            var args = def?.Args;
            var emitterId = TriggerActionArgUtil.TryGetInt(args, "emitterId", 0);
            return new EmitAction(emitterId);
        }

        public void Execute(TriggerContext context)
        {
            Start(context);
        }

        public IRunningAction Start(TriggerContext context)
        {
            if (context == null) return null;
            if (_emitterId <= 0)
            {
                Log.Warning("[Trigger] emit requires emitterId > 0");
                return null;
            }

            var configs = context.Services?.GetService(typeof(MobaConfigDatabase)) as MobaConfigDatabase;
            if (configs == null)
            {
                Log.Warning("[Trigger] emit cannot resolve MobaConfigDatabase from DI");
                return null;
            }

            if (!configs.TryGetEmitter(_emitterId, out var emitter) || emitter == null)
            {
                Log.Warning($"[Trigger] emit invalid emitterId={_emitterId} (config not found)");
                return null;
            }

            if (!TriggerActionArgUtil.TryResolveActorId(context.Source, out var casterActorId) || casterActorId <= 0)
            {
                Log.Warning("[Trigger] emit requires context.Source with valid actorId");
                return null;
            }

            var frameTime = context.Services?.GetService(typeof(IFrameTime)) as IFrameTime;
            var dt = frameTime != null && frameTime.DeltaTime > 0f ? frameTime.DeltaTime : 0.033333f;

            // Resolve center (AimPos/TargetPos/CasterPos) + offset.
            var center = ResolveCenter(context, casterActorId, emitter.CenterMode);
            var offset = new Vec3(emitter.OffsetX, emitter.OffsetY, emitter.OffsetZ);
            center += offset;

            var intervalSec = emitter.IntervalMs > 0 ? emitter.IntervalMs / 1000f : 0f;
            // For AOE, delay is handled by Area lifetime (emitter.DelayMs + aoe.DelayMs). Do not double-apply delay here.
            var delaySec = emitter.EmitKind == 2 ? 0f : (emitter.DelayMs > 0 ? emitter.DelayMs / 1000f : 0f);

            var count = emitter.TotalCount;
            if (count <= 0 && emitter.DurationMs > 0 && emitter.IntervalMs > 0)
            {
                count = Math.Max(1, (emitter.DurationMs / emitter.IntervalMs) + 1);
            }
            if (count <= 0) count = 1;

            // One-shot fast path.
            var hasOngoing = count > 1 && intervalSec > 0f;
            if (!hasOngoing && delaySec <= 0f)
            {
                EmitOnce(context, configs, emitter, casterActorId, in center);
                return null;
            }

            return new EmitScheduleRunningAction(
                delaySeconds: delaySec,
                intervalSeconds: intervalSec,
                totalCount: count,
                tick: () => EmitOnce(context, configs, emitter, casterActorId, in center));
        }

        private static Vec3 ResolveCenter(TriggerContext context, int casterActorId, int centerMode)
        {
            var payload = context.Event.Payload;

            // 0=AimPos default.
            if (centerMode == 0)
            {
                if (payload is IAbilityPipelineContext pc)
                {
                    return pc.GetAimPos();
                }

                if (payload is IEffectContext ec && ec.TryGetSkill(out var skill))
                {
                    return skill.AimPos;
                }

                if (context.Event.Args != null && context.Event.Args.TryGetValue("pos", out var p) && p is Vec3 v3)
                {
                    return v3;
                }

                return Vec3.Zero;
            }

            // 1=CasterPos.
            if (centerMode == 1)
            {
                var registry = context.Services?.GetService(typeof(MobaActorRegistry)) as MobaActorRegistry;
                if (registry != null && registry.TryGet(casterActorId, out var e) && e != null && e.hasTransform)
                {
                    return e.transform.Value.Position;
                }
                return Vec3.Zero;
            }

            // 2=TargetPos.
            if (centerMode == 2)
            {
                if (TriggerActionArgUtil.TryResolveActorId(context.Target, out var targetActorId) && targetActorId > 0)
                {
                    var registry = context.Services?.GetService(typeof(MobaActorRegistry)) as MobaActorRegistry;
                    if (registry != null && registry.TryGet(targetActorId, out var e) && e != null && e.hasTransform)
                    {
                        return e.transform.Value.Position;
                    }
                }
                return Vec3.Zero;
            }

            return Vec3.Zero;
        }

        private static void EmitOnce(TriggerContext context, MobaConfigDatabase configs, EmitterMO emitter, int casterActorId, in Vec3 center)
        {
            if (emitter == null) return;

            // 1 = Projectile
            if (emitter.EmitKind == 1)
            {
                if (emitter.TemplateId <= 0) return;

                var svc = context?.Services?.GetService(typeof(MobaProjectileService)) as MobaProjectileService;
                if (svc == null) return;

                if (!configs.TryGetProjectile(emitter.TemplateId, out var projectile) || projectile == null) return;

                // Create a launcher MO on the fly from emitter schedule fields.
                var launcherDto = new ProjectileLauncherDTO
                {
                    Id = emitter.Id,
                    Name = emitter.Name,
                    EmitterType = (int)AbilityKit.Ability.Impl.Moba.ProjectileEmitterType.Linear,
                    // EmitAction already handles schedule; ensure projectile launcher does not schedule again.
                    DurationMs = 0,
                    IntervalMs = 0,
                    CountPerShot = emitter.CountPerShot,
                    FanAngleDeg = emitter.FanAngleDeg,
                };
                var launcherMo = new ProjectileLauncherMO(launcherDto);

                var dir = Vec3.Forward;
                var payload = context.Event.Payload;
                if (payload is IAbilityPipelineContext pc)
                {
                    dir = pc.GetAimDir();
                }
                else if (payload is IEffectContext ec && ec.TryGetSkill(out var skill))
                {
                    dir = skill.AimDir;
                }
                if (dir.SqrMagnitude <= 0f) dir = Vec3.Forward;

                svc.LaunchFromSpawn(casterActorId, launcherMo, projectile, in center, in dir);
                return;
            }

            // 2 = AOE
            if (emitter.EmitKind == 2)
            {
                if (emitter.TemplateId <= 0) return;

                var projSvc = context?.Services?.GetService(typeof(IProjectileService)) as IProjectileService;
                if (projSvc == null) return;

                if (!configs.TryGetAoe(emitter.TemplateId, out var aoe) || aoe == null) return;

                var frameTime = context.Services?.GetService(typeof(IFrameTime)) as IFrameTime;
                var dt = frameTime != null && frameTime.DeltaTime > 0f ? frameTime.DeltaTime : 0.033333f;
                var frame = frameTime != null ? frameTime.Frame.Value : 0;

                var totalDelayMs = Math.Max(0, emitter.DelayMs) + Math.Max(0, aoe.DelayMs);
                var lifetimeFrames = totalDelayMs > 0 ? Math.Max(1, (int)MathF.Round((totalDelayMs / 1000f) / dt)) : 1;

                var mask = aoe.CollisionLayerMask;
                if (mask == 0) mask = -1;

                var p = new AreaSpawnParams(ownerId: casterActorId, center: center, radius: aoe.Radius, lifetimeFrames: lifetimeFrames, collisionLayerMask: mask, stayIntervalFrames: 0);
                var areaId = projSvc.SpawnArea(in p, frame);

                var registry = context?.Services?.GetService(typeof(MobaAreaTriggerRegistry)) as MobaAreaTriggerRegistry;
                registry?.Register(areaId, emitter.TemplateId, casterActorId, in center, aoe.Radius, mask, aoe.MaxTargets, onEnterTriggerId: 0, onExitTriggerId: 0, onExpireTriggerIds: aoe.OnDelayTriggerIds);
                return;
            }
        }

        private sealed class EmitScheduleRunningAction : IRunningAction
        {
            private readonly float _delay;
            private readonly float _interval;
            private readonly Action _tick;
            private float _elapsed;
            private float _delayLeft;
            private int _remaining;
            private bool _done;

            public EmitScheduleRunningAction(float delaySeconds, float intervalSeconds, int totalCount, Action tick)
            {
                _delay = delaySeconds;
                _delayLeft = delaySeconds;
                _interval = intervalSeconds;
                _tick = tick;
                _remaining = totalCount;
            }

            public bool IsDone => _done;

            public void Tick(float deltaTime)
            {
                if (_done) return;

                if (_delayLeft > 0f)
                {
                    _delayLeft -= deltaTime;
                    if (_delayLeft > 0f) return;
                }

                if (_interval <= 0f)
                {
                    // No interval specified; emit once.
                    if (_remaining > 0)
                    {
                        _tick?.Invoke();
                        _remaining--;
                    }
                    _done = true;
                    return;
                }

                _elapsed += deltaTime;
                while (_elapsed >= _interval && !_done)
                {
                    _elapsed -= _interval;
                    if (_remaining <= 0)
                    {
                        _done = true;
                        return;
                    }
                    _tick?.Invoke();
                    _remaining--;
                    if (_remaining <= 0)
                    {
                        _done = true;
                        return;
                    }
                }
            }

            public void Cancel()
            {
                _done = true;
            }

            public void Dispose()
            {
            }
        }
    }
}
