using System;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.Impl.Moba.Services.Projectile;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Impl.Triggering
{
    public sealed class ShootProjectileAction : ITriggerAction
    {
        private readonly ProjectileEmitterType _emitterType;
        private readonly int _projectileCode;
        private readonly float _speed;
        private readonly int _lifetimeFrames;
        private readonly float _maxDistance;

        public ShootProjectileAction(ProjectileEmitterType emitterType, int projectileCode, float speed, int lifetimeFrames, float maxDistance)
        {
            _emitterType = emitterType;
            _projectileCode = projectileCode;
            _speed = speed;
            _lifetimeFrames = lifetimeFrames;
            _maxDistance = maxDistance;
        }

        public static ShootProjectileAction FromDef(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null) return new ShootProjectileAction(ProjectileEmitterType.Linear, 0, 0f, 0, 0f);

            var emitter = ProjectileEmitterType.Linear;
            if (args.TryGetValue("emitterType", out var et) && et != null)
            {
                if (et is ProjectileEmitterType pet) emitter = pet;
                else if (et is int ei) emitter = (ProjectileEmitterType)ei;
                else if (et is long el) emitter = (ProjectileEmitterType)(int)el;
                else if (et is string es && int.TryParse(es, out var parsed)) emitter = (ProjectileEmitterType)parsed;
            }

            var code = TryGetInt(args, "projectileCode");
            var speed = TryGetFloat(args, "speed");
            var lifetime = TryGetInt(args, "lifetimeFrames");
            var maxDist = TryGetFloat(args, "maxDistance");

            return new ShootProjectileAction(emitter, code, speed, lifetime, maxDist);
        }

        public void Execute(TriggerContext context)
        {
            if (_projectileCode <= 0) return;

            var svc = context?.Services?.GetService(typeof(MobaProjectileService)) as MobaProjectileService;
            if (svc == null)
            {
                Log.Warning("[Trigger] shoot_projectile cannot resolve MobaProjectileService from DI");
                return;
            }

            if (!TryResolveActorId(context?.Source, out var casterActorId) || casterActorId <= 0)
            {
                Log.Warning("[Trigger] shoot_projectile requires context.Source with valid actorId");
                return;
            }

            var aimPos = Vec3.Zero;
            var aimDir = Vec3.Zero;

            if (context?.Event.Payload is SkillPipelineContext pipelineCtx)
            {
                aimPos = pipelineCtx.AimPos;
                aimDir = pipelineCtx.AimDir;
            }

            svc.Shoot(casterActorId, _emitterType, _projectileCode, _speed, _lifetimeFrames, _maxDistance, in aimPos, in aimDir);
        }

        private static int TryGetInt(System.Collections.Generic.IReadOnlyDictionary<string, object> args, string key)
        {
            if (args == null || key == null) return 0;
            if (!args.TryGetValue(key, out var obj) || obj == null) return 0;
            if (obj is int i) return i;
            if (obj is long l) return (int)l;
            if (obj is string s && int.TryParse(s, out var parsed)) return parsed;
            return 0;
        }

        private static float TryGetFloat(System.Collections.Generic.IReadOnlyDictionary<string, object> args, string key)
        {
            if (args == null || key == null) return 0f;
            if (!args.TryGetValue(key, out var obj) || obj == null) return 0f;
            if (obj is float f) return f;
            if (obj is double d) return (float)d;
            if (obj is int i) return i;
            if (obj is long l) return l;
            if (obj is string s && float.TryParse(s, out var parsed)) return parsed;
            return 0f;
        }

        private static bool TryResolveActorId(object obj, out int actorId)
        {
            actorId = 0;
            if (obj == null) return false;

            if (obj is int i)
            {
                actorId = i;
                return actorId > 0;
            }

            if (obj is long l)
            {
                actorId = (int)l;
                return actorId > 0;
            }

            if (obj is EcsEntityId id)
            {
                actorId = id.ActorId;
                return actorId > 0;
            }

            if (obj is IUnitFacade unit)
            {
                actorId = unit.Id.ActorId;
                return actorId > 0;
            }

            if (obj is global::ActorEntity e && e.hasActorId)
            {
                actorId = e.actorId.Value;
                return actorId > 0;
            }

            return false;
        }
    }
}
