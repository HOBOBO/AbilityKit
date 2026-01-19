using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.Impl.Moba;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Buffs
{
    [WorldSystem(order: MobaSystemOrder.BuffsTick, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaBuffTickSystem : WorldSystemBase
    {
        private MobaConfigDatabase _configs;
        private IWorldClock _clock;
        private IEventBus _eventBus;
        private ITriggerActionRunner _actionRunner;
        private EffectSourceRegistry _effectSource;
        private IFrameTime _frameTime;
        private MobaEffectExecutionService _effectExec;
        private Entitas.IGroup<global::ActorEntity> _group;

        public MobaBuffTickSystem(global::Contexts contexts, IWorldServices services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryGet(out _configs);
            Services.TryGet(out _clock);
            Services.TryGet(out _eventBus);
            Services.TryGet(out _actionRunner);
            Services.TryGet(out _effectSource);
            Services.TryGet(out _frameTime);
            Services.TryGet(out _effectExec);
            _group = Contexts.actor.GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.Buffs));
        }

        protected override void OnExecute()
        {
            if (_clock == null) return;
            var dt = _clock.DeltaTime;
            if (dt <= 0f) return;

            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null || !e.hasBuffs) continue;

                var list = e.buffs.Active;
                if (list == null || list.Count == 0) continue;

                for (int j = list.Count - 1; j >= 0; j--)
                {
                    var b = list[j];
                    if (b == null)
                    {
                        list.RemoveAt(j);
                        continue;
                    }

                    // interval tick
                    if (_configs != null && _configs.TryGetBuff(b.BuffId, out var buffCfg) && buffCfg != null)
                    {
                        TryIntervalTick(buffCfg, b, e.actorId.Value, dt);
                    }

                    b.Remaining -= dt;
                    if (b.Remaining > 0f) continue;

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

                    try
                    {
                        if (b.SourceContextId != 0)
                        {
                            _effectSource?.End(b.SourceContextId, GetFrame(), EffectSourceEndReason.Expired);
                        }
                    }
                    catch
                    {
                    }

                    if (_configs != null)
                    {
                        if (_configs.TryGetBuff(b.BuffId, out var buff) && buff != null)
                        {
                            PublishBuffRemove(_eventBus, buff, b.SourceId, e.actorId.Value, b, EffectSourceEndReason.Expired);
                            ExecuteStageEffects(buff.OnRemoveEffects, stage: "remove", sourceActorId: b.SourceId, targetActorId: e.actorId.Value);
                        }
                    }

                    list.RemoveAt(j);
                }
            }
        }

        private void TryIntervalTick(global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.BuffMO buff, global::AbilityKit.Ability.Impl.Moba.Conponents.BuffRuntime rt, int targetActorId, float dt)
        {
            if (buff == null) return;
            if (rt == null) return;
            if (buff.IntervalMs <= 0) return;
            if (buff.OnIntervalEffects == null || buff.OnIntervalEffects.Count == 0) return;

            rt.IntervalRemainingSeconds -= dt;
            if (rt.IntervalRemainingSeconds > 0f) return;

            // reset first to avoid re-entrancy issues
            rt.IntervalRemainingSeconds = buff.IntervalMs / 1000f;

            ExecuteStageEffects(buff.OnIntervalEffects, stage: "interval", sourceActorId: rt.SourceId, targetActorId: targetActorId);
            PublishBuffInterval(_eventBus, buff, rt.SourceId, targetActorId, rt);
        }

        private void PublishBuffInterval(IEventBus bus, global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.BuffMO buff, int sourceActorId, int targetActorId, global::AbilityKit.Ability.Impl.Moba.Conponents.BuffRuntime runtime)
        {
            if (bus == null) return;
            if (buff == null) return;

            // base interval event
            {
                var args0 = PooledTriggerArgs.Rent();
                args0[EffectTriggering.Args.Source] = sourceActorId;
                args0[EffectTriggering.Args.Target] = targetActorId;
                args0[EffectSourceKeys.SourceActorId] = sourceActorId;
                args0[EffectSourceKeys.TargetActorId] = targetActorId;
                args0[MobaBuffTriggering.Args.BuffId] = runtime != null ? runtime.BuffId : 0;
                args0[MobaBuffTriggering.Args.EffectId] = 0;
                args0[MobaBuffTriggering.Args.Stage] = "interval";
                args0[MobaBuffTriggering.Args.StackCount] = runtime != null ? runtime.StackCount : 0;
                if (runtime != null && runtime.SourceContextId != 0)
                {
                    args0[EffectSourceKeys.SourceContextId] = runtime.SourceContextId;
                }
                bus.Publish(new TriggerEvent(MobaBuffTriggering.Events.Interval, payload: runtime, args: args0));
            }

            var effects = buff.OnIntervalEffects;
            if (effects == null || effects.Count == 0) return;
            for (int i = 0; i < effects.Count; i++)
            {
                var effectId = effects[i];
                if (effectId <= 0) continue;
                var args1 = PooledTriggerArgs.Rent();
                args1[EffectTriggering.Args.Source] = sourceActorId;
                args1[EffectTriggering.Args.Target] = targetActorId;
                args1[EffectSourceKeys.SourceActorId] = sourceActorId;
                args1[EffectSourceKeys.TargetActorId] = targetActorId;
                args1[MobaBuffTriggering.Args.BuffId] = runtime != null ? runtime.BuffId : 0;
                args1[MobaBuffTriggering.Args.EffectId] = effectId;
                args1[MobaBuffTriggering.Args.Stage] = "interval";
                args1[MobaBuffTriggering.Args.StackCount] = runtime != null ? runtime.StackCount : 0;
                if (runtime != null && runtime.SourceContextId != 0)
                {
                    args1[EffectSourceKeys.SourceContextId] = runtime.SourceContextId;
                }
                bus.Publish(new TriggerEvent(MobaBuffTriggering.Events.WithEffect(MobaBuffTriggering.Events.Interval, effectId), payload: runtime, args: args1));
            }
        }

        private void ExecuteStageEffects(System.Collections.Generic.IReadOnlyList<int> effectIds, string stage, int sourceActorId, int targetActorId)
        {
            // 中文注释：TickSystem 执行 interval/remove 阶段 effects。
            if (_effectExec == null) return;
            if (effectIds == null || effectIds.Count == 0) return;
            for (int i = 0; i < effectIds.Count; i++)
            {
                var effectId = effectIds[i];
                if (effectId <= 0) continue;
                var ctx = BuildEffectContext(sourceActorId, targetActorId);
                _effectExec.Execute(effectId, ctx, EffectExecuteMode.InternalOnly);
            }
        }

        private static SkillPipelineContext BuildEffectContext(int sourceActorId, int targetActorId)
        {
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
                targetUnit: null);
            ctx.Initialize(abilityInstance: null, in req);
            return ctx;
        }

        private static void PublishBuffRemove(IEventBus bus, global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.BuffMO buff, int sourceActorId, int targetActorId, global::AbilityKit.Ability.Impl.Moba.Conponents.BuffRuntime runtime, EffectSourceEndReason reason)
        {
            if (bus == null) return;
            if (buff == null) return;

            // base remove event
            {
                var args0 = PooledTriggerArgs.Rent();
                args0[EffectTriggering.Args.Source] = sourceActorId;
                args0[EffectTriggering.Args.Target] = targetActorId;
                args0[EffectSourceKeys.SourceActorId] = sourceActorId;
                args0[EffectSourceKeys.TargetActorId] = targetActorId;
                args0[MobaBuffTriggering.Args.BuffId] = runtime != null ? runtime.BuffId : 0;
                args0[MobaBuffTriggering.Args.EffectId] = 0;
                args0[MobaBuffTriggering.Args.Stage] = "remove";
                args0[MobaBuffTriggering.Args.StackCount] = runtime != null ? runtime.StackCount : 0;
                args0[MobaBuffTriggering.Args.RemoveReason] = (int)reason;
                if (runtime != null && runtime.SourceContextId != 0)
                {
                    args0[EffectSourceKeys.SourceContextId] = runtime.SourceContextId;
                }
                bus.Publish(new TriggerEvent(MobaBuffTriggering.Events.Remove, payload: runtime, args: args0));
            }

            var effects = buff.OnRemoveEffects;
            if (effects == null || effects.Count == 0) return;
            for (int i = 0; i < effects.Count; i++)
            {
                var effectId = effects[i];
                if (effectId <= 0) continue;
                var args1 = PooledTriggerArgs.Rent();
                args1[EffectTriggering.Args.Source] = sourceActorId;
                args1[EffectTriggering.Args.Target] = targetActorId;
                args1[EffectSourceKeys.SourceActorId] = sourceActorId;
                args1[EffectSourceKeys.TargetActorId] = targetActorId;
                args1[MobaBuffTriggering.Args.BuffId] = runtime != null ? runtime.BuffId : 0;
                args1[MobaBuffTriggering.Args.EffectId] = effectId;
                args1[MobaBuffTriggering.Args.Stage] = "remove";
                args1[MobaBuffTriggering.Args.StackCount] = runtime != null ? runtime.StackCount : 0;
                args1[MobaBuffTriggering.Args.RemoveReason] = (int)reason;
                if (runtime != null && runtime.SourceContextId != 0)
                {
                    args1[EffectSourceKeys.SourceContextId] = runtime.SourceContextId;
                }
                bus.Publish(new TriggerEvent(MobaBuffTriggering.Events.WithEffect(MobaBuffTriggering.Events.Remove, effectId), payload: runtime, args: args1));
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
    }
}
