using System;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaEffectExecutionService : IService
    {
        private readonly IEventBus _eventBus;
        private readonly TriggerRunner _triggers;
        private readonly MobaTriggerIndexService _index;

        public MobaEffectExecutionService(IEventBus eventBus, TriggerRunner triggers, MobaTriggerIndexService index)
        {
            _eventBus = eventBus;
            _triggers = triggers;
            _index = index;
        }

        public void Execute(int effectId, SkillPipelineContext context, EffectExecuteMode mode = EffectExecuteMode.InternalOnly)
        {
            if (effectId <= 0) return;
            if (context == null) return;

            var needInternal = mode == EffectExecuteMode.InternalOnly || mode == EffectExecuteMode.InternalThenPublishEvent;
            var needPublish = mode == EffectExecuteMode.PublishEventOnly || mode == EffectExecuteMode.InternalThenPublishEvent;

            if (needInternal && _triggers == null) return;
            if (needPublish && _eventBus == null) return;

            static void FillArgs(PooledTriggerArgs args, int effectId2, SkillPipelineContext ctx)
            {
                args[MobaSkillTriggering.Args.SkillId] = ctx.SkillId;
                args[MobaSkillTriggering.Args.SkillSlot] = ctx.SkillSlot;
                args[MobaSkillTriggering.Args.CasterActorId] = ctx.CasterActorId;
                args[MobaSkillTriggering.Args.TargetActorId] = ctx.TargetActorId;
                args[MobaSkillTriggering.Args.AimPos] = ctx.AimPos;
                args[MobaSkillTriggering.Args.AimDir] = ctx.AimDir;
                args["effect.id"] = effectId2;
            }

            if (needInternal)
            {
                var args = PooledTriggerArgs.Rent();
                FillArgs(args, effectId, context);
                RunByTriggerId(effectId, args, context);
                args.Dispose();
            }

            if (needPublish)
            {
                var args1 = PooledTriggerArgs.Rent();
                FillArgs(args1, effectId, context);
                _eventBus.Publish(new TriggerEvent(MobaTriggerEventIds.EffectExecute, payload: context, args: args1));

                var args2 = PooledTriggerArgs.Rent();
                FillArgs(args2, effectId, context);
                _eventBus.Publish(new TriggerEvent(MobaTriggerEventIds.EffectExecuteById(effectId), payload: context, args: args2));
            }
        }

        private void RunByTriggerId(int triggerId, PooledTriggerArgs args, SkillPipelineContext context)
        {
            if (_triggers == null) return;
            if (_index == null) return;
            if (!_index.TryGetByTriggerId(triggerId, out var list) || list == null) return;

            var caster = context != null ? context.CasterUnit : null;

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                var def = e.Def;
                if (def == null) continue;

                _triggers.RunOnce(def, source: caster, target: caster, payload: context, args: args, initialLocalVars: e.InitialLocalVars);
            }
        }

        public void Dispose()
        {
        }
    }
}
