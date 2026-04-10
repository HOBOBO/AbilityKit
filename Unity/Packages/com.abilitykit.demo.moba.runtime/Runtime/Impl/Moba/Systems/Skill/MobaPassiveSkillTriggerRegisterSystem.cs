using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Share.Impl.Moba.Components;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using Entitas;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    [WorldSystem(order: MobaSystemOrder.PassiveSkillTriggers, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaPassiveSkillTriggerRegisterSystem : ReactiveWorldSystemBase<global::ActorEntity>
    {
        private MobaConfigDatabase _configs;
        private IFrameTime _frameTime;
        private EffectSourceRegistry _effectSource;
        private ITriggerActionRunner _actionRunner;

        private PassiveSkillTriggerListenerManager _listenerManager;

        private readonly Dictionary<int, HashSet<long>> _ownerKeysByActor = new Dictionary<int, HashSet<long>>();

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

            var actorId = entity != null && entity.hasActorId ? entity.actorId.Value : 0;
            RemoveOngoingTriggerPlansByOwnerKeys(entity, GetPreviousOwnerKeys(actorId));
            ForgetPreviousOwnerKeys(actorId);
            _listenerManager?.TryUnregister(entity, frame);
        }

        private void TryRegister(global::ActorEntity entity)
        {
            if (entity == null) return;
            if (_configs == null || _frameTime == null) return;
            if (!entity.hasActorId || !entity.hasSkillLoadout) return;

            if (_listenerManager == null) return;

            var frame = GetFrame();

            // Build/refresh passive owner keys (SourceContextId) and manage their lifecycle.
            // Listener list itself is legacy, but we reuse it as a rollback-friendly container for SourceContextId.
            _listenerManager.TryRegister(entity, frame, outRegistrations: null);

            UpdateOngoingTriggerPlansFromPassive(entity);
        }

        private void UpdateOngoingTriggerPlansFromPassive(global::ActorEntity entity)
        {
            if (entity == null) return;
            if (!entity.hasActorId || !entity.hasSkillLoadout) return;
            if (_configs == null) return;

            var actorId = entity.actorId.Value;

            var desiredOwnerKeys = new HashSet<long>();
            var ownerKeyByPassiveSkillId = new Dictionary<int, long>();

            if (entity.hasPassiveSkillTriggerListeners)
            {
                var listeners = entity.passiveSkillTriggerListeners.Active;
                if (listeners != null)
                {
                    for (int i = 0; i < listeners.Count; i++)
                    {
                        var l = listeners[i];
                        if (l == null) continue;
                        if (l.PassiveSkillId <= 0) continue;
                        if (l.SourceContextId == 0) continue;

                        if (!ownerKeyByPassiveSkillId.ContainsKey(l.PassiveSkillId))
                        {
                            ownerKeyByPassiveSkillId[l.PassiveSkillId] = l.SourceContextId;
                            desiredOwnerKeys.Add(l.SourceContextId);
                        }
                    }
                }
            }

            // remove ongoing trigger plan intents for passives that are no longer present
            var prev = GetPreviousOwnerKeys(actorId);
            if (prev != null && prev.Count > 0)
            {
                var removed = new List<long>();
                foreach (var k in prev)
                {
                    if (!desiredOwnerKeys.Contains(k)) removed.Add(k);
                }
                RemoveOngoingTriggerPlansByOwnerKeys(entity, removed);
            }

            // upsert desired ongoing trigger plan intents
            foreach (var kv in ownerKeyByPassiveSkillId)
            {
                var passiveSkillId = kv.Key;
                var ownerKey = kv.Value;
                if (ownerKey == 0) continue;

                if (!_configs.TryGetPassiveSkill(passiveSkillId, out var mo) || mo == null) continue;
                var triggerIds = mo.TriggerIds;
                if (triggerIds == null || triggerIds.Count == 0)
                {
                    RemoveOngoingTriggerPlansByOwnerKeys(entity, new List<long> { ownerKey });
                    continue;
                }

                var ids = new int[triggerIds.Count];
                for (int i = 0; i < triggerIds.Count; i++) ids[i] = triggerIds[i];

                UpsertOngoingTriggerPlansEntry(entity, ownerKey, ids);
            }

            StorePreviousOwnerKeys(actorId, desiredOwnerKeys);
        }

        private static void UpsertOngoingTriggerPlansEntry(global::ActorEntity e, long ownerKey, int[] triggerIds)
        {
            if (e == null) return;
            if (ownerKey == 0) return;

            var oldList = e.hasOngoingTriggerPlans ? e.ongoingTriggerPlans.Active : null;
            var newList = oldList != null && oldList.Count > 0 ? new List<OngoingTriggerPlanEntry>(oldList.Count + 1) : new List<OngoingTriggerPlanEntry>(1);
            var replaced = false;

            if (oldList != null)
            {
                for (int i = 0; i < oldList.Count; i++)
                {
                    var it = oldList[i];
                    if (it == null) continue;
                    if (it.OwnerKey == ownerKey)
                    {
                        newList.Add(new OngoingTriggerPlanEntry { OwnerKey = ownerKey, TriggerIds = triggerIds });
                        replaced = true;
                    }
                    else
                    {
                        newList.Add(new OngoingTriggerPlanEntry { OwnerKey = it.OwnerKey, TriggerIds = it.TriggerIds });
                    }
                }
            }

            if (!replaced)
            {
                newList.Add(new OngoingTriggerPlanEntry { OwnerKey = ownerKey, TriggerIds = triggerIds });
            }

            var rev = e.hasOngoingTriggerPlans ? e.ongoingTriggerPlans.Revision + 1 : 1;
            if (e.hasOngoingTriggerPlans) e.ReplaceOngoingTriggerPlans(newList, rev);
            else e.AddOngoingTriggerPlans(newList, rev);
        }

        private static void RemoveOngoingTriggerPlansByOwnerKeys(global::ActorEntity e, IEnumerable<long> ownerKeys)
        {
            if (e == null) return;
            if (ownerKeys == null) return;
            if (!e.hasOngoingTriggerPlans) return;

            var oldList = e.ongoingTriggerPlans.Active;
            if (oldList == null || oldList.Count == 0) return;

            var toRemove = new HashSet<long>();
            foreach (var k in ownerKeys)
            {
                if (k != 0) toRemove.Add(k);
            }
            if (toRemove.Count == 0) return;

            var newList = new List<OngoingTriggerPlanEntry>(oldList.Count);
            var removedAny = false;

            for (int i = 0; i < oldList.Count; i++)
            {
                var it = oldList[i];
                if (it == null) continue;
                if (toRemove.Contains(it.OwnerKey))
                {
                    removedAny = true;
                    continue;
                }
                newList.Add(new OngoingTriggerPlanEntry { OwnerKey = it.OwnerKey, TriggerIds = it.TriggerIds });
            }

            if (!removedAny) return;

            var rev = e.ongoingTriggerPlans.Revision + 1;
            if (newList.Count == 0) e.RemoveOngoingTriggerPlans();
            else e.ReplaceOngoingTriggerPlans(newList, rev);
        }

        private HashSet<long> GetPreviousOwnerKeys(int actorId)
        {
            if (actorId <= 0) return null;
            return _ownerKeysByActor.TryGetValue(actorId, out var set) ? set : null;
        }

        private void StorePreviousOwnerKeys(int actorId, HashSet<long> desired)
        {
            if (actorId <= 0) return;
            if (desired == null) desired = new HashSet<long>();
            _ownerKeysByActor[actorId] = new HashSet<long>(desired);
        }

        private void ForgetPreviousOwnerKeys(int actorId)
        {
            if (actorId <= 0) return;
            _ownerKeysByActor.Remove(actorId);
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
                            var e = entities[i];
                            if (e != null && e.hasActorId)
                            {
                                RemoveOngoingTriggerPlansByOwnerKeys(e, GetPreviousOwnerKeys(e.actorId.Value));
                                ForgetPreviousOwnerKeys(e.actorId.Value);
                            }
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
            if (_configs == null) Services.TryResolve(out _configs);
            if (_frameTime == null) Services.TryResolve(out _frameTime);
            if (_effectSource == null) Services.TryResolve(out _effectSource);
            if (_actionRunner == null) Services.TryResolve(out _actionRunner);

            if (_listenerManager == null && _configs != null)
            {
                _listenerManager = new PassiveSkillTriggerListenerManager(_configs, _effectSource, _actionRunner);
            }
        }
    }
}

