using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Pooling;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Trace;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services.Passive
{
    /// <summary>
    /// 被动技能生命周期服务：集中维护被动技能的运行时 listener、source context 和常驻触发器计划。
    /// </summary>
    [WorldService(typeof(MobaPassiveSkillLifecycleService))]
    public sealed class MobaPassiveSkillLifecycleService : IService, IMobaOwnerBoundTriggerGate, IMobaOwnerBoundTriggerExecutionSourceProvider
    {
        private static readonly ObjectPool<HashSet<long>> s_ownerKeySetPool = Pools.GetPool(
            createFunc: () => new HashSet<long>(),
            onRelease: set => set.Clear(),
            defaultCapacity: 32,
            maxSize: 512,
            collectionCheck: false);

        private static readonly ObjectPool<Dictionary<int, long>> s_ownerKeyByPassiveSkillIdPool = Pools.GetPool(
            createFunc: () => new Dictionary<int, long>(),
            onRelease: dictionary => dictionary.Clear(),
            defaultCapacity: 16,
            maxSize: 256,
            collectionCheck: false);

        private readonly MobaConfigDatabase _configs;
        private readonly MobaTraceRegistry _trace;
        private readonly ITriggerActionRunner _actionRunner;
        private readonly IFrameTime _frameTime;
        private readonly MobaTriggerIntervalContinuousService _continuousProcesses;
        private readonly Dictionary<int, HashSet<long>> _ownerKeysByActor = new Dictionary<int, HashSet<long>>();
        private readonly Dictionary<long, PassiveOwnerBinding> _passiveByOwnerKey = new Dictionary<long, PassiveOwnerBinding>();

        public MobaPassiveSkillLifecycleService(
            MobaConfigDatabase configs,
            MobaTraceRegistry trace = null,
            ITriggerActionRunner actionRunner = null,
            IFrameTime frameTime = null,
            MobaTriggerIntervalContinuousService continuousProcesses = null)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _trace = trace;
            _actionRunner = actionRunner;
            _frameTime = frameTime;
            _continuousProcesses = continuousProcesses;
        }

        /// <summary>
        /// 根据角色当前 SkillLoadout 同步被动运行时；新增被动创建 source context，移除被动取消 owner-bound 触发器。
        /// </summary>
        public void SyncActorPassives(global::ActorEntity entity, int frame)
        {
            if (entity == null) return;
            if (!entity.hasActorId || !entity.hasSkillLoadout) return;

            var listeners = EnsureListenerContainer(entity);
            if (listeners == null) return;

            SyncListeners(entity, listeners, frame);
            SyncOngoingTriggerPlans(entity, listeners, frame);
        }

        /// <summary>
        /// 卸载指定角色的全部被动运行时，并清理被动创建的 trace context 与触发器计划。
        /// </summary>
        public void UnregisterActor(global::ActorEntity entity, int frame)
        {
            if (entity == null) return;

            var actorId = entity.hasActorId ? entity.actorId.Value : 0;
            RemoveOngoingTriggerPlansByOwnerKeys(entity, GetPreviousOwnerKeys(actorId));
            ForgetPreviousOwnerKeys(actorId);
            RemoveAllListeners(entity, frame);
        }

        /// <summary>
        /// 世界关闭时结束仍由本服务持有的 passive root，并释放池化 ownerKey 集合。
        /// </summary>
        public void ReleaseAllOwnedPassiveContexts(int frame = 0)
        {
            if (_ownerKeysByActor.Count > 0)
            {
                var ownedContextIds = new HashSet<long>();
                foreach (var kv in _ownerKeysByActor)
                {
                    var actorOwnedContextIds = kv.Value;
                    if (actorOwnedContextIds == null) continue;

                    foreach (var contextId in actorOwnedContextIds)
                    {
                        if (contextId != 0) ownedContextIds.Add(contextId);
                    }
                }

                EndOwnedPassiveContexts(ownedContextIds, frame);

                foreach (var kv in _ownerKeysByActor)
                {
                    if (kv.Value != null) s_ownerKeySetPool.Release(kv.Value);
                }

                _ownerKeysByActor.Clear();
            }

            _passiveByOwnerKey.Clear();
        }

        public bool IsPassiveOwnerKey(long ownerKey)
        {
            return ownerKey != 0 && _passiveByOwnerKey.ContainsKey(ownerKey);
        }

        public bool IsMatch(long ownerKey, int triggerId)
        {
            return TryGetPassiveBinding(ownerKey, triggerId, out _);
        }

        public bool CanExecute(long ownerKey, int triggerId)
        {
            if (!TryGetPassiveBinding(ownerKey, triggerId, out var binding)) return true;

            var cooldownMs = binding.PassiveSkill.CooldownMs;
            if (cooldownMs <= 0) return true;

            var now = GetCurrentTimeMs();
            return binding.Runtime.CooldownEndTimeMs <= 0L || now >= binding.Runtime.CooldownEndTimeMs;
        }

        public void Complete(long ownerKey, int triggerId)
        {
            if (!TryGetPassiveBinding(ownerKey, triggerId, out var binding)) return;

            var cooldownMs = binding.PassiveSkill.CooldownMs;
            if (cooldownMs <= 0) return;

            binding.Runtime.CooldownDurationMs = cooldownMs;
            binding.Runtime.CooldownEndTimeMs = GetCurrentTimeMs() + cooldownMs;
        }

        public bool TryGetExecutionSource(long ownerKey, int triggerId, out MobaOwnerBoundTriggerExecutionSource source)
        {
            source = default;
            if (!TryGetPassiveBinding(ownerKey, triggerId, out var binding)) return false;
            if (binding.ActorId <= 0 || ownerKey == 0) return false;

            source = new MobaOwnerBoundTriggerExecutionSource(
                binding.ActorId,
                binding.ActorId,
                ownerKey,
                ownerKey,
                ownerKey,
                binding.PassiveSkillId);
            return source.HasExecutionSource;
        }

        public bool CanExecuteOwnerBoundTrigger(long ownerKey, int triggerId)
        {
            return CanExecute(ownerKey, triggerId);
        }

        public void CompleteOwnerBoundTrigger(long ownerKey, int triggerId)
        {
            Complete(ownerKey, triggerId);
        }

        public void Dispose()
        {
            ReleaseAllOwnedPassiveContexts();
        }

        private void SyncListeners(global::ActorEntity entity, List<PassiveSkillTriggerListenerRuntime> listeners, int frame)
        {
            var passiveSkills = entity.skillLoadout.PassiveSkills;
            if (passiveSkills == null) passiveSkills = Array.Empty<PassiveSkillRuntime>();

            var desired = BuildDesiredPassiveSkillIdSet(passiveSkills);
            try
            {
                RemoveObsoleteListeners(listeners, desired, frame);

                for (int i = 0; i < passiveSkills.Length; i++)
                {
                    var runtime = passiveSkills[i];
                    if (runtime == null) continue;

                    var passiveSkillId = runtime.PassiveSkillId;
                    if (passiveSkillId <= 0) continue;

                    if (!_configs.TryGetPassiveSkill(passiveSkillId, out var passiveSkill) || passiveSkill == null)
                    {
                        Log.Warning($"[MobaPassiveSkillLifecycleService] Passive skill config not found. actor={entity.actorId.Value} passiveSkillId={passiveSkillId} frame={frame}");
                        continue;
                    }

                    if (ContainsListener(listeners, passiveSkillId)) continue;

                    var listener = new PassiveSkillTriggerListenerRuntime
                    {
                        PassiveSkillId = passiveSkillId,
                    };

                    EnsurePassiveSkillContext(entity, passiveSkillId, listener, frame);
                    if (listener.SourceContextId == 0)
                    {
                        Log.Warning($"[MobaPassiveSkillLifecycleService] Passive skill listener registered without source context. actor={entity.actorId.Value} passiveSkillId={passiveSkillId} frame={frame} hasTrace={_trace != null}");
                    }

                    listeners.Add(listener);
                }
            }
            finally
            {
                desired.Clear();
            }
        }

        private HashSet<int> BuildDesiredPassiveSkillIdSet(PassiveSkillRuntime[] passiveSkills)
        {
            var desired = new HashSet<int>();
            if (passiveSkills == null || passiveSkills.Length == 0) return desired;

            for (int i = 0; i < passiveSkills.Length; i++)
            {
                var runtime = passiveSkills[i];
                if (runtime == null) continue;

                var passiveSkillId = runtime.PassiveSkillId;
                if (passiveSkillId <= 0) continue;
                if (!_configs.TryGetPassiveSkill(passiveSkillId, out var passiveSkill) || passiveSkill == null) continue;

                desired.Add(passiveSkillId);
            }

            return desired;
        }

        private void RemoveObsoleteListeners(List<PassiveSkillTriggerListenerRuntime> listeners, HashSet<int> desired, int frame)
        {
            if (listeners == null || listeners.Count == 0) return;

            var ownedContextIds = new HashSet<long>();
            try
            {
                for (int i = listeners.Count - 1; i >= 0; i--)
                {
                    var listener = listeners[i];
                    if (listener == null) continue;
                    if (desired != null && desired.Contains(listener.PassiveSkillId)) continue;

                    if (listener.SourceContextId != 0) ownedContextIds.Add(listener.SourceContextId);
                    listeners.RemoveAt(i);
                }

                EndOwnedPassiveContexts(ownedContextIds, frame);
            }
            finally
            {
                ownedContextIds.Clear();
            }
        }

        private void RemoveAllListeners(global::ActorEntity entity, int frame)
        {
            if (entity == null || !entity.hasPassiveSkillTriggerListeners) return;

            var listeners = entity.passiveSkillTriggerListeners.Active;
            if (listeners == null || listeners.Count == 0) return;

            var ownedContextIds = new HashSet<long>();
            try
            {
                for (int i = listeners.Count - 1; i >= 0; i--)
                {
                    var listener = listeners[i];
                    if (listener == null) continue;
                    if (listener.SourceContextId != 0) ownedContextIds.Add(listener.SourceContextId);
                    listeners.RemoveAt(i);
                }

                EndOwnedPassiveContexts(ownedContextIds, frame);
            }
            finally
            {
                ownedContextIds.Clear();
            }
        }

        private void EndOwnedPassiveContexts(IEnumerable<long> ownedContextIds, int frame)
        {
            if (ownedContextIds == null) return;

            foreach (var ownedContextId in ownedContextIds)
            {
                if (ownedContextId == 0) continue;

                // This context is a passive root created by EnsurePassiveSkillContext.
                // Its numeric value is also used as an owner routing key, but owner keys alone
                // never grant permission to end a trace context.
                try
                {
                    _continuousProcesses?.EndOwnerProcesses(ownedContextId, AbilityKit.Continuous.ContinuousEndReason.CleanedUp);
                    _actionRunner?.CancelByOwnerKey(ownedContextId);
                    _passiveByOwnerKey.Remove(ownedContextId);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaPassiveSkillLifecycleService] Owner runtime cleanup failed. ownerKey={ownedContextId}");
                }

                try
                {
                    _trace?.EndContext(ownedContextId, TraceLifecycleReason.Cancelled);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaPassiveSkillLifecycleService] Trace.EndContext failed. ownedContextId={ownedContextId} frame={frame}");
                }
            }
        }

        private void EnsurePassiveSkillContext(global::ActorEntity entity, int passiveSkillId, PassiveSkillTriggerListenerRuntime listener, int frame)
        {
            if (entity == null || listener == null) return;
            if (listener.SourceContextId != 0) return;
            if (!entity.hasActorId) return;

            if (_trace == null)
            {
                Log.Warning($"[MobaPassiveSkillLifecycleService] Cannot create passive skill source context because trace registry is missing. passiveSkillId={passiveSkillId} frame={frame}");
                return;
            }

            try
            {
                var actorId = entity.actorId.Value;
                listener.SourceContextId = _trace.CreateRootContext(
                    MobaTraceKind.SkillEffect,
                    passiveSkillId,
                    actorId,
                    actorId,
                    TraceEndpoint.Config(MobaRuntimeKindNames.Skill, passiveSkillId),
                    TraceEndpoint.Actor(actorId));
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaPassiveSkillLifecycleService] Trace.CreateRootContext failed. actor={entity.actorId.Value} passiveSkillId={passiveSkillId} frame={frame}");
                listener.SourceContextId = 0;
            }
        }

        private void SyncOngoingTriggerPlans(global::ActorEntity entity, List<PassiveSkillTriggerListenerRuntime> listeners, int frame)
        {
            if (entity == null) return;
            if (!entity.hasActorId || !entity.hasSkillLoadout) return;

            var actorId = entity.actorId.Value;
            var desiredOwnerKeys = s_ownerKeySetPool.Get();
            var ownerKeyByPassiveSkillId = s_ownerKeyByPassiveSkillIdPool.Get();

            try
            {
                if (listeners != null)
                {
                    for (int i = 0; i < listeners.Count; i++)
                    {
                        var listener = listeners[i];
                        if (listener == null) continue;
                        if (listener.PassiveSkillId <= 0) continue;
                        if (listener.SourceContextId == 0) continue;

                        if (!ownerKeyByPassiveSkillId.ContainsKey(listener.PassiveSkillId))
                        {
                            ownerKeyByPassiveSkillId[listener.PassiveSkillId] = listener.SourceContextId;
                            desiredOwnerKeys.Add(listener.SourceContextId);
                        }
                    }
                }

                RemoveStaleOwnerRuntimeState(entity, actorId, desiredOwnerKeys);
                SyncPassiveOwnerBindings(entity, ownerKeyByPassiveSkillId);
                ApplyDesiredOngoingTriggerPlans(entity, ownerKeyByPassiveSkillId);
                SyncPassiveTriggerIntervalContinuouses(entity, ownerKeyByPassiveSkillId, frame);
                StorePreviousOwnerKeys(actorId, desiredOwnerKeys);
            }
            finally
            {
                s_ownerKeyByPassiveSkillIdPool.Release(ownerKeyByPassiveSkillId);
                s_ownerKeySetPool.Release(desiredOwnerKeys);
            }
        }

        private void RemoveStaleOwnerRuntimeState(global::ActorEntity entity, int actorId, HashSet<long> desiredOwnerKeys)
        {
            var previous = GetPreviousOwnerKeys(actorId);
            if (previous == null || previous.Count == 0) return;

            foreach (var ownerKey in previous)
            {
                if (desiredOwnerKeys != null && desiredOwnerKeys.Contains(ownerKey)) continue;

                _continuousProcesses?.EndOwnerProcesses(ownerKey, AbilityKit.Continuous.ContinuousEndReason.CleanedUp);
                _passiveByOwnerKey.Remove(ownerKey);
            }
        }

        private void SyncPassiveOwnerBindings(global::ActorEntity entity, Dictionary<int, long> ownerKeyByPassiveSkillId)
        {
            if (entity == null || !entity.hasSkillLoadout || ownerKeyByPassiveSkillId == null) return;
            if (!entity.hasActorId) return;

            var actorId = entity.actorId.Value;
            var passiveSkills = entity.skillLoadout.PassiveSkills;
            if (passiveSkills == null || passiveSkills.Length == 0) return;

            foreach (var kv in ownerKeyByPassiveSkillId)
            {
                var passiveSkillId = kv.Key;
                var ownerKey = kv.Value;
                if (passiveSkillId <= 0 || ownerKey == 0) continue;
                if (!_configs.TryGetPassiveSkill(passiveSkillId, out var passiveSkill) || passiveSkill == null) continue;
                if (!TryGetPassiveRuntime(passiveSkills, passiveSkillId, out var runtime) || runtime == null) continue;

                _passiveByOwnerKey[ownerKey] = new PassiveOwnerBinding(actorId, passiveSkillId, runtime, passiveSkill);
            }
        }

        private static bool TryGetPassiveRuntime(PassiveSkillRuntime[] passiveSkills, int passiveSkillId, out PassiveSkillRuntime runtime)
        {
            runtime = null;
            if (passiveSkills == null || passiveSkillId <= 0) return false;

            for (int i = 0; i < passiveSkills.Length; i++)
            {
                var candidate = passiveSkills[i];
                if (candidate == null || candidate.PassiveSkillId != passiveSkillId) continue;

                runtime = candidate;
                return true;
            }

            return false;
        }

        private void SyncPassiveTriggerIntervalContinuouses(global::ActorEntity entity, Dictionary<int, long> ownerKeyByPassiveSkillId, int frame)
        {
            if (_continuousProcesses == null) return;
            if (entity == null || !entity.hasActorId || ownerKeyByPassiveSkillId == null || ownerKeyByPassiveSkillId.Count == 0) return;

            var actorId = entity.actorId.Value;
            foreach (var kv in ownerKeyByPassiveSkillId)
            {
                var passiveSkillId = kv.Key;
                var ownerKey = kv.Value;
                if (passiveSkillId <= 0 || ownerKey == 0) continue;
                if (!_configs.TryGetPassiveSkill(passiveSkillId, out var passiveSkill) || passiveSkill == null) continue;

                var processIds = passiveSkill.ContinuousProcessIds;
                _continuousProcesses.EndMissingOwnerProcesses(ownerKey, processIds, AbilityKit.Continuous.ContinuousEndReason.CleanedUp);
                if (processIds == null || processIds.Count == 0)
                {
                    _continuousProcesses.ReconcileOwner(ownerKey);
                    continue;
                }

                for (int i = 0; i < processIds.Count; i++)
                {
                    var processId = processIds[i];
                    if (processId <= 0) continue;

                    var source = new MobaContextSourceView(
                        MobaContextSourceResolveKind.DirectProvider,
                        MobaContextSourceBoundary.LiveRuntime,
                        EffectContextKind.ContinuousPeriodic,
                        MobaTraceKind.EffectExecution,
                        actorId,
                        actorId,
                        ownerKey,
                        ownerKey,
                        ownerKey,
                        ownerKey,
                        processId,
                        0,
                        frame,
                        "TriggerIntervalContinuous",
                        processId,
                        true,
                        default);
                    _continuousProcesses.UpsertProcess(processId, actorId, actorId, ownerKey, in source);
                }

                _continuousProcesses.ReconcileOwner(ownerKey);
            }
        }

        private void ApplyDesiredOngoingTriggerPlans(global::ActorEntity entity, Dictionary<int, long> ownerKeyByPassiveSkillId)
        {
            if (entity == null) return;

            var desired = new List<OngoingTriggerPlanEntry>(ownerKeyByPassiveSkillId?.Count ?? 0);
            if (ownerKeyByPassiveSkillId != null)
            {
                foreach (var kv in ownerKeyByPassiveSkillId)
                {
                    var passiveSkillId = kv.Key;
                    var ownerKey = kv.Value;
                    if (ownerKey == 0) continue;
                    if (!_configs.TryGetPassiveSkill(passiveSkillId, out var passiveSkill) || passiveSkill == null) continue;

                    var triggerIds = passiveSkill.TriggerIds;
                    if (triggerIds == null || triggerIds.Count == 0) continue;

                    var ids = new int[triggerIds.Count];
                    for (int i = 0; i < triggerIds.Count; i++) ids[i] = triggerIds[i];
                    desired.Add(new OngoingTriggerPlanEntry { OwnerKey = ownerKey, TriggerIds = ids });
                }
            }

            var current = entity.hasOngoingTriggerPlans ? entity.ongoingTriggerPlans.Active : null;
            if (AreOngoingTriggerPlansEqual(current, desired)) return;

            if (desired.Count == 0)
            {
                if (entity.hasOngoingTriggerPlans) entity.RemoveOngoingTriggerPlans();
                return;
            }

            var revision = entity.hasOngoingTriggerPlans ? entity.ongoingTriggerPlans.Revision + 1 : 1;
            if (entity.hasOngoingTriggerPlans) entity.ReplaceOngoingTriggerPlans(desired, revision);
            else entity.AddOngoingTriggerPlans(desired, revision);
        }

        private List<PassiveSkillTriggerListenerRuntime> EnsureListenerContainer(global::ActorEntity entity)
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

        private static bool ContainsListener(List<PassiveSkillTriggerListenerRuntime> list, int passiveSkillId)
        {
            if (list == null || list.Count == 0) return false;

            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item == null) continue;
                if (item.PassiveSkillId == passiveSkillId) return true;
            }

            return false;
        }

        private static bool AreOngoingTriggerPlansEqual(
            IReadOnlyList<OngoingTriggerPlanEntry> current,
            IReadOnlyList<OngoingTriggerPlanEntry> desired)
        {
            var currentCount = current?.Count ?? 0;
            var desiredCount = desired?.Count ?? 0;
            if (currentCount != desiredCount) return false;
            if (currentCount == 0) return true;

            for (int i = 0; i < desiredCount; i++)
            {
                var desiredEntry = desired[i];
                if (desiredEntry == null) return false;

                OngoingTriggerPlanEntry currentEntry = null;
                for (int j = 0; j < currentCount; j++)
                {
                    var candidate = current[j];
                    if (candidate != null && candidate.OwnerKey == desiredEntry.OwnerKey)
                    {
                        currentEntry = candidate;
                        break;
                    }
                }

                if (currentEntry == null || !AreTriggerIdsEqual(currentEntry.TriggerIds, desiredEntry.TriggerIds)) return false;
            }

            return true;
        }

        private static bool AreTriggerIdsEqual(int[] left, int[] right)
        {
            var leftLength = left?.Length ?? 0;
            var rightLength = right?.Length ?? 0;
            if (leftLength != rightLength) return false;

            for (int i = 0; i < leftLength; i++)
            {
                if (left[i] != right[i]) return false;
            }

            return true;
        }

        private void RemoveOngoingTriggerPlansByOwnerKeys(global::ActorEntity entity, IEnumerable<long> ownerKeys)
        {
            if (entity == null) return;
            if (ownerKeys == null) return;
            if (!entity.hasOngoingTriggerPlans) return;

            var oldList = entity.ongoingTriggerPlans.Active;
            if (oldList == null || oldList.Count == 0) return;

            var toRemove = s_ownerKeySetPool.Get();
            try
            {
                foreach (var ownerKey in ownerKeys)
                {
                    if (ownerKey != 0) toRemove.Add(ownerKey);
                }

                if (toRemove.Count == 0) return;

                var newList = new List<OngoingTriggerPlanEntry>(oldList.Count);
                var removedAny = false;

                for (int i = 0; i < oldList.Count; i++)
                {
                    var item = oldList[i];
                    if (item == null) continue;

                    if (toRemove.Contains(item.OwnerKey))
                    {
                        removedAny = true;
                        continue;
                    }

                    newList.Add(new OngoingTriggerPlanEntry { OwnerKey = item.OwnerKey, TriggerIds = item.TriggerIds });
                }

                if (!removedAny) return;

                foreach (var ownerKey in toRemove)
                {
                    _continuousProcesses?.EndOwnerProcesses(ownerKey, AbilityKit.Continuous.ContinuousEndReason.CleanedUp);
                    _passiveByOwnerKey.Remove(ownerKey);
                }

                var revision = entity.ongoingTriggerPlans.Revision + 1;
                if (newList.Count == 0) entity.RemoveOngoingTriggerPlans();
                else entity.ReplaceOngoingTriggerPlans(newList, revision);
            }
            finally
            {
                s_ownerKeySetPool.Release(toRemove);
            }
        }

        private HashSet<long> GetPreviousOwnerKeys(int actorId)
        {
            if (actorId <= 0) return null;
            return _ownerKeysByActor.TryGetValue(actorId, out var set) ? set : null;
        }

        private void StorePreviousOwnerKeys(int actorId, HashSet<long> desired)
        {
            if (actorId <= 0) return;

            if (_ownerKeysByActor.TryGetValue(actorId, out var oldSet))
            {
                _ownerKeysByActor.Remove(actorId);
                s_ownerKeySetPool.Release(oldSet);
            }

            var stored = s_ownerKeySetPool.Get();
            if (desired != null)
            {
                foreach (var ownerKey in desired)
                {
                    if (ownerKey != 0) stored.Add(ownerKey);
                }
            }

            _ownerKeysByActor[actorId] = stored;
        }

        private void ForgetPreviousOwnerKeys(int actorId)
        {
            if (actorId <= 0) return;
            if (!_ownerKeysByActor.TryGetValue(actorId, out var set)) return;

            foreach (var ownerKey in set)
            {
                _continuousProcesses?.EndOwnerProcesses(ownerKey, AbilityKit.Continuous.ContinuousEndReason.CleanedUp);
                _passiveByOwnerKey.Remove(ownerKey);
            }

            _ownerKeysByActor.Remove(actorId);
            s_ownerKeySetPool.Release(set);
        }

        private long GetCurrentTimeMs()
        {
            return MobaSkillRuntimeAccess.GetCurrentTimeMs(_frameTime);
        }

        private bool TryGetPassiveBinding(long ownerKey, int triggerId, out PassiveOwnerBinding binding)
        {
            binding = null;
            if (ownerKey == 0 || triggerId <= 0) return false;
            if (!_passiveByOwnerKey.TryGetValue(ownerKey, out binding) || binding == null) return false;
            if (binding.PassiveSkill == null || binding.Runtime == null) return false;
            return ContainsTriggerId(binding.PassiveSkill.TriggerIds, triggerId);
        }

        private static bool ContainsTriggerId(IReadOnlyList<int> triggerIds, int triggerId)
        {
            if (triggerIds == null || triggerIds.Count == 0) return false;
            for (int i = 0; i < triggerIds.Count; i++)
            {
                if (triggerIds[i] == triggerId) return true;
            }

            return false;
        }

        private sealed class PassiveOwnerBinding
        {
            public readonly int ActorId;
            public readonly int PassiveSkillId;
            public readonly PassiveSkillRuntime Runtime;
            public readonly PassiveSkillMO PassiveSkill;

            public PassiveOwnerBinding(int actorId, int passiveSkillId, PassiveSkillRuntime runtime, PassiveSkillMO passiveSkill)
            {
                ActorId = actorId;
                PassiveSkillId = passiveSkillId;
                Runtime = runtime;
                PassiveSkill = passiveSkill;
            }
        }
    }
}

