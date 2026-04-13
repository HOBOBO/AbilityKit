using System.Collections.Generic;
using AbilityKit.Demo.Moba;
using AbilityKit.Core.Common.Projectile;
using AbilityKit.Effect;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Core.Math;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Demo.Moba.Systems.Area
{
    [WorldSystem(order: MobaSystemOrder.ProjectileSync + 1, Phase = WorldSystemPhase.PostExecute)]
    public sealed class MobaAreaSyncSystem : WorldSystemBase
    {
        private sealed class AreaTriggerPayload
        {
            public int TriggerId;
            public AreaSpawnEvent Spawn;
            public AreaEnterEvent Enter;
            public AreaExitEvent Exit;
            public AreaExpireEvent Expire;
            public int OwnerId;
            public int TargetActorId;
            public int Frame;
            public Vec3 Center;
            public float Radius;
            public ColliderId Collider;
            public int CollisionLayerMask;
            public int MaxTargets;
        }

        private IProjectileService _projectiles;
        private MobaActorRegistry _registry;
        private AbilityKit.Triggering.Eventing.IEventBus _eventBus;
        private MobaEffectExecutionService _effects;
        private MobaAreaTriggerRegistry _areaTriggers;

        private readonly List<AreaSpawnEvent> _spawns = new List<AreaSpawnEvent>(32);
        private readonly List<AreaEnterEvent> _enters = new List<AreaEnterEvent>(64);
        private readonly List<AreaExitEvent> _exits = new List<AreaExitEvent>(64);
        private readonly List<AreaExpireEvent> _expires = new List<AreaExpireEvent>(32);

        public MobaAreaSyncSystem(global::Entitas.IContexts contexts, IWorldResolver services) : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _projectiles);
            Services.TryResolve(out _registry);
            Services.TryResolve(out _eventBus);
            Services.TryResolve(out _effects);
            Services.TryResolve(out _areaTriggers);
        }

        protected override void OnExecute()
        {
            if (_projectiles == null || _registry == null) return;

            _spawns.Clear();
            _projectiles.DrainAreaSpawnEvents(_spawns);
            for (int i = 0; i < _spawns.Count; i++)
            {
                var evt = _spawns[i];
                PublishAreaEvent(AreaTriggering.Events.Spawn, evt, ownerActorId: evt.OwnerId, targetActorId: 0, frame: evt.Frame, center: evt.Center, radius: evt.Radius, collider: default);
            }

            _enters.Clear();
            _projectiles.DrainAreaEnterEvents(_enters);
            for (int i = 0; i < _enters.Count; i++)
            {
                var evt = _enters[i];
                var hitActorId = ResolveActorIdByCollider(evt.Collider);

                PublishAreaEvent(AreaTriggering.Events.Enter, evt, ownerActorId: evt.OwnerId, targetActorId: hitActorId, frame: evt.Frame, center: default, radius: 0f, collider: evt.Collider);

                if (_effects != null && _areaTriggers != null && _areaTriggers.TryGet(evt.Area, out var entry) && entry.OnEnterTriggerId > 0)
                {
                    _effects.ExecuteTriggerId(entry.OnEnterTriggerId, new AreaTriggerPayload
                    {
                        TriggerId = entry.OnEnterTriggerId,
                        Enter = evt,
                        OwnerId = evt.OwnerId,
                        TargetActorId = hitActorId,
                        Frame = evt.Frame,
                        Collider = evt.Collider,
                        Center = entry.Center,
                        Radius = entry.Radius,
                        CollisionLayerMask = entry.CollisionLayerMask,
                        MaxTargets = entry.MaxTargets,
                    });
                }
            }

            _exits.Clear();
            _projectiles.DrainAreaExitEvents(_exits);
            for (int i = 0; i < _exits.Count; i++)
            {
                var evt = _exits[i];
                var hitActorId = ResolveActorIdByCollider(evt.Collider);

                PublishAreaEvent(AreaTriggering.Events.Exit, evt, ownerActorId: evt.OwnerId, targetActorId: hitActorId, frame: evt.Frame, center: default, radius: 0f, collider: evt.Collider);

                if (_effects != null && _areaTriggers != null && _areaTriggers.TryGet(evt.Area, out var entry) && entry.OnExitTriggerId > 0)
                {
                    _effects.ExecuteTriggerId(entry.OnExitTriggerId, new AreaTriggerPayload
                    {
                        TriggerId = entry.OnExitTriggerId,
                        Exit = evt,
                        OwnerId = evt.OwnerId,
                        TargetActorId = hitActorId,
                        Frame = evt.Frame,
                        Collider = evt.Collider,
                        Center = entry.Center,
                        Radius = entry.Radius,
                        CollisionLayerMask = entry.CollisionLayerMask,
                        MaxTargets = entry.MaxTargets,
                    });
                }
            }

            _expires.Clear();
            _projectiles.DrainAreaExpireEvents(_expires);
            for (int i = 0; i < _expires.Count; i++)
            {
                var evt = _expires[i];

                PublishAreaEvent(AreaTriggering.Events.Expire, evt, ownerActorId: evt.OwnerId, targetActorId: 0, frame: evt.Frame, center: default, radius: 0f, collider: default);

                if (_effects != null && _areaTriggers != null && _areaTriggers.TryGet(evt.Area, out var entry) && entry.OnExpireTriggerIds != null && entry.OnExpireTriggerIds.Length > 0)
                {
                    for (int ti = 0; ti < entry.OnExpireTriggerIds.Length; ti++)
                    {
                        var triggerId = entry.OnExpireTriggerIds[ti];
                        if (triggerId <= 0) continue;

                        _effects.ExecuteTriggerId(triggerId, new AreaTriggerPayload
                        {
                            TriggerId = triggerId,
                            Expire = evt,
                            OwnerId = evt.OwnerId,
                            TargetActorId = 0,
                            Frame = evt.Frame,
                            Center = entry.Center,
                            Radius = entry.Radius,
                            CollisionLayerMask = entry.CollisionLayerMask,
                            MaxTargets = entry.MaxTargets,
                        });
                    }
                }

                _areaTriggers?.Unregister(evt.Area);
            }
        }

        private void PublishAreaEvent(string eventId, object raw, int ownerActorId, int targetActorId, int frame, in Vec3 center, float radius, ColliderId collider)
        {
            if (_eventBus == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            var eid = TriggeringIdUtil.GetEventEid(eventId);
            var payload = new AreaEventArgs
            {
                EventId = eventId,
                AreaId = 0,
                OwnerActorId = ownerActorId,
                TargetActorId = targetActorId,
                Frame = frame,
                Center = center,
                Radius = radius,
                Collider = collider,
                Raw = raw,
            };

            _eventBus.Publish(new EventKey<AreaEventArgs>(eid), in payload);
            object boxed = payload;
            _eventBus.Publish(new EventKey<object>(eid), in boxed);
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

