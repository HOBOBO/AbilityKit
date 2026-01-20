using System;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;
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
        private readonly int _launcherId;
        private readonly int _projectileId;

        public ShootProjectileAction(int launcherId, int projectileId)
        {
            _launcherId = launcherId;
            _projectileId = projectileId;
        }

        public static ShootProjectileAction FromDef(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null) return new ShootProjectileAction(launcherId: 0, projectileId: 0);

            var launcherId = TryGetInt(args, "launcherId");
            var projectileId = TryGetInt(args, "projectileId");

            return new ShootProjectileAction(launcherId, projectileId);
        }

        public void Execute(TriggerContext context)
        {
            if (_projectileId <= 0) return;

            var svc = context?.Services?.GetService(typeof(MobaProjectileService)) as MobaProjectileService;
            if (svc == null)
            {
                Log.Warning("[Trigger] shoot_projectile cannot resolve MobaProjectileService from DI");
                return;
            }

            var configs = context?.Services?.GetService(typeof(MobaConfigDatabase)) as MobaConfigDatabase;
            if (configs == null)
            {
                Log.Warning("[Trigger] shoot_projectile cannot resolve MobaConfigDatabase from DI");
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

            ProjectileLauncherMO launcher = null;
            ProjectileMO projectile = null;

            if (_launcherId > 0) configs.TryGetProjectileLauncher(_launcherId, out launcher);
            if (_projectileId > 0) configs.TryGetProjectile(_projectileId, out projectile);

            if (launcher == null)
            {
                Log.Warning($"[Trigger] shoot_projectile invalid launcherId={_launcherId} (launcher config not found)");
                return;
            }

            if (projectile == null)
            {
                Log.Warning($"[Trigger] shoot_projectile invalid projectileId={_projectileId} (projectile config not found)");
                return;
            }

            if (!svc.Launch(casterActorId, launcher, projectile, in aimPos, in aimDir))
            {
                Log.Warning($"[Trigger] shoot_projectile launch failed. launcherId={_launcherId} projectileId={_projectileId}");
            }
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
