using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using Entitas;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    [WorldSystem(order: MobaSystemOrder.PassiveSkillTriggers, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaPassiveSkillTriggerRegisterSystem : ReactiveWorldSystemBase<global::ActorEntity>
    {
        private AbilityKit.Triggering.Eventing.IEventBus _eventBus;
        private TriggerRunner _triggers;
        private MobaTriggerIndexService _triggerIndex;
        private MobaConfigDatabase _configs;
        private IFrameTime _frameTime;
        private EffectSourceRegistry _effectSource;
        private ITriggerActionRunner _actionRunner;

        private AbilityKit.Triggering.Eventing.IEventBus _planEventBus;

        private PassiveSkillTriggerListenerManager _listenerManager;
        private PassiveSkillTriggerExecutor _executor;

        private MobaEventSubscriptionRegistry _eventSubRegistry;

        private readonly List<PassiveSkillTriggerListenerManager.Registration> _pendingRegistrations = new List<PassiveSkillTriggerListenerManager.Registration>(8);

        public MobaPassiveSkillTriggerRegisterSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override IGroup<global::ActorEntity> CreateGroup(global::Entitas.IContexts contexts)
        {
            var c = (global::Contexts)contexts;
            return c.actor.GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.SkillLoadout));
        }

        protected override bool ShouldReactToReplace(int componentIndex)
        {
            return componentIndex == ActorComponentsLookup.SkillLoadout;
        }

        protected override void OnEntityChanged(global::ActorEntity entity)
        {
            EnsureServices();
            TryRegister(entity);
        }

        protected override void OnEntityRemovedFromGroup(global::ActorEntity entity)
        {
            EnsureServices();

            var frame = GetFrame();
            _listenerManager?.TryUnregister(entity, frame);
        }

        private void TryRegister(global::ActorEntity entity)
        {
            if (entity == null) return;
            if (_eventBus == null || _triggers == null || _triggerIndex == null || _configs == null || _frameTime == null) return;
            if (!entity.hasActorId || !entity.hasSkillLoadout) return;

            if (_listenerManager == null || _executor == null) return;

            var frame = GetFrame();

            _pendingRegistrations.Clear();
            _listenerManager.TryRegister(entity, frame, _pendingRegistrations);

            for (int i = 0; i < _pendingRegistrations.Count; i++)
            {
                var reg = _pendingRegistrations[i];
                var mo = reg.PassiveSkill;
                var l = reg.Listener;
                if (mo == null || l == null) continue;

                if (!string.IsNullOrEmpty(l.EventId))
                {
                    if (_eventSubRegistry == null)
                    {
                        Log.Warning("[MobaPassiveSkillTriggerRegisterSystem] MobaEventSubscriptionRegistry not found; skip subscribe");
                        continue;
                    }

                    if (!_eventSubRegistry.TrySubscribe<SkillCastContext>(
                            _eventBus,
                            l.EventId,
                            args => _executor.HandleEvent(entity, mo, l, in args),
                            out var sub))
                    {
                        continue;
                    }

                    l.Sub = sub;
                }
                else
                {
                    _executor.ExecuteOnce(entity, mo, l);
                }
            }
        }

        private int GetFrame()
        {
            try
            {
                return _frameTime != null ? _frameTime.Frame.Value : 0;
            }
            catch
            {
                return 0;
            }
        }

        protected override void OnTearDown()
        {
            try
            {
                var g = Group;
                if (g != null)
                {
                    var entities = g.GetEntities();
                    if (entities != null)
                    {
                        var frame = GetFrame();
                        for (int i = 0; i < entities.Length; i++)
                        {
                            _listenerManager?.TryUnregister(entities[i], frame);
                        }
                    }
                }
            }
            finally
            {
                base.OnTearDown();
            }
        }

        private void EnsureServices()
        {
            if (_eventBus == null) Services.TryResolve(out _eventBus);
            if (_triggers == null) Services.TryResolve(out _triggers);
            if (_triggerIndex == null) Services.TryResolve(out _triggerIndex);
            if (_configs == null) Services.TryResolve(out _configs);
            if (_frameTime == null) Services.TryResolve(out _frameTime);
            if (_effectSource == null) Services.TryResolve(out _effectSource);
            if (_actionRunner == null) Services.TryResolve(out _actionRunner);
            if (_planEventBus == null) Services.TryResolve(out _planEventBus);
            if (_eventSubRegistry == null) Services.TryResolve(out _eventSubRegistry);

            if (_listenerManager == null && _configs != null && _triggerIndex != null)
            {
                _listenerManager = new PassiveSkillTriggerListenerManager(_configs, _triggerIndex, _effectSource, _actionRunner);
            }

            if (_executor == null && _triggers != null && _frameTime != null)
            {
                _executor = new PassiveSkillTriggerExecutor(_triggers, _effectSource, _frameTime, _planEventBus);
            }
        }
    }
}

