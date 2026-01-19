using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Buffs
{
    [WorldSystem(order: MobaSystemOrder.BuffsApply, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaBuffApplySystem : WorldSystemBase
    {
        private MobaConfigDatabase _configs;
        private IUnitResolver _units;
        private IEventBus _eventBus;
        private ITriggerActionRunner _actionRunner;
        private EffectSourceRegistry _effectSource;
        private IFrameTime _frameTime;
        private MobaEffectExecutionService _effectExec;

        private Entitas.IGroup<global::ActorEntity> _group;

        public MobaBuffApplySystem(global::Contexts contexts, IWorldServices services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryGet(out _configs);
            Services.TryGet(out _units);
            Services.TryGet(out _eventBus);
            Services.TryGet(out _actionRunner);
            Services.TryGet(out _effectSource);
            Services.TryGet(out _frameTime);
            Services.TryGet(out _effectExec);
            _group = Contexts.actor.GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.ApplyBuffRequest));
        }

        protected override void OnExecute()
        {
            if (_configs == null || _units == null) return;

            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null || !e.hasActorId || !e.hasApplyBuffRequest) continue;

                var req = e.applyBuffRequest;
                e.RemoveApplyBuffRequest();

                if (req.BuffId <= 0) continue;

                if (!_units.TryResolve(new EcsEntityId(e.actorId.Value), out var unit) || unit == null) continue;

                if (!_configs.TryGetBuff(req.BuffId, out var buff) || buff == null) continue;

                if (!e.hasBuffs)
                {
                    e.AddBuffs(new List<BuffRuntime>());
                }

                var list = e.buffs.Active;
                if (list == null)
                {
                    list = new List<BuffRuntime>();
                    e.ReplaceBuffs(list);
                }

                var duration = req.DurationOverrideMs > 0 ? req.DurationOverrideMs : buff.DurationMs;

                var durationSeconds = duration > 0 ? duration / 1000f : 0f;

                var targetActorId = e.actorId.Value;
                var existingIndex = FindExistingBuffIndex(list, buff.Id);
                if (existingIndex >= 0)
                {
                    var rt = list[existingIndex];
                    var applied = ApplyToExisting(rt, buff, req, durationSeconds, _effectSource, _actionRunner, GetFrame(), req.SourceId, targetActorId);
                    EnsureBuffContext(rt, buff.Id, req.SourceId, targetActorId, _effectSource, GetFrame());
                    PublishBuffEvent(_eventBus, MobaBuffTriggering.Events.ApplyOrRefresh, buff, req.SourceId, targetActorId, durationSeconds, rt);
                    if (applied)
                    {
                        ResetInterval(rt, buff);
                        ExecuteStageEffects(buff.OnAddEffects, stage: "add", sourceActorId: req.SourceId, targetActorId: targetActorId, unit, GetFrame(), rt);
                    }
                }
                else
                {
                    var rt = CreateNewRuntime(buff, req, durationSeconds);
                    EnsureBuffContext(rt, buff.Id, req.SourceId, targetActorId, _effectSource, GetFrame());
                    list.Add(rt);
                    PublishBuffEvent(_eventBus, MobaBuffTriggering.Events.ApplyOrRefresh, buff, req.SourceId, targetActorId, durationSeconds, rt);
                    ResetInterval(rt, buff);
                    ExecuteStageEffects(buff.OnAddEffects, stage: "add", sourceActorId: req.SourceId, targetActorId: targetActorId, unit, GetFrame(), rt);
                }
            }
        }

        private static void ResetInterval(BuffRuntime rt, BuffMO buff)
        {
            if (rt == null) return;
            if (buff == null) return;
            rt.IntervalRemainingSeconds = buff.IntervalMs > 0 ? buff.IntervalMs / 1000f : 0f;
        }

        private void ExecuteStageEffects(IReadOnlyList<int> effectIds, string stage, int sourceActorId, int targetActorId, IUnitFacade targetUnit, int frame, BuffRuntime runtime)
        {
            // 中文注释：Buff 的多阶段效果执行入口。
            // stage: add/remove/interval 等，主要用于事件 args 标识来源阶段。
            if (_effectExec == null) return;
            if (effectIds == null || effectIds.Count == 0) return;

            for (int i = 0; i < effectIds.Count; i++)
            {
                var effectId = effectIds[i];
                if (effectId <= 0) continue;
                var ctx = BuildEffectContext(sourceActorId, targetActorId, targetUnit);
                _effectExec.Execute(effectId, ctx, EffectExecuteMode.InternalOnly);
                PublishBuffEffectEvent(_eventBus, MobaBuffTriggering.Events.ApplyOrRefresh, effectId, stage, sourceActorId, targetActorId, runtime);
            }
        }

        private static SkillPipelineContext BuildEffectContext(int sourceActorId, int targetActorId, IUnitFacade targetUnit)
        {
            // 中文注释：Buff 执行 effect 时复用 SkillPipelineContext 作为通用的 effect 执行上下文。
            // 这里 skillId/slot 置 0，Aim 置零向量。
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

        private static bool ApplyToExisting(BuffRuntime existing, BuffMO buff, ApplyBuffRequestComponent req, float durationSeconds, EffectSourceRegistry effectSource, ITriggerActionRunner actionRunner, int frame, int sourceActorId, int targetActorId)
        {
            if (existing == null) return false;

            switch (buff.StackingPolicy)
            {
                case BuffStackingPolicy.IgnoreIfExists:
                    return false;
                case BuffStackingPolicy.Replace:
                    CancelAndEnd(existing, effectSource, actionRunner, frame);
                    existing.SourceId = req.SourceId;
                    existing.StackCount = 0;
                    existing.Remaining = durationSeconds;
                    AddStack(existing, buff.MaxStacks);
                    return true;
                case BuffStackingPolicy.AddStack:
                    AddStack(existing, buff.MaxStacks);
                    RefreshRemaining(existing, buff.RefreshPolicy, durationSeconds);
                    existing.SourceId = req.SourceId;
                    return true;
                case BuffStackingPolicy.RefreshDuration:
                    RefreshRemaining(existing, buff.RefreshPolicy, durationSeconds);
                    existing.SourceId = req.SourceId;
                    return true;
                case BuffStackingPolicy.None:
                default:
                    return false;
            }
        }

        private static BuffRuntime CreateNewRuntime(BuffMO buff, ApplyBuffRequestComponent req, float durationSeconds)
        {
            var rt = new BuffRuntime
            {
                BuffId = buff.Id,
                Remaining = durationSeconds,
                IntervalRemainingSeconds = 0,
                SourceId = req.SourceId,
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

        private static void PublishBuffEffectEvent(IEventBus bus, string baseEventId, int effectId, string stage, int sourceActorId, int targetActorId, BuffRuntime runtime)
        {
            // 中文注释：针对单个 effectId 发布 buff 事件（含 buff.apply.<effectId>）。
            if (bus == null) return;
            if (string.IsNullOrEmpty(baseEventId)) return;
            if (effectId <= 0) return;

            void Fill(PooledTriggerArgs args)
            {
                args[EffectTriggering.Args.Source] = sourceActorId;
                args[EffectTriggering.Args.Target] = targetActorId;
                args[EffectSourceKeys.SourceActorId] = sourceActorId;
                args[EffectSourceKeys.TargetActorId] = targetActorId;
                args[MobaBuffTriggering.Args.BuffId] = runtime != null ? runtime.BuffId : 0;
                args[MobaBuffTriggering.Args.EffectId] = effectId;
                args[MobaBuffTriggering.Args.Stage] = stage;
                args[MobaBuffTriggering.Args.StackCount] = runtime != null ? runtime.StackCount : 0;
                if (runtime != null && runtime.SourceContextId != 0)
                {
                    args[EffectSourceKeys.SourceContextId] = runtime.SourceContextId;
                }
            }

            var args1 = PooledTriggerArgs.Rent();
            Fill(args1);
            bus.Publish(new TriggerEvent(baseEventId, payload: runtime, args: args1));

            var args2 = PooledTriggerArgs.Rent();
            Fill(args2);
            bus.Publish(new TriggerEvent(MobaBuffTriggering.Events.WithEffect(baseEventId, effectId), payload: runtime, args: args2));
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
                effectSource?.End(rt.SourceContextId, frame, EffectSourceEndReason.Cancelled);
            }
            catch
            {
            }

            rt.SourceContextId = 0;
        }

        
    }
}
