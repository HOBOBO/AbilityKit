using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Triggering;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering
{
    public interface IMobaStageTriggerService
    {
        void ExecuteAreaStage(AreaEventArgs payload);
        void ExecuteProjectileSpawn(in ProjectileSpawnEvent evt);
        void ExecuteProjectileTick(in ProjectileTickEvent evt);
        void ExecuteProjectileExit(in ProjectileExitEvent evt);
        void ExecuteProjectileHit(in ProjectileHitEvent evt, int hitActorId);
    }

    [WorldService(typeof(IMobaStageTriggerService))]
    [WorldService(typeof(MobaStageTriggerService))]
    public sealed class MobaStageTriggerService : IService, IMobaStageTriggerService, IDisposable
    {
        private const string CueKindProjectile = "Projectile";
        private const string EventProjectileSpawn = "projectile.spawn";
        private const string EventProjectileTick = "projectile.tick";
        private const string EventProjectileExit = "projectile.exit";
        private const string EventProjectileHit = "projectile.hit";

        [WorldInject(required: false)] private MobaConfigDatabase _configs = null;
        [WorldInject(required: false)] private MobaTriggerExecutionGateway _triggers = null;
        [WorldInject(required: false)] private MobaProjectileLinkService _projectileLinks = null;
        [WorldInject(required: false)] private MobaPresentationCueSnapshotService _presentationCues = null;
        [WorldInject] private MobaExecutionContextRegistry _executionContexts = null;

        public void ExecuteAreaStage(AreaEventArgs payload)
        {
            if (payload == null) return;
            var eventId = payload.EventId;
            var templateId = payload.TemplateId;
            if (_triggers == null || _configs == null)
            {
                Log.Warning($"[MobaStageTriggerService] area.stage skipped missing service eventId={eventId} areaId={payload.AreaId} templateId={templateId} hasTriggers={_triggers != null} hasConfigs={_configs != null}");
                return;
            }

            if (templateId <= 0 || string.IsNullOrEmpty(eventId))
            {
                Log.Warning($"[MobaStageTriggerService] area.stage skipped invalid args eventId={eventId} areaId={payload.AreaId} templateId={templateId}");
                return;
            }

            if (!_configs.TryGetAoe(templateId, out var aoe) || aoe == null)
            {
                Log.Warning($"[MobaStageTriggerService] area.stage skipped missing aoe eventId={eventId} areaId={payload.AreaId} templateId={templateId}");
                return;
            }

            var triggerIds = ResolveAreaTriggerIds(eventId, aoe) ?? Array.Empty<int>();
            for (var i = 0; i < triggerIds.Length; i++)
            {
                var triggerId = triggerIds[i];
                if (triggerId <= 0) continue;
                var request = MobaTriggerExecutionRequest<AreaEventArgs>.Create(triggerId, payload, eventId);
                _triggers.ExecuteDirectTrigger(in request);
            }
        }

        public void ExecuteProjectileSpawn(in ProjectileSpawnEvent evt)
        {
            if (!TryGetProjectile(evt.TemplateId, out var projectile)) return;

            var sourceContext = ResolveProjectileSource(evt.Projectile);
            var sourceActorId = ResolveSourceActorId(in sourceContext, evt.OwnerId, evt.LauncherActorId);
            ReportProjectileCue(MobaPresentationCueStage.Started, projectile.OnSpawnVfxId, projectile, evt.Projectile, sourceActorId, 0, evt.Frame, evt.Position, in sourceContext, EventProjectileSpawn, 0);
            ExecuteProjectileTriggers(projectile.OnSpawnTriggerIds, BuildProjectileEventArgs(EventProjectileSpawn, projectile, evt.Projectile, sourceActorId, 0, evt.Frame, evt, evt.Position, evt.Direction, default, in sourceContext));
        }

        public void ExecuteProjectileTick(in ProjectileTickEvent evt)
        {
            if (!TryGetProjectile(evt.TemplateId, out var projectile)) return;

            var sourceContext = ResolveProjectileSource(evt.Projectile);
            var sourceActorId = ResolveSourceActorId(in sourceContext, evt.OwnerId, evt.LauncherActorId);
            ExecuteProjectileTriggers(projectile.OnTickTriggerIds, BuildProjectileEventArgs(EventProjectileTick, projectile, evt.Projectile, sourceActorId, 0, evt.Frame, evt, evt.Position, default, default, in sourceContext));
        }

        public void ExecuteProjectileExit(in ProjectileExitEvent evt)
        {
            if (!TryGetProjectile(evt.TemplateId, out var projectile)) return;

            var sourceContext = ResolveProjectileSource(evt.Projectile);
            var sourceActorId = ResolveSourceActorId(in sourceContext, evt.OwnerId, evt.LauncherActorId);
            ReportProjectileCue(MobaPresentationCueStage.Completed, projectile.OnExpireVfxId, projectile, evt.Projectile, sourceActorId, 0, evt.Frame, evt.Position, in sourceContext, EventProjectileExit, (int)evt.Reason);
            ExecuteProjectileTriggers(projectile.OnExitTriggerIds, BuildProjectileEventArgs(EventProjectileExit, projectile, evt.Projectile, sourceActorId, 0, evt.Frame, evt, evt.Position, default, evt.Reason, in sourceContext));
        }

        public void ExecuteProjectileHit(in ProjectileHitEvent evt, int hitActorId)
        {
            if (!TryGetProjectile(evt.TemplateId, out var projectile)) return;
            if (hitActorId <= 0) return;

            var sourceContext = ResolveProjectileSource(evt.Projectile);
            var sourceActorId = ResolveSourceActorId(in sourceContext, evt.OwnerId, evt.LauncherActorId);
            if (!sourceContext.IsValid) return;

            var hitContextId = 0L;
            var rootContextId = sourceContext.RootContextId != 0L
                ? sourceContext.RootContextId
                : sourceContext.SourceContextId;
            var ownerContextId = sourceContext.OwnerContextId != 0L
                ? sourceContext.OwnerContextId
                : rootContextId;
            var executionContexts = _executionContexts ??
                throw new InvalidOperationException("Projectile hit requires MobaExecutionContextRegistry.");
            var hit = executionContexts.Create(new MobaExecutionContextCreateRequest(
                MobaExecutionKind.ProjectileHit,
                projectile.Id,
                sourceActorId,
                hitActorId,
                sourceContext.SourceContextId,
                rootContextId,
                ownerContextId,
                evt.Frame,
                originKind: MobaExecutionKind.ProjectileLaunch,
                originConfigId: projectile.Id));
            hitContextId = hit.ContextId;
            sourceContext = WithProjectileEventContext(
                in sourceContext,
                hitContextId,
                rootContextId,
                ownerContextId,
                hitActorId,
                MobaExecutionKind.ProjectileHit);

            try
            {
                ReportProjectileCue(MobaPresentationCueStage.Executed, projectile.OnHitVfxId, projectile, evt.Projectile, sourceActorId, hitActorId, evt.Frame, evt.Point, in sourceContext, EventProjectileHit, evt.HitCount);

                if (_triggers == null) return;

                var payload = new ProjectileHitArgs
                {
                    SourceActorId = sourceActorId,
                    TargetActorId = hitActorId,
                    SourceConfigId = projectile.Id,
                    Frame = evt.Frame,
                    Raw = evt,
                    SourceContext = sourceContext,
                    CasterActorId = sourceActorId,
                    ProjectileTemplateId = projectile.Id,
                    ProjectileId = evt.Projectile,
                    Point = evt.Point,
                    Normal = evt.Normal,
                    HitCollider = evt.HitCollider,
                };

                if (projectile.OnHitEffectId > 0)
                {
                    var effectRequest = MobaTriggerExecutionRequest<ProjectileHitArgs>.Create(projectile.OnHitEffectId, payload, EventProjectileHit);
                    _triggers.ExecuteDirectTrigger(in effectRequest);
                }

                ExecuteProjectileHitTriggers(projectile.OnHitTriggerIds, payload);
            }
            finally
            {
                if (hitContextId != 0L)
                {
                    executionContexts.End(hitContextId, (int)MobaExecutionEndReason.Completed, evt.Frame);
                }
            }
        }

        public void Dispose()
        {
        }

        private bool TryGetProjectile(int templateId, out ProjectileMO projectile)
        {
            projectile = null;
            return _configs != null && templateId > 0 && _configs.TryGetProjectile(templateId, out projectile) && projectile != null;
        }

        private ProjectileSourceContext ResolveProjectileSource(ProjectileId projectileId)
        {
            if (_projectileLinks != null && _projectileLinks.TryGetSource(projectileId, out var sourceContext))
            {
                return sourceContext;
            }

            return default;
        }

        private static int ResolveSourceActorId(in ProjectileSourceContext sourceContext, int ownerActorId, int launcherActorId)
        {
            if (sourceContext.SourceActorId > 0) return sourceContext.SourceActorId;
            if (ownerActorId > 0) return ownerActorId;
            return launcherActorId;
        }

        private static ProjectileEventArgs BuildProjectileEventArgs(string eventId, ProjectileMO projectile, ProjectileId projectileId, int sourceActorId, int targetActorId, int frame, object raw, Vec3 position, Vec3 direction, ProjectileExitReason exitReason, in ProjectileSourceContext sourceContext)
        {
            return new ProjectileEventArgs
            {
                EventId = eventId,
                SourceActorId = sourceActorId,
                TargetActorId = targetActorId,
                SourceConfigId = projectile.Id,
                Frame = frame,
                Raw = raw,
                SourceContext = sourceContext,
                CasterActorId = sourceActorId,
                ProjectileTemplateId = projectile.Id,
                ProjectileId = projectileId,
                Position = position,
                Direction = direction,
                ExitReason = exitReason,
            };
        }

        private void ExecuteProjectileTriggers(int[] triggerIds, ProjectileEventArgs payload)
        {
            if (_triggers == null || triggerIds == null || triggerIds.Length == 0 || payload == null) return;

            for (var i = 0; i < triggerIds.Length; i++)
            {
                var triggerId = triggerIds[i];
                if (triggerId <= 0) continue;
                var request = MobaTriggerExecutionRequest<ProjectileEventArgs>.Create(triggerId, payload, payload.EventId);
                _triggers.ExecuteDirectTrigger(in request);
            }
        }

        private void ExecuteProjectileHitTriggers(int[] triggerIds, ProjectileHitArgs payload)
        {
            if (_triggers == null || triggerIds == null || triggerIds.Length == 0 || payload == null) return;

            for (var i = 0; i < triggerIds.Length; i++)
            {
                var triggerId = triggerIds[i];
                if (triggerId <= 0) continue;
                var request = MobaTriggerExecutionRequest<ProjectileHitArgs>.Create(triggerId, payload, EventProjectileHit);
                _triggers.ExecuteDirectTrigger(in request);
            }
        }

        private void ReportProjectileCue(MobaPresentationCueStage stage, int vfxId, ProjectileMO projectile, ProjectileId projectileId, int sourceActorId, int targetActorId, int frame, Vec3 position, in ProjectileSourceContext sourceContext, string eventId, int lifecycleReason)
        {
            if (_presentationCues == null || vfxId <= 0 || projectile == null) return;

            var entry = new MobaPresentationCueSnapshotEntry
            {
                Stage = (int)stage,
                CueKind = CueKindProjectile,
                TemplateId = projectile.Id,
                VfxId = vfxId,
                RequestKey = BuildProjectileCueKey(eventId, projectileId, targetActorId, frame),
                SourceActorId = sourceActorId,
                TargetActorId = targetActorId,
                Targets = targetActorId > 0 ? new[] { targetActorId } : Array.Empty<int>(),
                Positions = new[] { position.X, position.Y, position.Z },
                OwnerKind = CueKindProjectile,
                InstanceId = projectileId.Value,
                InstanceKey = $"projectile:{projectileId.Value}",
                LifecycleReason = lifecycleReason,
                ContextKind = (int)EffectContextKind.Projectile,
                OriginKind = (int)(eventId == EventProjectileHit ? MobaExecutionKind.ProjectileHit : MobaExecutionKind.ProjectileLaunch),
                SourceContextId = sourceContext.SourceContextId,
                RootContextId = sourceContext.RootContextId != 0L ? sourceContext.RootContextId : sourceContext.SourceContextId,
                OwnerContextId = sourceContext.OwnerContextId != 0L ? sourceContext.OwnerContextId : sourceContext.SourceContextId,
                SourceConfigId = projectile.Id,
                ContextEventId = eventId,
                NumericParamKeys = new[] { 1, 2 },
                NumericParamValues = new[] { (float)projectileId.Value, frame },
                StringParamKeys = new[] { "event" },
                StringParamValues = new[] { eventId },
            };

            _presentationCues.Report(in entry);
        }

        private static string BuildProjectileCueKey(string eventId, ProjectileId projectileId, int targetActorId, int frame)
        {
            return targetActorId > 0
                ? $"{eventId}:{projectileId.Value}:{targetActorId}:{frame}"
                : $"{eventId}:{projectileId.Value}:{frame}";
        }

        private static string FormatTriggerIds(int[] triggerIds)
        {
            if (triggerIds == null) return "<null>";
            if (triggerIds.Length == 0) return "<empty>";
            return string.Join(",", triggerIds);
        }

        private static int[] ResolveAreaTriggerIds(string eventId, AoeMO aoe)
        {
            if (aoe == null || string.IsNullOrEmpty(eventId)) return Array.Empty<int>();

            switch (eventId)
            {
                case "area.delay":
                    return aoe.OnDelayTriggerIds;
                case "area.spawn":
                    return Array.Empty<int>();
                case "area.enter":
                    return aoe.OnEnterTriggerIds;
                case "area.exit":
                    return aoe.OnExitTriggerIds;
                case "area.tick":
                case "area.stay":
                    return aoe.OnIntervalTriggerIds;
                default:
                    return Array.Empty<int>();
            }
        }


        private static ProjectileSourceContext WithProjectileEventContext(
            in ProjectileSourceContext source,
            long contextId,
            long rootContextId,
            long ownerContextId,
            int targetActorId,
            MobaExecutionKind kind)
        {
            source.TryGetOrigin(out var origin);
            origin = MobaGameplayOriginBuilder.Create()
                .FromOrigin(in origin)
                .WithActors(source.SourceActorId, targetActorId)
                .WithLifecycleNode(kind, source.ProjectileConfigId, contextId)
                .WithRootContext(rootContextId)
                .WithOwnerContext(ownerContextId)
                .Build();
            return ProjectileSourceContextBuilder.Create()
                .FromSourceContext(in source)
                .WithActors(source.SourceActorId, targetActorId)
                .WithSourceContext(contextId)
                .WithRootContext(rootContextId)
                .WithOwnerContext(ownerContextId)
                .WithOrigin(in origin)
                .Build();
        }
    }
}
