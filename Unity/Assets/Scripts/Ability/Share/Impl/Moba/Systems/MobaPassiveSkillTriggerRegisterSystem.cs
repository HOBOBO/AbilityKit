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
        private IEventBus _eventBus;
        private TriggerRunner _triggers;
        private MobaTriggerIndexService _triggerIndex;
        private MobaConfigDatabase _configs;
        private IFrameTime _frameTime;
        private EffectSourceRegistry _effectSource;
        private ITriggerActionRunner _actionRunner;

        public MobaPassiveSkillTriggerRegisterSystem(global::Contexts contexts, IWorldServices services)
            : base(contexts, services)
        {
        }

        protected override IGroup<global::ActorEntity> CreateGroup(global::Contexts contexts)
        {
            return contexts.actor.GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.SkillLoadout));
        }

        protected override bool ShouldReactToReplace(int componentIndex)
        {
            return componentIndex == ActorComponentsLookup.SkillLoadout;
        }

        protected override void OnEntityChanged(global::ActorEntity entity)
        {
            if (_eventBus == null) Services.TryGet(out _eventBus);
            if (_triggers == null) Services.TryGet(out _triggers);
            if (_triggerIndex == null) Services.TryGet(out _triggerIndex);
            if (_configs == null) Services.TryGet(out _configs);
            if (_frameTime == null) Services.TryGet(out _frameTime);
            if (_effectSource == null) Services.TryGet(out _effectSource);
            if (_actionRunner == null) Services.TryGet(out _actionRunner);

            TryRegister(entity);
        }

        protected override void OnEntityRemovedFromGroup(global::ActorEntity entity)
        {
            if (_effectSource == null) Services.TryGet(out _effectSource);
            if (_actionRunner == null) Services.TryGet(out _actionRunner);

            TryUnregister(entity);
        }

        private void TryRegister(global::ActorEntity entity)
        {
            if (entity == null) return;
            if (_eventBus == null || _triggers == null || _triggerIndex == null || _configs == null || _frameTime == null) return;
            if (!entity.hasActorId || !entity.hasSkillLoadout) return;

            var passiveSkills = entity.skillLoadout.PassiveSkills;
            if (passiveSkills == null) passiveSkills = Array.Empty<PassiveSkillRuntime>();

            if (!entity.hasPassiveSkillTriggerListeners)
            {
                entity.AddPassiveSkillTriggerListeners(new List<PassiveSkillTriggerListenerRuntime>(4));
            }

            var listeners = entity.passiveSkillTriggerListeners.Active;
            if (listeners == null)
            {
                listeners = new List<PassiveSkillTriggerListenerRuntime>(4);
                entity.passiveSkillTriggerListeners.Active = listeners;
            }

            // Prune obsolete listeners first (supports runtime loadout replace).
            var desired = new HashSet<long>();
            for (int i = 0; i < passiveSkills.Length; i++)
            {
                var rt = passiveSkills[i];
                if (rt == null) continue;
                var passiveSkillId = rt.PassiveSkillId;
                if (passiveSkillId <= 0) continue;

                if (!_configs.TryGetPassiveSkill(passiveSkillId, out var mo) || mo == null) continue;
                var triggerIds = mo.TriggerIds;
                if (triggerIds == null || triggerIds.Count == 0) continue;

                for (int j = 0; j < triggerIds.Count; j++)
                {
                    var triggerId = triggerIds[j];
                    if (triggerId <= 0) continue;
                    desired.Add(MakeListenerKey(passiveSkillId, triggerId));
                }
            }

            RemoveObsoleteListeners(listeners, desired);

            for (int i = 0; i < passiveSkills.Length; i++)
            {
                var rt = passiveSkills[i];
                if (rt == null) continue;

                var passiveSkillId = rt.PassiveSkillId;
                if (passiveSkillId <= 0) continue;

                if (!_configs.TryGetPassiveSkill(passiveSkillId, out var mo) || mo == null) continue;

                var triggerIds = mo.TriggerIds;
                if (triggerIds == null || triggerIds.Count == 0) continue;

                for (int j = 0; j < triggerIds.Count; j++)
                {
                    var triggerId = triggerIds[j];
                    if (triggerId <= 0) continue;

                    if (ContainsListener(listeners, passiveSkillId, triggerId))
                    {
                        continue;
                    }

                    if (!_triggerIndex.TryGetByTriggerId(triggerId, out var entries) || entries == null || entries.Count == 0)
                    {
                        continue;
                    }

                    var eventId = entries[0].Def?.EventId;
                    if (string.IsNullOrEmpty(eventId))
                    {
                        continue;
                    }

                    var entryList = new List<PassiveSkillTriggerEntryRuntime>(entries.Count);
                    for (int k = 0; k < entries.Count; k++)
                    {
                        var e = entries[k];
                        if (e.Def == null) continue;
                        entryList.Add(new PassiveSkillTriggerEntryRuntime { Def = e.Def, InitialLocalVars = e.InitialLocalVars });
                    }

                    if (entryList.Count == 0) continue;

                    var l = new PassiveSkillTriggerListenerRuntime
                    {
                        PassiveSkillId = passiveSkillId,
                        TriggerId = triggerId,
                        EventId = eventId,
                        Entries = entryList,
                    };

                    EnsurePassiveSkillContext(entity, listeners, passiveSkillId, l);

                    var handler = new PassiveSkillTriggerEventHandler(this, entity, mo, l);
                    l.Sub = _eventBus.Subscribe(eventId, handler);

                    listeners.Add(l);
                }
            }
        }

        private static long MakeListenerKey(int passiveSkillId, int triggerId)
        {
            unchecked
            {
                return ((long)passiveSkillId << 32) | (uint)triggerId;
            }
        }

        private void RemoveObsoleteListeners(List<PassiveSkillTriggerListenerRuntime> listeners, HashSet<long> desired)
        {
            if (listeners == null || listeners.Count == 0) return;

            var ownerKeys = new HashSet<long>();

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                var l = listeners[i];
                if (l == null) continue;

                var key = MakeListenerKey(l.PassiveSkillId, l.TriggerId);
                if (desired.Contains(key)) continue;

                if (l.SourceContextId != 0)
                {
                    ownerKeys.Add(l.SourceContextId);
                }

                try
                {
                    l.Sub?.Unsubscribe();
                }
                catch
                {
                }
                l.Sub = null;

                listeners.RemoveAt(i);
            }

            if (ownerKeys.Count > 0)
            {
                var frame = GetFrame();
                foreach (var k in ownerKeys)
                {
                    try
                    {
                        _actionRunner?.CancelByOwnerKey(k);
                    }
                    catch
                    {
                    }

                    try
                    {
                        _effectSource?.End(k, frame, EffectSourceEndReason.Cancelled);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void TryUnregister(global::ActorEntity entity)
        {
            if (entity == null) return;
            if (!entity.hasPassiveSkillTriggerListeners) return;

            var listeners = entity.passiveSkillTriggerListeners.Active;
            if (listeners == null || listeners.Count == 0) return;

            var ownerKeys = new HashSet<long>();

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                var l = listeners[i];
                if (l == null) continue;

                if (l.SourceContextId != 0)
                {
                    ownerKeys.Add(l.SourceContextId);
                }

                try
                {
                    l.Sub?.Unsubscribe();
                }
                catch
                {
                }

                l.Sub = null;
                listeners.RemoveAt(i);
            }

            if (ownerKeys.Count > 0)
            {
                var frame = GetFrame();
                foreach (var key in ownerKeys)
                {
                    try
                    {
                        _actionRunner?.CancelByOwnerKey(key);
                    }
                    catch
                    {
                    }

                    try
                    {
                        _effectSource?.End(key, frame, EffectSourceEndReason.Cancelled);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static bool ContainsListener(List<PassiveSkillTriggerListenerRuntime> list, int passiveSkillId, int triggerId)
        {
            if (list == null || list.Count == 0) return false;
            for (int i = 0; i < list.Count; i++)
            {
                var l = list[i];
                if (l == null) continue;
                if (l.PassiveSkillId == passiveSkillId && l.TriggerId == triggerId) return true;
            }
            return false;
        }

        private long GetNowMs()
        {
            return (long)(_frameTime.Time * 1000f);
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

        private void EnsurePassiveSkillContext(global::ActorEntity entity, List<PassiveSkillTriggerListenerRuntime> listeners, int passiveSkillId, PassiveSkillTriggerListenerRuntime l)
        {
            if (entity == null) return;
            if (l == null) return;
            if (l.SourceContextId != 0) return;
            if (_effectSource == null) return;
            if (!entity.hasActorId) return;

            // Reuse existing source context for the same passive skill.
            if (listeners != null)
            {
                for (int i = 0; i < listeners.Count; i++)
                {
                    var existing = listeners[i];
                    if (existing == null) continue;
                    if (existing.PassiveSkillId != passiveSkillId) continue;
                    if (existing.SourceContextId == 0) continue;
                    l.SourceContextId = existing.SourceContextId;
                    return;
                }
            }

            try
            {
                l.SourceContextId = _effectSource.CreateRoot(
                    kind: EffectSourceKind.System,
                    configId: passiveSkillId,
                    sourceActorId: entity.actorId.Value,
                    targetActorId: entity.actorId.Value,
                    frame: GetFrame());
            }
            catch
            {
                l.SourceContextId = 0;
            }
        }

        private PassiveSkillRuntime TryGetPassiveSkillRuntime(global::ActorEntity entity, int passiveSkillId)
        {
            if (entity == null || !entity.hasSkillLoadout) return null;
            var list = entity.skillLoadout.PassiveSkills;
            if (list == null || list.Length == 0) return null;

            for (int i = 0; i < list.Length; i++)
            {
                var rt = list[i];
                if (rt == null) continue;
                if (rt.PassiveSkillId == passiveSkillId) return rt;
            }

            return null;
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
                        for (int i = 0; i < entities.Length; i++)
                        {
                            TryUnregister(entities[i]);
                        }
                    }
                }
            }
            finally
            {
                base.OnTearDown();
            }
        }

        private sealed class PassiveSkillTriggerEventHandler : IEventHandler
        {
            private readonly MobaPassiveSkillTriggerRegisterSystem _sys;
            private readonly global::ActorEntity _entity;
            private readonly PassiveSkillMO _passiveSkill;
            private readonly PassiveSkillTriggerListenerRuntime _listener;

            public PassiveSkillTriggerEventHandler(MobaPassiveSkillTriggerRegisterSystem sys, global::ActorEntity entity, PassiveSkillMO passiveSkill, PassiveSkillTriggerListenerRuntime listener)
            {
                _sys = sys;
                _entity = entity;
                _passiveSkill = passiveSkill;
                _listener = listener;
            }

            public void Handle(in TriggerEvent evt)
            {
                if (_sys == null || _entity == null || _passiveSkill == null || _listener == null) return;

                try
                {
                    var rt = _sys.TryGetPassiveSkillRuntime(_entity, _passiveSkill.Id);
                    if (rt == null) return;

                    var nowMs = _sys.GetNowMs();
                    if (rt.CooldownEndTimeMs > 0 && nowMs < rt.CooldownEndTimeMs)
                    {
                        return;
                    }

                    var entries = _listener.Entries;
                    if (entries == null || entries.Count == 0) return;

                    PooledTriggerArgs mergedArgs = null;
                    try
                    {
                        mergedArgs = PooledTriggerArgs.Rent();
                        if (evt.Args != null)
                        {
                            foreach (var kv in evt.Args)
                            {
                                if (kv.Key == null) continue;
                                mergedArgs[kv.Key] = kv.Value;
                            }
                        }

                        if (_listener.SourceContextId != 0)
                        {
                            mergedArgs[EffectSourceKeys.SourceContextId] = _listener.SourceContextId;
                        }

                        var triggered = false;
                        for (int i = 0; i < entries.Count; i++)
                        {
                            var e = entries[i];
                            var def = e?.Def;
                            if (def == null) continue;

                            if (_sys._triggers.RunOnce(def, source: null, target: null, payload: evt.Payload, args: mergedArgs, initialLocalVars: e.InitialLocalVars))
                            {
                                triggered = true;
                            }
                        }

                        if (triggered && _passiveSkill.CooldownMs > 0)
                        {
                            rt.CooldownEndTimeMs = nowMs + _passiveSkill.CooldownMs;
                        }
                    }
                    finally
                    {
                        mergedArgs?.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaPassiveSkillTriggerRegisterSystem] handler exception (passiveSkillId={_passiveSkill.Id}, triggerId={_listener.TriggerId}, eventId={_listener.EventId})");
                }
            }
        }
    }
}
