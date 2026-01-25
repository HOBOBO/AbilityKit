using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Share.Common.Projectile;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.Share.Impl.Moba.Services.Projectile;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Area
{
    [WorldSystem(order: MobaSystemOrder.ProjectileSync + 1, Phase = WorldSystemPhase.PostExecute)]
    public sealed class MobaAreaSyncSystem : WorldSystemBase
    {
        private IProjectileService _projectiles;
        private MobaActorRegistry _registry;
        private IEventBus _eventBus;
        private MobaEffectExecutionService _effects;
        private MobaAreaTriggerRegistry _areaTriggers;

        private readonly List<AreaSpawnEvent> _spawns = new List<AreaSpawnEvent>(32);
        private readonly List<AreaEnterEvent> _enters = new List<AreaEnterEvent>(64);
        private readonly List<AreaExitEvent> _exits = new List<AreaExitEvent>(64);
        private readonly List<AreaExpireEvent> _expires = new List<AreaExpireEvent>(32);

        public MobaAreaSyncSystem(global::Entitas.IContexts contexts, IWorldServices services) : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryGet(out _projectiles);
            Services.TryGet(out _registry);
            Services.TryGet(out _eventBus);
            Services.TryGet(out _effects);
            Services.TryGet(out _areaTriggers);
        }

        protected override void OnExecute()
        {
            if (_projectiles == null || _registry == null) return;

            _spawns.Clear();
            _projectiles.DrainAreaSpawnEvents(_spawns);
            for (int i = 0; i < _spawns.Count; i++)
            {
                var evt = _spawns[i];
                if (_eventBus != null)
                {
                    var args = PooledTriggerArgs.Rent();
                    args[EffectTriggering.Args.Source] = evt.OwnerId;
                    args[EffectTriggering.Args.Target] = 0;
                    args[EffectTriggering.Args.OriginSource] = evt.OwnerId;
                    args[EffectTriggering.Args.OriginTarget] = 0;

                    args[AreaTriggering.Args.AreaId] = evt.Area.Value;
                    args[AreaTriggering.Args.OwnerId] = evt.OwnerId;
                    args[AreaTriggering.Args.Frame] = evt.Frame;
                    args[AreaTriggering.Args.Center] = evt.Center;
                    args[AreaTriggering.Args.Radius] = evt.Radius;

                    _eventBus.Publish(new TriggerEvent(AreaTriggering.Events.Spawn, payload: evt, args: args));
                }
            }

            _enters.Clear();
            _projectiles.DrainAreaEnterEvents(_enters);
            for (int i = 0; i < _enters.Count; i++)
            {
                var evt = _enters[i];
                var hitActorId = ResolveActorIdByCollider(evt.Collider);

                if (_eventBus != null)
                {
                    var args = PooledTriggerArgs.Rent();
                    args[EffectTriggering.Args.Source] = evt.OwnerId;
                    args[EffectTriggering.Args.Target] = hitActorId;
                    args[EffectTriggering.Args.OriginSource] = evt.OwnerId;
                    args[EffectTriggering.Args.OriginTarget] = hitActorId;

                    args[AreaTriggering.Args.AreaId] = evt.Area.Value;
                    args[AreaTriggering.Args.OwnerId] = evt.OwnerId;
                    args[AreaTriggering.Args.Frame] = evt.Frame;
                    args[AreaTriggering.Args.Collider] = evt.Collider;

                    _eventBus.Publish(new TriggerEvent(AreaTriggering.Events.Enter, payload: evt, args: args));
                }

                if (_effects != null && _areaTriggers != null && _areaTriggers.TryGet(evt.Area, out var entry) && entry.OnEnterTriggerId > 0)
                {
                    var args2 = PooledTriggerArgs.Rent();
                    args2[EffectTriggering.Args.Source] = evt.OwnerId;
                    args2[EffectTriggering.Args.Target] = hitActorId;
                    args2[EffectTriggering.Args.OriginSource] = evt.OwnerId;
                    args2[EffectTriggering.Args.OriginTarget] = hitActorId;

                    args2[AreaTriggering.Args.AreaId] = evt.Area.Value;
                    args2[AreaTriggering.Args.OwnerId] = evt.OwnerId;
                    args2[AreaTriggering.Args.Frame] = evt.Frame;
                    args2[AreaTriggering.Args.Collider] = evt.Collider;
                    args2[AreaTriggering.Args.Center] = entry.Center;
                    args2[AreaTriggering.Args.Radius] = entry.Radius;
                    args2["trigger.id"] = entry.OnEnterTriggerId;

                    _effects.ExecuteTriggerId(entry.OnEnterTriggerId, source: evt.OwnerId, target: hitActorId, payload: evt, args: args2);
                    args2.Dispose();
                }
            }

            _exits.Clear();
            _projectiles.DrainAreaExitEvents(_exits);
            for (int i = 0; i < _exits.Count; i++)
            {
                var evt = _exits[i];
                var hitActorId = ResolveActorIdByCollider(evt.Collider);

                if (_eventBus != null)
                {
                    var args = PooledTriggerArgs.Rent();
                    args[EffectTriggering.Args.Source] = evt.OwnerId;
                    args[EffectTriggering.Args.Target] = hitActorId;
                    args[EffectTriggering.Args.OriginSource] = evt.OwnerId;
                    args[EffectTriggering.Args.OriginTarget] = hitActorId;

                    args[AreaTriggering.Args.AreaId] = evt.Area.Value;
                    args[AreaTriggering.Args.OwnerId] = evt.OwnerId;
                    args[AreaTriggering.Args.Frame] = evt.Frame;
                    args[AreaTriggering.Args.Collider] = evt.Collider;

                    _eventBus.Publish(new TriggerEvent(AreaTriggering.Events.Exit, payload: evt, args: args));
                }

                if (_effects != null && _areaTriggers != null && _areaTriggers.TryGet(evt.Area, out var entry) && entry.OnExitTriggerId > 0)
                {
                    var args2 = PooledTriggerArgs.Rent();
                    args2[EffectTriggering.Args.Source] = evt.OwnerId;
                    args2[EffectTriggering.Args.Target] = hitActorId;
                    args2[EffectTriggering.Args.OriginSource] = evt.OwnerId;
                    args2[EffectTriggering.Args.OriginTarget] = hitActorId;

                    args2[AreaTriggering.Args.AreaId] = evt.Area.Value;
                    args2[AreaTriggering.Args.OwnerId] = evt.OwnerId;
                    args2[AreaTriggering.Args.Frame] = evt.Frame;
                    args2[AreaTriggering.Args.Collider] = evt.Collider;
                    args2[AreaTriggering.Args.Center] = entry.Center;
                    args2[AreaTriggering.Args.Radius] = entry.Radius;
                    args2["trigger.id"] = entry.OnExitTriggerId;

                    _effects.ExecuteTriggerId(entry.OnExitTriggerId, source: evt.OwnerId, target: hitActorId, payload: evt, args: args2);
                    args2.Dispose();
                }
            }

            _expires.Clear();
            _projectiles.DrainAreaExpireEvents(_expires);
            for (int i = 0; i < _expires.Count; i++)
            {
                var evt = _expires[i];

                if (_eventBus != null)
                {
                    var args = PooledTriggerArgs.Rent();
                    args[EffectTriggering.Args.Source] = evt.OwnerId;
                    args[EffectTriggering.Args.Target] = 0;
                    args[EffectTriggering.Args.OriginSource] = evt.OwnerId;
                    args[EffectTriggering.Args.OriginTarget] = 0;

                    args[AreaTriggering.Args.AreaId] = evt.Area.Value;
                    args[AreaTriggering.Args.OwnerId] = evt.OwnerId;
                    args[AreaTriggering.Args.Frame] = evt.Frame;

                    _eventBus.Publish(new TriggerEvent(AreaTriggering.Events.Expire, payload: evt, args: args));
                }

                if (_effects != null && _areaTriggers != null && _areaTriggers.TryGet(evt.Area, out var entry) && entry.OnExpireTriggerIds != null && entry.OnExpireTriggerIds.Length > 0)
                {
                    for (int ti = 0; ti < entry.OnExpireTriggerIds.Length; ti++)
                    {
                        var triggerId = entry.OnExpireTriggerIds[ti];
                        if (triggerId <= 0) continue;

                        var args2 = PooledTriggerArgs.Rent();
                        args2[EffectTriggering.Args.Source] = evt.OwnerId;
                        args2[EffectTriggering.Args.Target] = 0;
                        args2[EffectTriggering.Args.OriginSource] = evt.OwnerId;
                        args2[EffectTriggering.Args.OriginTarget] = 0;

                        args2[AreaTriggering.Args.AreaId] = evt.Area.Value;
                        args2[AreaTriggering.Args.OwnerId] = evt.OwnerId;
                        args2[AreaTriggering.Args.Frame] = evt.Frame;
                        args2[AreaTriggering.Args.Center] = entry.Center;
                        args2[AreaTriggering.Args.Radius] = entry.Radius;
                        args2["area.layerMask"] = entry.CollisionLayerMask;
                        args2["area.maxTargets"] = entry.MaxTargets;
                        args2["trigger.id"] = triggerId;

                        _effects.ExecuteTriggerId(triggerId, source: evt.OwnerId, target: 0, payload: evt, args: args2);
                        args2.Dispose();
                    }
                }

                _areaTriggers?.Unregister(evt.Area);
            }
        }

        private int ResolveActorIdByCollider(ColliderId id)
        {
            if (_registry == null) return 0;
            if (id.Value <= 0) return 0;

            try
            {
                foreach (var kv in _registry.Entries)
                {
                    var e = kv.Value;
                    if (e == null || !e.hasActorId || !e.hasCollisionId) continue;
                    if (e.collisionId.Value.Equals(id))
                    {
                        return e.actorId.Value;
                    }
                }
            }
            catch
            {
                return 0;
            }

            return 0;
        }
    }
}

