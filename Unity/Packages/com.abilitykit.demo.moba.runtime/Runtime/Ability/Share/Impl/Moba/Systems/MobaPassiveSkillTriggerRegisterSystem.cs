using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Effect;
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

        public MobaPassiveSkillTriggerRegisterSystem(global::Entitas.IContexts contexts, IWorldServices services)
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

                    // Event-driven triggers subscribe to EventBus; empty EventId triggers execute immediately once.
                    if (!string.IsNullOrEmpty(eventId))
                    {
                        var handler = new PassiveSkillTriggerEventHandler(this, entity, mo, l);
                        l.Sub = _eventBus.Subscribe(eventId, handler);
                    }

                    listeners.Add(l);

                    if (string.IsNullOrEmpty(eventId))
                    {
                        ExecutePassiveTriggerOnce(entity, mo, l);
                    }
                }
            }
        }

        private void ExecutePassiveTriggerOnce(global::ActorEntity entity, PassiveSkillMO passiveSkill, PassiveSkillTriggerListenerRuntime listener)
        {
            if (entity == null || passiveSkill == null || listener == null) return;
            if (!entity.hasActorId) return;

            try
            {
                var rt = TryGetPassiveSkillRuntime(entity, passiveSkill.Id);
                if (rt == null) return;

                var nowMs = GetNowMs();
                if (rt.CooldownEndTimeMs > 0 && nowMs < rt.CooldownEndTimeMs)
                {
                    return;
                }

                var entries = listener.Entries;
                if (entries == null || entries.Count == 0) return;

                // For direct-execute passives, synthesize minimal args so that triggers can access sourceContextId/sourceActorId.
                var args = PooledTriggerArgs.Rent();
                try
                {
                    var selfId = entity.actorId.Value;
                    args[EffectSourceKeys.SourceActorId] = selfId;
                    args[EffectSourceKeys.TargetActorId] = selfId;
                    args[EffectTriggering.Args.Source] = selfId;
                    args[EffectTriggering.Args.Target] = selfId;
                    if (listener.SourceContextId != 0)
                    {
                        args[EffectSourceKeys.SourceContextId] = listener.SourceContextId;

                        EffectOriginArgsHelper.FillFromRegistry(args, listener.SourceContextId, _effectSource);
                    }

                    if (!args.ContainsKey(EffectTriggering.Args.OriginSource)) args[EffectTriggering.Args.OriginSource] = selfId;
                    if (!args.ContainsKey(EffectTriggering.Args.OriginTarget)) args[EffectTriggering.Args.OriginTarget] = selfId;

                    var triggered = false;
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var e = entries[i];
                        var def = e?.Def;
                        if (def == null) continue;

                        // Direct execute is always treated as internal (not external event).
                        if (_triggers.RunOnce(def, source: entity, target: entity, payload: null, args: args, initialLocalVars: e.InitialLocalVars))
                        {
                            triggered = true;
                        }
                    }

                    if (triggered && passiveSkill.CooldownMs > 0)
                    {
                        rt.CooldownEndTimeMs = nowMs + passiveSkill.CooldownMs;
                    }
                }
                finally
                {
                    args.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaPassiveSkillTriggerRegisterSystem] ExecutePassiveTriggerOnce exception (passiveSkillId={passiveSkill.Id}, triggerId={listener.TriggerId})");
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
                    if (string.IsNullOrEmpty(_listener.EventId))
                    {
                        // Empty EventId means direct-execute trigger; it shouldn't be invoked by EventBus.
                        return;
                    }

                    var isExternalEvent = false;
                    if (_entity.hasActorId)
                    {
                        var selfId = _entity.actorId.Value;
                        if (TryGetEventSourceActorId(in evt, out var sourceActorId) && sourceActorId != 0 && sourceActorId != selfId)
                        {
                            isExternalEvent = true;
                        }
                    }

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
                    var injectedArgs = false;
                    var injectedOldExists = false;
                    object injectedOldValue = null;
                    IDictionary<string, object> mutableArgs = null;
                    try
                    {
                        var args = evt.Args;
                        if (_listener.SourceContextId != 0 && args != null)
                        {
                            mutableArgs = args as IDictionary<string, object>;
                            if (mutableArgs != null)
                            {
                                injectedOldExists = mutableArgs.TryGetValue(EffectSourceKeys.SourceContextId, out injectedOldValue);
                                mutableArgs[EffectSourceKeys.SourceContextId] = _listener.SourceContextId;
                                injectedArgs = true;
                            }
                            else
                            {
                                // Args is read-only; fall back to copying once.
                                mergedArgs = PooledTriggerArgs.Rent();
                                foreach (var kv in args)
                                {
                                    if (kv.Key == null) continue;
                                    mergedArgs[kv.Key] = kv.Value;
                                }
                                mergedArgs[EffectSourceKeys.SourceContextId] = _listener.SourceContextId;
                                args = mergedArgs;
                            }
                        }

                        // Inject a standard flag for whether this event is external to the owner.
                        // Triggers should gate on this via an explicit condition: arg_eq(key='common.is_external', value=0).
                        if (args != null)
                        {
                            if (args is IDictionary<string, object> dict)
                            {
                                dict["common.is_external"] = isExternalEvent ? 1 : 0;
                            }
                            else
                            {
                                if (mergedArgs == null)
                                {
                                    mergedArgs = PooledTriggerArgs.Rent();
                                    foreach (var kv in args)
                                    {
                                        if (kv.Key == null) continue;
                                        mergedArgs[kv.Key] = kv.Value;
                                    }
                                    args = mergedArgs;
                                }
                                mergedArgs["common.is_external"] = isExternalEvent ? 1 : 0;
                            }
                        }

                        var triggered = false;
                        for (int i = 0; i < entries.Count; i++)
                        {
                            var e = entries[i];
                            var def = e?.Def;
                            if (def == null) continue;

                            if (_sys._triggers.RunOnce(def, source: null, target: null, payload: evt.Payload, args: args, initialLocalVars: e.InitialLocalVars))
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
                        if (injectedArgs && mutableArgs != null)
                        {
                            if (injectedOldExists)
                            {
                                mutableArgs[EffectSourceKeys.SourceContextId] = injectedOldValue;
                            }
                            else
                            {
                                mutableArgs.Remove(EffectSourceKeys.SourceContextId);
                            }
                        }
                        mergedArgs?.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaPassiveSkillTriggerRegisterSystem] handler exception (passiveSkillId={_passiveSkill.Id}, triggerId={_listener.TriggerId}, eventId={_listener.EventId})");
                }
            }

            private static bool TryGetEventSourceActorId(in TriggerEvent evt, out int actorId)
            {
                actorId = 0;

                if (evt.Payload is SkillCastContext sc)
                {
                    actorId = sc.CasterActorId;
                    return actorId != 0;
                }

                if (evt.Payload is SkillCastRequest req)
                {
                    actorId = req.CasterActorId;
                    return actorId != 0;
                }

                var args = evt.Args;
                if (args == null) return false;

                if (args.TryGetValue(MobaSkillTriggering.Args.CasterActorId, out var v) && v is int casterId)
                {
                    actorId = casterId;
                    return true;
                }

                if (args.TryGetValue(EffectSourceKeys.SourceActorId, out v) && v is int sourceActorId)
                {
                    actorId = sourceActorId;
                    return true;
                }

                return false;
            }
        }
    }
}

