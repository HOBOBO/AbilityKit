using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Demo.Moba.Systems.Area
{
    [WorldSystem(order: MobaSystemOrder.ProjectileSync + 1, Phase = WorldSystemPhase.PostExecute)]
    public sealed class MobaAreaSyncSystem : WorldSystemBase
    {
        private IProjectileService _projectiles;
        private MobaActorRegistry _registry;
        private IEventBus _eventBus;
        private MobaAreaRuntimeService _areaRuntime;
        private IMobaTemporaryEntityLifecycleService _lifecycle;
        private IMobaStageTriggerService _stageTriggers;
        private IMobaBattleDiagnosticsService _diagnostics;
        private IMobaBattleDiagnosticEventSink _eventCollector;
        private IFrameTime _frameTime;

        private readonly List<AreaSpawnEvent> _spawns = new List<AreaSpawnEvent>(32);
        private readonly List<AreaEnterEvent> _enters = new List<AreaEnterEvent>(64);
        private readonly List<AreaExitEvent> _exits = new List<AreaExitEvent>(64);
        private readonly List<AreaStayEvent> _stays = new List<AreaStayEvent>(64);
        private readonly List<AreaExpireEvent> _expires = new List<AreaExpireEvent>(32);
        private readonly List<MobaAreaRuntimeInfo> _dueDelayAreas = new List<MobaAreaRuntimeInfo>(32);

        public MobaAreaSyncSystem(global::Entitas.IContexts contexts, IWorldResolver services) : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _projectiles);
            Services.TryResolve(out _registry);
            Services.TryResolve(out _eventBus);
            Services.TryResolve(out _areaRuntime);
            Services.TryResolve(out _lifecycle);
            Services.TryResolve(out _stageTriggers);
            Services.TryResolve(out _diagnostics);
            Services.TryResolve(out _eventCollector);
            Services.TryResolve(out _frameTime);
        }

        protected override void OnExecute()
        {
            if (_projectiles == null || _registry == null) return;

            _spawns.Clear();
            _projectiles.DrainAreaSpawnEvents(_spawns);
            if (_spawns.Count > 0)
            {
                WarnAreaSync("drain.spawn", $"count={_spawns.Count}");
            }
            for (int i = 0; i < _spawns.Count; i++)
            {
                var evt = _spawns[i];
                var info = RequireAreaInfo(evt.Area);
                WarnAreaSync("publish.spawn", $"areaId={evt.Area.Value} templateId={info.TemplateId} owner={evt.OwnerId} frame={evt.Frame} sourceContextId={info.SourceContextId} rootContextId={info.RootContextId}");
                PublishAreaEvent("area.spawn", evt.Area.Value, info.TemplateId, MobaTraceKind.AreaSpawn, evt, in info, ownerActorId: evt.OwnerId, targetActorId: 0, frame: evt.Frame, center: evt.Center, radius: evt.Radius, collider: default, info.CollisionLayerMask, info.MaxTargets);
                CollectAreaSpawned(in info);
            }

            PublishDueDelayAreaEvents();

            _enters.Clear();
            _projectiles.DrainAreaEnterEvents(_enters);
            if (_enters.Count > 0)
            {
                WarnAreaSync("drain.enter", $"count={_enters.Count}");
                _lifecycle?.RecordEnterEvents(MobaTemporaryEntityKind.Area, _enters.Count);
            }
            for (int i = 0; i < _enters.Count; i++)
            {
                var evt = _enters[i];
                var hitActorId = ResolveActorIdByCollider(evt.Collider);
                if (hitActorId <= 0 || hitActorId == evt.OwnerId) continue;
                var info = RequireAreaInfo(evt.Area);
                WarnAreaSync("publish.enter", $"areaId={evt.Area.Value} templateId={info.TemplateId} owner={evt.OwnerId} target={hitActorId} frame={evt.Frame} sourceContextId={info.SourceContextId}");
                PublishAreaEvent("area.enter", evt.Area.Value, info.TemplateId, MobaTraceKind.AreaEnter, evt, in info, ownerActorId: evt.OwnerId, targetActorId: hitActorId, frame: evt.Frame, center: info.Center, radius: info.Radius, collider: evt.Collider, info.CollisionLayerMask, info.MaxTargets);
            }

            _exits.Clear();
            _projectiles.DrainAreaExitEvents(_exits);
            if (_exits.Count > 0) _lifecycle?.RecordExitEvents(MobaTemporaryEntityKind.Area, _exits.Count);
            for (int i = 0; i < _exits.Count; i++)
            {
                var evt = _exits[i];
                var hitActorId = ResolveActorIdByCollider(evt.Collider);
                if (hitActorId <= 0 || hitActorId == evt.OwnerId) continue;
                var info = RequireAreaInfo(evt.Area);
                PublishAreaEvent("area.exit", evt.Area.Value, info.TemplateId, MobaTraceKind.AreaExit, evt, in info, ownerActorId: evt.OwnerId, targetActorId: hitActorId, frame: evt.Frame, center: info.Center, radius: info.Radius, collider: evt.Collider, info.CollisionLayerMask, info.MaxTargets);
            }

            _stays.Clear();
            _projectiles.DrainAreaStayEvents(_stays);
            if (_stays.Count > 0) _lifecycle?.RecordTickEvents(MobaTemporaryEntityKind.Area, _stays.Count);
            for (int i = 0; i < _stays.Count; i++)
            {
                var evt = _stays[i];
                var hitActorId = ResolveActorIdByCollider(evt.Collider);
                if (hitActorId <= 0 || hitActorId == evt.OwnerId) continue;
                var info = RequireAreaInfo(evt.Area);
                PublishAreaEvent("area.tick", evt.Area.Value, info.TemplateId, MobaTraceKind.AreaStay, evt, in info, ownerActorId: evt.OwnerId, targetActorId: hitActorId, frame: evt.Frame, center: info.Center, radius: info.Radius, collider: evt.Collider, info.CollisionLayerMask, info.MaxTargets);
            }

            _expires.Clear();
            _projectiles.DrainAreaExpireEvents(_expires);
            if (_expires.Count > 0) _lifecycle?.RecordExpireEvents(MobaTemporaryEntityKind.Area, _expires.Count);
            for (int i = 0; i < _expires.Count; i++)
            {
                var evt = _expires[i];
                var info = RequireAreaInfo(evt.Area);
                PublishAreaEvent("area.expire", evt.Area.Value, info.TemplateId, MobaTraceKind.AreaExpire, evt, in info, ownerActorId: evt.OwnerId, targetActorId: 0, frame: evt.Frame, center: info.Center, radius: info.Radius, collider: default, info.CollisionLayerMask, info.MaxTargets);
                CollectAreaEnded(in info);
                _areaRuntime.Unregister(evt.Area);
            }
        }

        private void PublishDueDelayAreaEvents()
        {
            if (_areaRuntime == null) return;

            _dueDelayAreas.Clear();
            var frame = ResolveCurrentFrame();
            _areaRuntime.CollectDueDelayAreas(frame, _dueDelayAreas);
            if (_dueDelayAreas.Count > 0)
            {
                WarnAreaSync("drain.delay", $"count={_dueDelayAreas.Count} frame={frame}");
            }

            for (var i = 0; i < _dueDelayAreas.Count; i++)
            {
                var info = _dueDelayAreas[i];
                WarnAreaSync("publish.delay", $"areaId={info.AreaId} templateId={info.TemplateId} owner={info.OwnerActorId} frame={frame} sourceContextId={info.SourceContextId} rootContextId={info.RootContextId}");
                PublishAreaEvent("area.delay", info.AreaId, info.TemplateId, MobaTraceKind.AreaSpawn, info, in info, ownerActorId: info.OwnerActorId, targetActorId: 0, frame: frame, center: info.Center, radius: info.Radius, collider: default, info.CollisionLayerMask, info.MaxTargets);
            }

            _dueDelayAreas.Clear();
        }

        private void PublishAreaEvent(string eventId, int areaId, int templateId, MobaTraceKind traceKind, object raw, in MobaAreaRuntimeInfo info, int ownerActorId, int targetActorId, int frame, in Vec3 center, float radius, ColliderId collider, int collisionLayerMask, int maxTargets)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            if (_eventBus != null)
            {
                var eid = TriggeringIdUtil.GetEventEid(eventId);
                var payload = new AreaEventArgs
                {
                    EventId = eventId,
                    AreaId = areaId,
                    TemplateId = templateId,
                    OwnerActorId = ownerActorId,
                    TargetActorId = targetActorId,
                    Frame = frame,
                    TraceKind = traceKind != MobaTraceKind.None ? traceKind : MobaTraceKind.AreaSpawn,
                    Center = center,
                    Radius = radius,
                    Collider = collider,
                    CollisionLayerMask = collisionLayerMask,
                    MaxTargets = maxTargets,
                    SourceContextId = info.SourceContextId,
                    RootContextId = info.RootContextId,
                    OwnerContextId = info.OwnerContextId,
                    SkillRuntimeHandle = info.SkillRuntimeHandle,
                    Raw = raw,
                };

                _eventBus.Publish(new EventKey<AreaEventArgs>(eid), in payload);
                var objectKey = new EventKey<object>(eid);
                if (_eventBus.HasSubscribers(objectKey))
                {
                    object boxed = payload;
                    _eventBus.Publish(objectKey, in boxed);
                }
            }

            _stageTriggers?.ExecuteAreaStage(eventId, areaId, templateId, raw, in info, ownerActorId, targetActorId, frame, in center, radius, collider, collisionLayerMask, maxTargets);
        }

        private MobaAreaRuntimeInfo RequireAreaInfo(AreaId areaId)
        {
            if (_areaRuntime == null)
            {
                WarnAreaSync("metadata.service_missing", $"areaId={areaId.Value}");
                throw new InvalidOperationException("MobaAreaSyncSystem requires MobaAreaRuntimeService for area event metadata.");
            }

            if (!_areaRuntime.TryGetArea(areaId.Value, out var info))
            {
                WarnAreaSync("metadata.missing", $"areaId={areaId.Value}");
                throw new InvalidOperationException($"Area event missing runtime metadata. areaId={areaId.Value}");
            }

            if (info.TemplateId <= 0)
            {
                WarnAreaSync("metadata.template_missing", $"areaId={areaId.Value} sourceContextId={info.SourceContextId}");
                throw new InvalidOperationException($"Area event missing template id. areaId={areaId.Value}");
            }

            return info;
        }

        private int ResolveCurrentFrame()
        {
            return _frameTime != null ? _frameTime.Frame.Value : 0;
        }

        private void WarnAreaSync(string suffix, string message)
        {
            if (_diagnostics == null) return;
            _diagnostics.Warning("moba.area.sync." + suffix, message);
        }

        private int ResolveActorIdByCollider(ColliderId id)
        {
            if (_registry == null) return 0;
            if (id.Value <= 0) return 0;

            foreach (var kv in _registry.Entries)
            {
                var e = kv.Value;
                if (e == null || !e.hasActorId || !e.hasCollisionId) continue;
                if (e.collisionId.Value.Equals(id))
                {
                    return e.actorId.Value;
                }
            }

            return 0;
        }

        // ===== 诊断 Producer：Area 生命周期草稿提交 =====

        private void CollectAreaSpawned(in MobaAreaRuntimeInfo info)
        {
            if (_eventCollector == null) return;

            try
            {
                var draft = MobaAreaDiagnosticProducer.CreateAreaSpawnedDraft(in info);
                _eventCollector.TryCollect(in draft);
            }
            catch (Exception)
            {
                // 诊断提交失败不应影响区域生成流程，静默吞掉异常。
            }
        }

        private void CollectAreaEnded(in MobaAreaRuntimeInfo info)
        {
            if (_eventCollector == null) return;

            try
            {
                var draft = MobaAreaDiagnosticProducer.CreateAreaEndedDraft(in info);
                _eventCollector.TryCollect(in draft);
            }
            catch (Exception)
            {
                // 诊断提交失败不应影响区域销毁流程，静默吞掉异常。
            }
        }
    }
}
