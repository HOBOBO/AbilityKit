using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaBuffService : IService
    {
        private readonly MobaConfigDatabase _configs;
        private readonly IEventBus _eventBus;
        private readonly ITriggerActionRunner _actionRunner;
        private readonly EffectSourceRegistry _effectSource;
        private readonly IFrameTime _frameTime;
        private readonly MobaActorLookupService _actors;
        private readonly MobaEffectExecutionService _effectExec;

        public MobaBuffService(MobaConfigDatabase configs, IEventBus eventBus, ITriggerActionRunner actionRunner, EffectSourceRegistry effectSource, IFrameTime frameTime, MobaActorLookupService actors, MobaEffectExecutionService effectExec)
        {
            _configs = configs;
            _eventBus = eventBus;
            _actionRunner = actionRunner;
            _effectSource = effectSource;
            _frameTime = frameTime;
            _actors = actors;
            _effectExec = effectExec;
        }

        public global::ActorEntity TryGetActorEntity(int actorId)
        {
            if (_actors != null && _actors.TryGetActorEntity(actorId, out var e) && e != null)
            {
                return e;
            }
            return null;
        }

        public bool ApplyBuffImmediate(global::ActorEntity target, int buffId, int sourceActorId, int durationOverrideMs)
        {
            if (target == null) return false;
            if (!target.hasActorId) return false;
            if (buffId <= 0) return false;

            if (_configs == null) return false;
            if (!_configs.TryGetBuff(buffId, out var buff) || buff == null) return false;

            if (target.hasApplyBuffRequest && target.applyBuffRequest != null && target.applyBuffRequest.BuffId == buffId)
            {
                target.RemoveApplyBuffRequest();
            }

            if (!target.hasBuffs)
            {
                target.AddBuffs(new List<BuffRuntime>());
            }

            var list = target.buffs.Active;
            if (list == null)
            {
                list = new List<BuffRuntime>();
                target.ReplaceBuffs(list);
            }

            var duration = durationOverrideMs > 0 ? durationOverrideMs : buff.DurationMs;
            var durationSeconds = duration > 0 ? duration / 1000f : 0f;
            var targetActorId = target.actorId.Value;

            var existingIndex = FindExistingBuffIndex(list, buff.Id);
            if (existingIndex >= 0)
            {
                var rt = list[existingIndex];
                var applied = ApplyToExisting(rt, buff, sourceActorId, durationSeconds, _effectSource, _actionRunner, GetFrame(), targetActorId);
                EnsureBuffContext(rt, buff.Id, sourceActorId, targetActorId, _effectSource, GetFrame());
                PublishBuffEvent(_eventBus, MobaBuffTriggering.Events.ApplyOrRefresh, buff, sourceActorId, targetActorId, durationSeconds, rt);
                if (applied)
                {
                    ResetInterval(rt, buff);
                    ExecuteStageEffects(buff.OnAddEffects, sourceActorId: sourceActorId, targetActorId: targetActorId, targetUnit: null);
                    PublishBuffPerEffect(_eventBus, MobaBuffTriggering.Events.ApplyOrRefresh, buff.OnAddEffects, stage: "add", sourceActorId: sourceActorId, targetActorId: targetActorId, rt);
                }
                return true;
            }

            var created = CreateNewRuntime(buff, sourceActorId, durationSeconds);
            EnsureBuffContext(created, buff.Id, sourceActorId, targetActorId, _effectSource, GetFrame());
            list.Add(created);
            PublishBuffEvent(_eventBus, MobaBuffTriggering.Events.ApplyOrRefresh, buff, sourceActorId, targetActorId, durationSeconds, created);
            ResetInterval(created, buff);
            ExecuteStageEffects(buff.OnAddEffects, sourceActorId: sourceActorId, targetActorId: targetActorId, targetUnit: null);
            PublishBuffPerEffect(_eventBus, MobaBuffTriggering.Events.ApplyOrRefresh, buff.OnAddEffects, stage: "add", sourceActorId: sourceActorId, targetActorId: targetActorId, created);
            return true;
        }

        public bool RemoveBuffImmediate(global::ActorEntity target, int buffId, int sourceActorId, EffectSourceEndReason reason)
        {
            if (target == null) return false;
            if (buffId <= 0) return false;

            if (target.hasApplyBuffRequest && target.applyBuffRequest != null && target.applyBuffRequest.BuffId == buffId)
            {
                target.RemoveApplyBuffRequest();
            }

            if (!target.hasBuffs) return false;

            var list = target.buffs.Active;
            if (list == null || list.Count == 0) return false;

            var removed = false;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var b = list[i];
                if (b == null) continue;
                if (b.BuffId != buffId) continue;

                removed = true;

                try
                {
                    if (b.SourceContextId != 0)
                    {
                        _actionRunner?.CancelByOwnerKey(b.SourceContextId);
                    }
                }
                catch
                {
                }

                var normalizedReason = reason;
                if (normalizedReason == EffectSourceEndReason.None) normalizedReason = EffectSourceEndReason.Dispelled;

                try
                {
                    if (b.SourceContextId != 0)
                    {
                        _effectSource?.End(b.SourceContextId, GetFrame(), normalizedReason);
                    }
                }
                catch
                {
                }

                if (_configs != null)
                {
                    if (_configs.TryGetBuff(b.BuffId, out var buff) && buff != null)
                    {
                        PublishBuffRemove(_eventBus, buff, sourceActorId, target.actorId.Value, b, normalizedReason);
                        ExecuteStageEffects(buff.OnRemoveEffects, sourceActorId: sourceActorId, targetActorId: target.actorId.Value, targetUnit: null);
                    }
                }

                list.RemoveAt(i);
            }

            return removed;
        }

        private static int FindExistingBuffIndex(List<BuffRuntime> list, int buffId)
        {
            if (list == null) return -1;

            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b == null) continue;
                if (b.BuffId == buffId) return i;
            }

            return -1;
        }

        private static bool ApplyToExisting(BuffRuntime existing, BuffMO buff, int sourceActorId, float durationSeconds, EffectSourceRegistry effectSource, ITriggerActionRunner actionRunner, int frame, int targetActorId)
        {
            if (existing == null) return false;

            switch (buff.StackingPolicy)
            {
                case BuffStackingPolicy.IgnoreIfExists:
                    return false;
                case BuffStackingPolicy.Replace:
                    CancelAndEnd(existing, effectSource, actionRunner, frame);
                    existing.SourceId = sourceActorId;
                    existing.StackCount = 0;
                    existing.Remaining = durationSeconds;
                    AddStack(existing, buff.MaxStacks);
                    return true;
                case BuffStackingPolicy.AddStack:
                    AddStack(existing, buff.MaxStacks);
                    RefreshRemaining(existing, buff.RefreshPolicy, durationSeconds);
                    existing.SourceId = sourceActorId;
                    return true;
                case BuffStackingPolicy.RefreshDuration:
                    RefreshRemaining(existing, buff.RefreshPolicy, durationSeconds);
                    existing.SourceId = sourceActorId;
                    return true;
                case BuffStackingPolicy.None:
                default:
                    return false;
            }
        }

        private static BuffRuntime CreateNewRuntime(BuffMO buff, int sourceActorId, float durationSeconds)
        {
            var rt = new BuffRuntime
            {
                BuffId = buff.Id,
                Remaining = durationSeconds,
                IntervalRemainingSeconds = 0,
                SourceId = sourceActorId,
                StackCount = 0,
                SourceContextId = 0,
            };

            AddStack(rt, buff.MaxStacks);
            return rt;
        }

        private static void RefreshRemaining(BuffRuntime rt, BuffRefreshPolicy policy, float durationSeconds)
        {
            if (rt == null) return;

            switch (policy)
            {
                case BuffRefreshPolicy.ResetRemaining:
                    rt.Remaining = durationSeconds;
                    return;
                case BuffRefreshPolicy.AddRemaining:
                    rt.Remaining += durationSeconds;
                    return;
                case BuffRefreshPolicy.KeepRemaining:
                case BuffRefreshPolicy.None:
                default:
                    return;
            }
        }

        private static void AddStack(BuffRuntime rt, int maxStacks)
        {
            if (rt == null) return;

            if (maxStacks <= 0) maxStacks = int.MaxValue;
            if (rt.StackCount >= maxStacks) return;

            rt.StackCount++;
        }

        private static void PublishBuffEvent(IEventBus bus, string eventId, BuffMO buff, int sourceActorId, int targetActorId, float durationSeconds, BuffRuntime runtime)
        {
            if (bus == null) return;
            if (buff == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            PublishOnce(bus, eventId, buff, sourceActorId, targetActorId, durationSeconds, runtime);
        }

        private static void PublishOnce(IEventBus bus, string eventId, BuffMO buff, int sourceActorId, int targetActorId, float durationSeconds, BuffRuntime runtime)
        {
            if (bus == null) return;
            if (buff == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            var args = PooledTriggerArgs.Rent();
            args[EffectTriggering.Args.Source] = sourceActorId;
            args[EffectTriggering.Args.Target] = targetActorId;
            args[EffectSourceKeys.SourceActorId] = sourceActorId;
            args[EffectSourceKeys.TargetActorId] = targetActorId;
            args[MobaBuffTriggering.Args.BuffId] = buff.Id;
            args[MobaBuffTriggering.Args.EffectId] = 0;
            args[MobaBuffTriggering.Args.DurationSeconds] = durationSeconds;
            args[MobaBuffTriggering.Args.StackCount] = runtime != null ? runtime.StackCount : 0;
            if (runtime != null && runtime.SourceContextId != 0)
            {
                args[EffectSourceKeys.SourceContextId] = runtime.SourceContextId;
            }

            bus.Publish(new TriggerEvent(eventId, payload: runtime, args: args));
        }

        private static void EnsureBuffContext(BuffRuntime rt, int buffId, int sourceActorId, int targetActorId, EffectSourceRegistry effectSource, int frame)
        {
            if (rt == null) return;
            if (rt.SourceContextId != 0) return;
            if (effectSource == null) return;

            rt.SourceContextId = effectSource.CreateRoot(
                kind: EffectSourceKind.Buff,
                configId: buffId,
                sourceActorId: sourceActorId,
                targetActorId: targetActorId,
                frame: frame);
        }

        private static void CancelAndEnd(BuffRuntime rt, EffectSourceRegistry effectSource, ITriggerActionRunner actionRunner, int frame)
        {
            if (rt == null) return;
            if (rt.SourceContextId == 0) return;

            try
            {
                actionRunner?.CancelByOwnerKey(rt.SourceContextId);
            }
            catch
            {
            }

            try
            {
                effectSource?.End(rt.SourceContextId, frame, EffectSourceEndReason.Replaced);
            }
            catch
            {
            }

            rt.SourceContextId = 0;
        }

        private static void PublishBuffRemove(IEventBus bus, BuffMO buff, int sourceActorId, int targetActorId, BuffRuntime runtime, EffectSourceEndReason reason)
        {
            if (bus == null) return;
            if (buff == null) return;

            PublishBuffStageEvent(bus, MobaBuffTriggering.Events.Remove, effectIds: buff.OnRemoveEffects, stage: "remove", sourceActorId, targetActorId, runtime, reason);
        }

        private static void PublishBuffStageEvent(IEventBus bus, string baseEventId, IReadOnlyList<int> effectIds, string stage, int sourceActorId, int targetActorId, BuffRuntime runtime, EffectSourceEndReason reason)
        {
            if (bus == null) return;
            if (string.IsNullOrEmpty(baseEventId)) return;

            // 先发布 base event（effectId=0），用于通用监听。
            {
                var args0 = PooledTriggerArgs.Rent();
                args0[EffectTriggering.Args.Source] = sourceActorId;
                args0[EffectTriggering.Args.Target] = targetActorId;
                args0[EffectSourceKeys.SourceActorId] = sourceActorId;
                args0[EffectSourceKeys.TargetActorId] = targetActorId;
                args0[MobaBuffTriggering.Args.BuffId] = runtime != null ? runtime.BuffId : 0;
                args0[MobaBuffTriggering.Args.EffectId] = 0;
                args0[MobaBuffTriggering.Args.Stage] = stage;
                args0[MobaBuffTriggering.Args.StackCount] = runtime != null ? runtime.StackCount : 0;
                args0[MobaBuffTriggering.Args.RemoveReason] = (int)reason;
                if (runtime != null && runtime.SourceContextId != 0)
                {
                    args0[EffectSourceKeys.SourceContextId] = runtime.SourceContextId;
                }
                bus.Publish(new TriggerEvent(baseEventId, payload: runtime, args: args0));
            }

            if (effectIds == null || effectIds.Count == 0) return;

            for (int i = 0; i < effectIds.Count; i++)
            {
                var effectId = effectIds[i];
                if (effectId <= 0) continue;

                var args1 = PooledTriggerArgs.Rent();
                args1[EffectTriggering.Args.Source] = sourceActorId;
                args1[EffectTriggering.Args.Target] = targetActorId;
                args1[EffectSourceKeys.SourceActorId] = sourceActorId;
                args1[EffectSourceKeys.TargetActorId] = targetActorId;
                args1[MobaBuffTriggering.Args.BuffId] = runtime != null ? runtime.BuffId : 0;
                args1[MobaBuffTriggering.Args.EffectId] = effectId;
                args1[MobaBuffTriggering.Args.Stage] = stage;
                args1[MobaBuffTriggering.Args.StackCount] = runtime != null ? runtime.StackCount : 0;
                args1[MobaBuffTriggering.Args.RemoveReason] = (int)reason;
                if (runtime != null && runtime.SourceContextId != 0)
                {
                    args1[EffectSourceKeys.SourceContextId] = runtime.SourceContextId;
                }
                bus.Publish(new TriggerEvent(MobaBuffTriggering.Events.WithEffect(baseEventId, effectId), payload: runtime, args: args1));
            }
        }

        private static void ResetInterval(BuffRuntime rt, BuffMO buff)
        {
            if (rt == null) return;
            if (buff == null) return;
            rt.IntervalRemainingSeconds = buff.IntervalMs > 0 ? buff.IntervalMs / 1000f : 0f;
        }

        private void ExecuteStageEffects(IReadOnlyList<int> effectIds, int sourceActorId, int targetActorId, IUnitFacade targetUnit)
        {
            // 中文注释：MobaBuffService 的立即执行路径，同样需要执行 Buff 的多阶段效果。
            if (_effectExec == null) return;
            if (effectIds == null || effectIds.Count == 0) return;

            for (int i = 0; i < effectIds.Count; i++)
            {
                var effectId = effectIds[i];
                if (effectId <= 0) continue;
                var ctx = BuildEffectContext(sourceActorId, targetActorId, targetUnit);
                _effectExec.Execute(effectId, ctx, EffectExecuteMode.InternalOnly);
            }
        }

        private static void PublishBuffPerEffect(IEventBus bus, string baseEventId, IReadOnlyList<int> effectIds, string stage, int sourceActorId, int targetActorId, BuffRuntime rt)
        {
            // 中文注释：用于 buff.apply/buff.interval 等场景，按 effectId 发布 buff.xxx.<effectId> 事件。
            // 注意：baseEventId（无 .<id>）一般由调用者单独发布一次（effectId=0）。
            if (bus == null) return;
            if (string.IsNullOrEmpty(baseEventId)) return;
            if (effectIds == null || effectIds.Count == 0) return;

            for (int i = 0; i < effectIds.Count; i++)
            {
                var effectId = effectIds[i];
                if (effectId <= 0) continue;

                var args = PooledTriggerArgs.Rent();
                args[EffectTriggering.Args.Source] = sourceActorId;
                args[EffectTriggering.Args.Target] = targetActorId;
                args[EffectSourceKeys.SourceActorId] = sourceActorId;
                args[EffectSourceKeys.TargetActorId] = targetActorId;
                args[MobaBuffTriggering.Args.BuffId] = rt != null ? rt.BuffId : 0;
                args[MobaBuffTriggering.Args.EffectId] = effectId;
                args[MobaBuffTriggering.Args.Stage] = stage;
                args[MobaBuffTriggering.Args.StackCount] = rt != null ? rt.StackCount : 0;
                if (rt != null && rt.SourceContextId != 0)
                {
                    args[EffectSourceKeys.SourceContextId] = rt.SourceContextId;
                }

                bus.Publish(new TriggerEvent(MobaBuffTriggering.Events.WithEffect(baseEventId, effectId), payload: rt, args: args));
            }
        }

        private static SkillPipelineContext BuildEffectContext(int sourceActorId, int targetActorId, IUnitFacade targetUnit)
        {
            // 中文注释：Buff 执行 effect 时复用 SkillPipelineContext 作为通用的 effect 执行上下文。
            var ctx = new SkillPipelineContext();
            var req = new SkillCastRequest(
                skillId: 0,
                skillSlot: 0,
                casterActorId: sourceActorId,
                targetActorId: targetActorId,
                aimPos: Vec3.Zero,
                aimDir: Vec3.Forward,
                worldServices: null,
                eventBus: null,
                casterUnit: null,
                targetUnit: targetUnit);
            ctx.Initialize(abilityInstance: null, in req);
            return ctx;
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

        public void Dispose()
        {
        }
    }
}
