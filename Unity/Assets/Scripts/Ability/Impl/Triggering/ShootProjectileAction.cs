using System;
using System.Collections.Generic;
using AbilityKit.Ability;
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

        private readonly ProjectileTargetMode _targetMode;
        private readonly ProjectileFaceMode _faceMode;
        private readonly ProjectileSpawnMode _spawnMode;
        private readonly int _targetActorId;
        private readonly int[] _targetActorIds;
        private readonly int _searchQueryId;
        private readonly Vec3 _offset;

        public ShootProjectileAction(
            int launcherId,
            int projectileId,
            ProjectileTargetMode targetMode,
            ProjectileFaceMode faceMode,
            ProjectileSpawnMode spawnMode,
            int targetActorId,
            int[] targetActorIds,
            int searchQueryId,
            in Vec3 offset)
        {
            _launcherId = launcherId;
            _projectileId = projectileId;
            _targetMode = targetMode;
            _faceMode = faceMode;
            _spawnMode = spawnMode;
            _targetActorId = targetActorId;
            _targetActorIds = targetActorIds;
            _searchQueryId = searchQueryId;
            _offset = offset;
        }

        public static ShootProjectileAction FromDef(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null)
            {
                return new ShootProjectileAction(
                    launcherId: 0,
                    projectileId: 0,
                    targetMode: ProjectileTargetMode.SkillAim,
                    faceMode: ProjectileFaceMode.SkillAimDir,
                    spawnMode: global::AbilityKit.Ability.Impl.Moba.ProjectileSpawnMode.LegacyAimPos,
                    targetActorId: 0,
                    targetActorIds: null,
                    searchQueryId: 0,
                    offset: Vec3.Zero);
            }

            var launcherId = global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.TryGetInt(args, "launcherId");
            var projectileId = global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.TryGetInt(args, "projectileId");

            var targetMode = ProjectileTargetMode.SkillAim;
            if (args.TryGetValue("targetMode", out var targetModeObj))
            {
                targetMode = global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.ParseEnum(targetModeObj, ProjectileTargetMode.SkillAim);
            }

            var faceMode = ProjectileFaceMode.SkillAimDir;
            if (args.TryGetValue("faceMode", out var faceModeObj))
            {
                faceMode = global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.ParseEnum(faceModeObj, ProjectileFaceMode.SkillAimDir);
            }

            var spawnMode = global::AbilityKit.Ability.Impl.Moba.ProjectileSpawnMode.LegacyAimPos;
            if (args.TryGetValue("spawnMode", out var spawnModeObj))
            {
                spawnMode = global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.ParseEnum(spawnModeObj, global::AbilityKit.Ability.Impl.Moba.ProjectileSpawnMode.LegacyAimPos);
            }

            var targetActorId = global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.TryGetInt(args, "targetActorId");
            var targetActorIds = TryParseActorIdList(args);
            var searchQueryId = global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.TryGetInt(args, "searchQueryId");

            var offsetX = global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.TryGetFloat(args, "offsetX");
            var offsetY = global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.TryGetFloat(args, "offsetY");
            var offsetZ = global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.TryGetFloat(args, "offsetZ");
            var offset = new Vec3(offsetX, offsetY, offsetZ);

            return new ShootProjectileAction(launcherId, projectileId, targetMode, faceMode, spawnMode, targetActorId, targetActorIds, searchQueryId, in offset);
        }

        private static int[] TryParseActorIdList(IReadOnlyDictionary<string, object> args)
        {
            if (args == null) return null;
            if (!args.TryGetValue("targetActorIds", out var obj) || obj == null) return null;
            if (obj is int[] arr) return arr;

            if (obj is string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return null;
                var parts = s.Split(',');
                if (parts == null || parts.Length == 0) return null;

                var list = new List<int>(parts.Length);
                for (var i = 0; i < parts.Length; i++)
                {
                    var p = parts[i];
                    if (string.IsNullOrWhiteSpace(p)) continue;
                    if (int.TryParse(p.Trim(), out var id) && id > 0)
                    {
                        list.Add(id);
                    }
                }

                return list.Count == 0 ? null : list.ToArray();
            }

            return null;
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

            if (!global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.TryResolveActorId(context?.Source, out var casterActorId) || casterActorId <= 0)
            {
                Log.Warning("[Trigger] shoot_projectile requires context.Source with valid actorId");
                return;
            }

            var aimPos = Vec3.Zero;
            var aimDir = Vec3.Zero;

            var casterPos = Vec3.Zero;
            var casterForward = Vec3.Forward;

            var actorRegistry = context?.Services?.GetService(typeof(MobaActorRegistry)) as MobaActorRegistry;
            if (actorRegistry != null && actorRegistry.TryGet(casterActorId, out var casterEntity) && casterEntity != null && casterEntity.hasTransform)
            {
                var t = casterEntity.transform.Value;
                casterPos = t.Position;
                casterForward = t.Rotation.Rotate(Vec3.Forward).Normalized;
            }

            var payload = context != null ? context.Event.Payload : null;
            var payloadAimPos = Vec3.Zero;
            var payloadAimDir = Vec3.Zero;
            if (payload is IEffectContext ec && ec.TryGetSkill(out var skill))
            {
                payloadAimPos = skill.AimPos;
                payloadAimDir = skill.AimDir;
            }
            else if (payload is IAbilityPipelineContext pc)
            {
                payloadAimPos = pc.GetAimPos();
                payloadAimDir = pc.GetAimDir();
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

            var targetIds = new List<int>(8);

            if (_targetMode == ProjectileTargetMode.ActorId)
            {
                if (_targetActorIds != null && _targetActorIds.Length > 0)
                {
                    for (var i = 0; i < _targetActorIds.Length; i++)
                    {
                        var id = _targetActorIds[i];
                        if (id > 0) targetIds.Add(id);
                    }
                }
                else if (_targetActorId > 0)
                {
                    targetIds.Add(_targetActorId);
                }
                else
                {
                    Log.Warning("[Trigger] shoot_projectile targetMode=ActorId requires targetActorId/targetActorIds");
                    return;
                }
            }
            else if (_targetMode == ProjectileTargetMode.Search)
            {
                if (_searchQueryId <= 0)
                {
                    Log.Warning("[Trigger] shoot_projectile targetMode=Search requires searchQueryId > 0");
                    return;
                }

                var searchSvc = context?.Services?.GetService(typeof(SearchTargetService)) as SearchTargetService;
                if (searchSvc == null)
                {
                    Log.Warning("[Trigger] shoot_projectile targetMode=Search cannot resolve SearchTargetService from DI");
                    return;
                }

                if (!searchSvc.TrySearchFirstActorId(_searchQueryId, casterActorId, in payloadAimPos, out var foundTargetActorId) || foundTargetActorId <= 0)
                {
                    Log.Warning($"[Trigger] shoot_projectile targetMode=Search found no target. searchQueryId={_searchQueryId}");
                    return;
                }

                targetIds.Add(foundTargetActorId);
            }

            if (_targetMode == ProjectileTargetMode.SkillAim)
            {
                var targetPos = payloadAimPos;
                var spawnPos = targetPos;
                if (_spawnMode == global::AbilityKit.Ability.Impl.Moba.ProjectileSpawnMode.FromCaster)
                {
                    spawnPos = casterPos;
                }
                else if (_spawnMode == global::AbilityKit.Ability.Impl.Moba.ProjectileSpawnMode.FromTargetPoint)
                {
                    spawnPos = targetPos;
                }

                spawnPos += _offset;

                var dir = payloadAimDir;
                if (_faceMode == ProjectileFaceMode.ToTarget)
                {
                    var d = targetPos - spawnPos;
                    dir = d.Equals(Vec3.Zero) ? casterForward : d.Normalized;
                }
                else if (_faceMode == ProjectileFaceMode.CasterForward)
                {
                    dir = casterForward;
                }
                else if (_faceMode == ProjectileFaceMode.SkillAimDir)
                {
                    if (dir.Equals(Vec3.Zero)) dir = casterForward;
                }

                if (_spawnMode == global::AbilityKit.Ability.Impl.Moba.ProjectileSpawnMode.LegacyAimPos)
                {
                    aimPos = spawnPos;
                    aimDir = dir;
                    if (!svc.Launch(casterActorId, launcher, projectile, in aimPos, in aimDir))
                    {
                        Log.Warning($"[Trigger] shoot_projectile launch failed. launcherId={_launcherId} projectileId={_projectileId}");
                    }
                }
                else
                {
                    if (!svc.LaunchFromSpawn(casterActorId, launcher, projectile, in spawnPos, in dir))
                    {
                        Log.Warning($"[Trigger] shoot_projectile launch failed. launcherId={_launcherId} projectileId={_projectileId}");
                    }
                }

                return;
            }

            if (actorRegistry == null)
            {
                Log.Warning("[Trigger] shoot_projectile cannot resolve MobaActorRegistry from DI");
                return;
            }

            for (var i = 0; i < targetIds.Count; i++)
            {
                var targetActorId = targetIds[i];
                if (targetActorId <= 0) continue;

                if (!actorRegistry.TryGet(targetActorId, out var targetEntity) || targetEntity == null || !targetEntity.hasTransform)
                {
                    Log.Warning($"[Trigger] shoot_projectile cannot resolve target actor transform. targetActorId={targetActorId}");
                    continue;
                }

                var targetPos = targetEntity.transform.Value.Position;

                var spawnCenter = casterPos;
                if (_spawnMode == global::AbilityKit.Ability.Impl.Moba.ProjectileSpawnMode.FromTargetPoint)
                {
                    spawnCenter = targetPos;
                }
                else if (_spawnMode == global::AbilityKit.Ability.Impl.Moba.ProjectileSpawnMode.LegacyAimPos)
                {
                    spawnCenter = casterPos;
                }
                else if (_spawnMode == global::AbilityKit.Ability.Impl.Moba.ProjectileSpawnMode.FromCaster)
                {
                    spawnCenter = casterPos;
                }

                var spawnPos = spawnCenter + _offset;

                var dir = payloadAimDir;
                if (_faceMode == ProjectileFaceMode.ToTarget)
                {
                    var d = targetPos - spawnPos;
                    dir = d.Equals(Vec3.Zero) ? casterForward : d.Normalized;
                }
                else if (_faceMode == ProjectileFaceMode.CasterForward)
                {
                    dir = casterForward;
                }
                else if (_faceMode == ProjectileFaceMode.SkillAimDir)
                {
                    if (dir.Equals(Vec3.Zero))
                    {
                        var d = targetPos - spawnPos;
                        dir = d.Equals(Vec3.Zero) ? casterForward : d.Normalized;
                    }
                }

                if (_spawnMode == global::AbilityKit.Ability.Impl.Moba.ProjectileSpawnMode.LegacyAimPos)
                {
                    aimPos = spawnPos;
                    aimDir = dir;
                    if (!svc.Launch(casterActorId, launcher, projectile, in aimPos, in aimDir))
                    {
                        Log.Warning($"[Trigger] shoot_projectile launch failed. launcherId={_launcherId} projectileId={_projectileId}");
                    }
                }
                else
                {
                    if (!svc.LaunchFromSpawn(casterActorId, launcher, projectile, in spawnPos, in dir))
                    {
                        Log.Warning($"[Trigger] shoot_projectile launch failed. launcherId={_launcherId} projectileId={_projectileId}");
                    }
                }
            }
        }
    }
}
