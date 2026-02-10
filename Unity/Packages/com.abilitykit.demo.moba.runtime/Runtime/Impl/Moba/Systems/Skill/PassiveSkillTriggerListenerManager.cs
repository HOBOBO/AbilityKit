using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    internal sealed class PassiveSkillTriggerListenerManager
    {
        private readonly MobaConfigDatabase _configs;
        private readonly MobaTriggerIndexService _triggerIndex;
        private readonly EffectSourceRegistry _effectSource;
        private readonly ITriggerActionRunner _actionRunner;

        internal readonly struct Registration
        {
            public readonly PassiveSkillMO PassiveSkill;
            public readonly PassiveSkillTriggerListenerRuntime Listener;

            public Registration(PassiveSkillMO passiveSkill, PassiveSkillTriggerListenerRuntime listener)
            {
                PassiveSkill = passiveSkill;
                Listener = listener;
            }
        }

        public PassiveSkillTriggerListenerManager(
            MobaConfigDatabase configs,
            MobaTriggerIndexService triggerIndex,
            EffectSourceRegistry effectSource,
            ITriggerActionRunner actionRunner)
        {
            _configs = configs;
            _triggerIndex = triggerIndex;
            _effectSource = effectSource;
            _actionRunner = actionRunner;
        }

        public List<PassiveSkillTriggerListenerRuntime> EnsureListenerContainer(global::ActorEntity entity)
        {
            if (entity == null) return null;

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

            return listeners;
        }

        public void TryRegister(global::ActorEntity entity, int frame, List<Registration> outRegistrations)
        {
            if (entity == null) return;
            if (_configs == null || _triggerIndex == null) return;
            if (!entity.hasActorId || !entity.hasSkillLoadout) return;

            var passiveSkills = entity.skillLoadout.PassiveSkills;
            if (passiveSkills == null) passiveSkills = Array.Empty<PassiveSkillRuntime>();

            var listeners = EnsureListenerContainer(entity);
            if (listeners == null) return;

            var desired = BuildDesiredListenerKeys(passiveSkills);
            RemoveObsoleteListeners(listeners, desired, frame);

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

                    EnsurePassiveSkillContext(entity, listeners, passiveSkillId, l, frame);

                    listeners.Add(l);
                    outRegistrations?.Add(new Registration(mo, l));
                }
            }
        }

        public void TryUnregister(global::ActorEntity entity, int frame)
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
                    l.Sub?.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaPassiveSkillTriggerRegisterSystem] unsubscribe passive listener failed (passiveSkillId={l.PassiveSkillId}, triggerId={l.TriggerId})");
                }

                l.Sub = null;
                listeners.RemoveAt(i);
            }

            EndOwnerKeys(ownerKeys, frame);
        }

        private HashSet<long> BuildDesiredListenerKeys(PassiveSkillRuntime[] passiveSkills)
        {
            var desired = new HashSet<long>();
            if (passiveSkills == null || passiveSkills.Length == 0) return desired;

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

            return desired;
        }

        private void RemoveObsoleteListeners(List<PassiveSkillTriggerListenerRuntime> listeners, HashSet<long> desired, int frame)
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
                    l.Sub?.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaPassiveSkillTriggerRegisterSystem] unsubscribe obsolete passive listener failed (passiveSkillId={l.PassiveSkillId}, triggerId={l.TriggerId})");
                }
                l.Sub = null;

                listeners.RemoveAt(i);
            }

            EndOwnerKeys(ownerKeys, frame);
        }

        private void EndOwnerKeys(HashSet<long> ownerKeys, int frame)
        {
            if (ownerKeys == null || ownerKeys.Count == 0) return;

            foreach (var key in ownerKeys)
            {
                try
                {
                    _actionRunner?.CancelByOwnerKey(key);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaPassiveSkillTriggerRegisterSystem] CancelByOwnerKey failed (ownerKey={key})");
                }

                try
                {
                    _effectSource?.End(key, frame, EffectSourceEndReason.Cancelled);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaPassiveSkillTriggerRegisterSystem] EffectSource.End failed (ownerKey={key}, frame={frame})");
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

        private void EnsurePassiveSkillContext(global::ActorEntity entity, List<PassiveSkillTriggerListenerRuntime> listeners, int passiveSkillId, PassiveSkillTriggerListenerRuntime l, int frame)
        {
            if (entity == null) return;
            if (l == null) return;
            if (l.SourceContextId != 0) return;
            if (_effectSource == null) return;
            if (!entity.hasActorId) return;

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
                    frame: frame);
            }
            catch
            {
                l.SourceContextId = 0;
            }
        }

        private static long MakeListenerKey(int passiveSkillId, int triggerId)
        {
            unchecked
            {
                return ((long)passiveSkillId << 32) | (uint)triggerId;
            }
        }
    }
}
