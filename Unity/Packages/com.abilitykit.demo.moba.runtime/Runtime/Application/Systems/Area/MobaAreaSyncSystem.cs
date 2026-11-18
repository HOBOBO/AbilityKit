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
    /// <summary>
    /// 由共同结算入口持有的区域通知消费者，不再独立安装或自行推进物理。
    /// 每轮全部通知冻结后才消费；领域来源保留至最后一次终结回调结束。
    /// </summary>
    internal sealed class MobaAreaSyncSystem : WorldSystemBase
    {
        private static int CompareAreaEnter(AreaEnterEvent a, AreaEnterEvent b)
        {
            var areaOrder = a.Area.Value.CompareTo(b.Area.Value);
            return areaOrder != 0 ? areaOrder : a.Collider.Value.CompareTo(b.Collider.Value);
        }

        private IProjectileService _projectiles;
        private MobaActorRegistry _registry;
        private IEventBus _eventBus;
        private MobaAreaRuntimeService _areaRuntime;
        private IMobaTemporaryEntityLifecycleService _lifecycle;
        private IMobaStageTriggerService _stageTriggers;
        private IMobaBattleDiagnosticEventSink _eventCollector;
        private IFrameTime _frameTime;
        private MobaExecutionContextRegistry _executionContexts;
        private MobaConfigDatabase _configs;
        private MobaCombatRulesService _combatRules;

        private readonly List<AreaSpawnEvent> _spawns = new List<AreaSpawnEvent>(32);
        private readonly List<AreaEnterEvent> _enters = new List<AreaEnterEvent>(64);
        private readonly List<AreaExitEvent> _exits = new List<AreaExitEvent>(64);
        private readonly List<AreaStayEvent> _stays = new List<AreaStayEvent>(64);
        private readonly List<AreaExpireEvent> _expires = new List<AreaExpireEvent>(32);
        private readonly List<MobaAreaRuntimeInfo> _dueDelayAreas = new List<MobaAreaRuntimeInfo>(32);
        private readonly List<int> _delayCandidates = new List<int>(32);

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
            Services.TryResolve(out _eventCollector);
            Services.TryResolve(out _frameTime);
            Services.TryResolve(out _executionContexts);
            Services.TryResolve(out _configs);
            Services.TryResolve(out _combatRules);
        }

        internal int FreezeBatch()
        {
            _spawns.Clear();
            _projectiles.DrainAreaSpawnEvents(_spawns);
            _enters.Clear();
            _projectiles.DrainAreaEnterEvents(_enters);
            _exits.Clear();
            _projectiles.DrainAreaExitEvents(_exits);
            _stays.Clear();
            _projectiles.DrainAreaStayEvents(_stays);
            _expires.Clear();
            _projectiles.DrainAreaExpireEvents(_expires);
            _areaRuntime.CollectDelayCandidates(_delayCandidates);
            return _spawns.Count + _enters.Count + _exits.Count + _stays.Count + _expires.Count;
        }

        internal void ConsumeBatch(Action<int> consumeBudget)
        {
            for (int i = 0; i < _spawns.Count; i++)
            {
                var evt = _spawns[i];
                var info = RequireAreaInfo(evt.Area);
                PublishAreaEvent("area.spawn", evt.Area.Value, info.TemplateId, MobaExecutionKind.AreaSpawn, evt, in info, ownerActorId: evt.OwnerId, targetActorId: 0, frame: evt.Frame, center: evt.Center, radius: evt.Radius, collider: default, info.CollisionLayerMask, info.MaxTargets);
                CollectAreaSpawned(in info);
            }

            PublishDueDelayAreaEvents(consumeBudget);

            if (_enters.Count > 0)
            {
                _enters.Sort(CompareAreaEnter);
                _lifecycle?.RecordEnterEvents(MobaTemporaryEntityKind.Area, _enters.Count);
            }
            for (int i = 0; i < _enters.Count; i++)
            {
                var evt = _enters[i];
                var hitActorId = ResolveActorIdByCollider(evt.Collider);
                if (hitActorId <= 0 || hitActorId == evt.OwnerId)
                {
                    continue;
                }
                var info = RequireAreaInfo(evt.Area);
                if (_configs == null || !_configs.TryGetAoe(info.TemplateId, out var aoe) || aoe == null)
                {
                    continue;
                }
                /*
                 * 先校验目标资格和敌我关系，再认领一次性或组内目标。
                 */
                if (aoe.ConsumeOnFirstEnter || info.ClaimTargetsWithinGroup)
                {
                    if (info.ClaimTargetsWithinGroup &&
                        (!_registry.TryGet(hitActorId, out var targetActor) ||
                         !targetActor.hasUnitSubType ||
                         !MobaAreaClaimQualification.Allows(info.ClaimUnitSubTypeMask, targetActor.unitSubType.Value)))
                    {
                        continue;
                    }
                    var isEnemy = _combatRules != null && _combatRules.AreEnemies(evt.OwnerId, hitActorId);
                    if (!isEnemy)
                    {
                        continue;
                    }
                }
                /*
                 * 认领紧邻分发。前一个回调删除区域后，后续冻结的 Enter 不再有物理资格。
                 */
                var consumed = _projectiles.TryAcceptAreaEnter(evt.Area, hitActorId);
                if (!consumed) continue;
                PublishAreaEvent(evt.CrossingDirection == 0 ? "area.enter" : "area.cross", evt.Area.Value, info.TemplateId, MobaExecutionKind.AreaEnter, evt, in info, ownerActorId: evt.OwnerId, targetActorId: hitActorId, frame: evt.Frame, center: info.Center, radius: info.Radius, collider: evt.Collider, info.CollisionLayerMask, info.MaxTargets);
            }

            if (_exits.Count > 0) _lifecycle?.RecordExitEvents(MobaTemporaryEntityKind.Area, _exits.Count);
            for (int i = 0; i < _exits.Count; i++)
            {
                var evt = _exits[i];
                var hitActorId = ResolveActorIdByCollider(evt.Collider);
                if (hitActorId <= 0 || hitActorId == evt.OwnerId) continue;
                var info = RequireAreaInfo(evt.Area);
                PublishAreaEvent("area.exit", evt.Area.Value, info.TemplateId, MobaExecutionKind.AreaExit, evt, in info, ownerActorId: evt.OwnerId, targetActorId: hitActorId, frame: evt.Frame, center: info.Center, radius: info.Radius, collider: evt.Collider, info.CollisionLayerMask, info.MaxTargets);
            }

            if (_stays.Count > 0) _lifecycle?.RecordTickEvents(MobaTemporaryEntityKind.Area, _stays.Count);
            for (int i = 0; i < _stays.Count; i++)
            {
                var evt = _stays[i];
                var hitActorId = ResolveActorIdByCollider(evt.Collider);
                if (hitActorId <= 0 || hitActorId == evt.OwnerId) continue;
                var info = RequireAreaInfo(evt.Area);
                PublishAreaEvent("area.tick", evt.Area.Value, info.TemplateId, MobaExecutionKind.AreaStay, evt, in info, ownerActorId: evt.OwnerId, targetActorId: hitActorId, frame: evt.Frame, center: info.Center, radius: info.Radius, collider: evt.Collider, info.CollisionLayerMask, info.MaxTargets);
            }

            if (_expires.Count > 0) _lifecycle?.RecordExpireEvents(MobaTemporaryEntityKind.Area, _expires.Count);
            for (int i = 0; i < _expires.Count; i++)
            {
                var evt = _expires[i];
                var info = RequireAreaInfo(evt.Area);
                PublishAreaEvent("area.expire", evt.Area.Value, info.TemplateId, MobaExecutionKind.AreaExpire, evt, in info, ownerActorId: evt.OwnerId, targetActorId: 0, frame: evt.Frame, center: info.Center, radius: info.Radius, collider: default, info.CollisionLayerMask, info.MaxTargets);
                CollectAreaEnded(in info);
                _areaRuntime.Unregister(evt.Area);
            }
        }

        private void PublishDueDelayAreaEvents(Action<int> consumeBudget)
        {
            if (_areaRuntime == null) return;

            _dueDelayAreas.Clear();
            var frame = ResolveCurrentFrame();
            _areaRuntime.CollectDueDelayAreas(frame, _dueDelayAreas, _delayCandidates);
            consumeBudget(_dueDelayAreas.Count);

            for (var i = 0; i < _dueDelayAreas.Count; i++)
            {
                var info = _dueDelayAreas[i];
                PublishAreaEvent("area.delay", info.AreaId, info.TemplateId, MobaExecutionKind.AreaSpawn, info, in info, ownerActorId: info.OwnerActorId, targetActorId: 0, frame: frame, center: info.Center, radius: info.Radius, collider: default, info.CollisionLayerMask, info.MaxTargets);
            }

            _dueDelayAreas.Clear();
        }

        private void PublishAreaEvent(string eventId, int areaId, int templateId, MobaExecutionKind executionKind, object raw, in MobaAreaRuntimeInfo info, int ownerActorId, int targetActorId, int frame, in Vec3 center, float radius, ColliderId collider, int collisionLayerMask, int maxTargets)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            var sourceContextId = info.SourceContextId;
            var rootContextId = info.RootContextId != 0L ? info.RootContextId : sourceContextId;
            var ownerContextId = info.OwnerContextId != 0L ? info.OwnerContextId : sourceContextId;
            var eventContextId = 0L;
            var normalizedExecutionKind = NormalizeAreaExecutionKind(executionKind);
            if (normalizedExecutionKind != MobaExecutionKind.None)
            {
                if (_executionContexts == null)
                    throw new InvalidOperationException($"Area event requires MobaExecutionContextRegistry. eventId={eventId} areaId={areaId}");
                var node = _executionContexts.Create(new MobaExecutionContextCreateRequest(
                    normalizedExecutionKind,
                    templateId,
                    ownerActorId,
                    targetActorId,
                    sourceContextId,
                    rootContextId,
                    ownerContextId,
                    frame,
                    originKind: MobaExecutionKind.AreaSpawn,
                    originConfigId: templateId));
                eventContextId = node.ContextId;
                sourceContextId = eventContextId;
            }

            var payload = new AreaEventArgs
            {
                EventId = eventId,
                AreaId = areaId,
                TemplateId = templateId,
                OwnerActorId = ownerActorId,
                TargetActorId = targetActorId,
                Frame = frame,
                ExecutionKind = executionKind != MobaExecutionKind.None ? executionKind : MobaExecutionKind.AreaSpawn,
                Center = center,
                Forward = info.Forward,
                Radius = radius,
                Collider = collider,
                CrossingDirection = raw is AreaEnterEvent enter ? enter.CrossingDirection : 0,
                CollisionLayerMask = collisionLayerMask,
                MaxTargets = maxTargets,
                SourceContextId = sourceContextId,
                RootContextId = rootContextId,
                OwnerContextId = ownerContextId,
                SkillRuntimeHandle = info.SkillRuntimeHandle,
                Raw = raw,
            };

            try
            {
                if (_eventBus != null)
                {
                    var eid = TriggeringIdUtil.GetEventEid(eventId);
                    _eventBus.Publish(new EventKey<AreaEventArgs>(eid), in payload);
                    var objectKey = new EventKey<object>(eid);
                    if (_eventBus.HasSubscribers(objectKey))
                    {
                        object boxed = payload;
                        _eventBus.Publish(objectKey, in boxed);
                    }

                }

                _stageTriggers?.ExecuteAreaStage(payload);
            }
            finally
            {
                if (eventContextId != 0L)
                    _executionContexts.End(eventContextId, (int)MobaExecutionEndReason.Completed, frame);
            }
        }

        private static MobaExecutionKind NormalizeAreaExecutionKind(MobaExecutionKind executionKind)
        {
            switch (executionKind)
            {
                case MobaExecutionKind.AreaEnter: return MobaExecutionKind.AreaEnter;
                case MobaExecutionKind.AreaExit: return MobaExecutionKind.AreaExit;
                case MobaExecutionKind.AreaStay: return MobaExecutionKind.AreaStay;
                case MobaExecutionKind.AreaExpire: return MobaExecutionKind.AreaExpire;
                default: return MobaExecutionKind.None;
            }
        }

        private MobaAreaRuntimeInfo RequireAreaInfo(AreaId areaId)
        {
            if (_areaRuntime == null)
            {
                throw new InvalidOperationException("MobaAreaSyncSystem requires MobaAreaRuntimeService for area event metadata.");
            }

            if (!_areaRuntime.TryGetArea(areaId.Value, out var info))
            {
                throw new InvalidOperationException($"Area event missing runtime metadata. areaId={areaId.Value}");
            }

            if (info.TemplateId <= 0)
            {
                throw new InvalidOperationException($"Area event missing template id. areaId={areaId.Value}");
            }

            return info;
        }

        private int ResolveCurrentFrame()
        {
            return _frameTime != null ? _frameTime.Frame.Value : 0;
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
                // 诊断提交失败不应影响区域生成流程，静默吞掉异常�?
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
                // 诊断提交失败不应影响区域销毁流程，静默吞掉异常�?
            }
        }
    }
}
