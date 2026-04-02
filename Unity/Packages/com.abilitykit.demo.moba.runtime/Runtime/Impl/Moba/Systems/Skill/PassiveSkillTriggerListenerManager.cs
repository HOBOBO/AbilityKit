using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO;
using AbilityKit.Ability.Impl.Moba.Components;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    internal sealed class PassiveSkillTriggerListenerManager
    {
        private readonly MobaConfigDatabase _configs;
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
            EffectSourceRegistry effectSource,
            ITriggerActionRunner actionRunner)
        {
            _configs = configs;
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
            if (_configs == null) return;
            if (!entity.hasActorId || !entity.hasSkillLoadout) return;

            var passiveSkills = entity.skillLoadout.PassiveSkills;
            if (passiveSkills == null) passiveSkills = Array.Empty<PassiveSkillRuntime>();

            var listeners = EnsureListenerContainer(entity);
            if (listeners == null) return;

            var desired = BuildDesiredPassiveSkillIdSet(passiveSkills);
            RemoveObsoleteListeners(listeners, desired, frame);

            for (int i = 0; i < passiveSkills.Length; i++)
            {
                var rt = passiveSkills[i];
                if (rt == null) continue;

                var passiveSkillId = rt.PassiveSkillId;
                if (passiveSkillId <= 0) continue;

                if (!_configs.TryGetPassiveSkill(passiveSkillId, out var mo) || mo == null) continue;

                if (ContainsListener(listeners, passiveSkillId))
                {
                    continue;
                }

                var l = new PassiveSkillTriggerListenerRuntime
                {
                    PassiveSkillId = passiveSkillId,
                };

                EnsurePassiveSkillContext(entity, listeners, passiveSkillId, l, frame);

                listeners.Add(l);
                outRegistrations?.Add(new Registration(mo, l));
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

                listeners.RemoveAt(i);
            }

            EndOwnerKeys(ownerKeys, frame);
        }

        private HashSet<int> BuildDesiredPassiveSkillIdSet(PassiveSkillRuntime[] passiveSkills)
        {
            var desired = new HashSet<int>();
            if (passiveSkills == null || passiveSkills.Length == 0) return desired;

            for (int i = 0; i < passiveSkills.Length; i++)
            {
                var rt = passiveSkills[i];
                if (rt == null) continue;
                var passiveSkillId = rt.PassiveSkillId;
                if (passiveSkillId <= 0) continue;

                if (!_configs.TryGetPassiveSkill(passiveSkillId, out var mo) || mo == null) continue;
                desired.Add(passiveSkillId);
            }

            return desired;
        }

        private void RemoveObsoleteListeners(List<PassiveSkillTriggerListenerRuntime> listeners, HashSet<int> desired, int frame)
        {
            if (listeners == null || listeners.Count == 0) return;

            var ownerKeys = new HashSet<long>();

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                var l = listeners[i];
                if (l == null) continue;

                if (desired.Contains(l.PassiveSkillId)) continue;

                if (l.SourceContextId != 0)
                {
                    ownerKeys.Add(l.SourceContextId);
                }

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

        private static bool ContainsListener(List<PassiveSkillTriggerListenerRuntime> list, int passiveSkillId)
        {
            if (list == null || list.Count == 0) return false;

            for (int i = 0; i < list.Count; i++)
            {
                var it = list[i];
                if (it == null) continue;
                if (it.PassiveSkillId == passiveSkillId) return true;
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
    }
}
