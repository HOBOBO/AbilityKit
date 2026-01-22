using System;
using AbilityKit.Ability;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Share.Common.Log;
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

        public void Execute(int effectId, IAbilityPipelineContext context, EffectExecuteMode mode = EffectExecuteMode.InternalOnly)
        {
            if (effectId <= 0) return;
            if (context == null) return;

            var wrappedContext = EffectContextWrapper.Wrap(context);
            if (wrappedContext == null) return;

            var needInternal = mode == EffectExecuteMode.InternalOnly || mode == EffectExecuteMode.InternalThenPublishEvent;
            var needPublish = mode == EffectExecuteMode.PublishEventOnly || mode == EffectExecuteMode.InternalThenPublishEvent;

            if (needInternal && _triggers == null) return;
            if (needPublish && _eventBus == null) return;

            static void FillArgs(PooledTriggerArgs args, int effectId2, IAbilityPipelineContext ctx)
            {
                args[MobaSkillTriggering.Args.SkillId] = ctx.GetSkillId();
                args[MobaSkillTriggering.Args.SkillSlot] = ctx.GetSkillSlot();
                args[MobaSkillTriggering.Args.CasterActorId] = ctx.GetCasterActorId();
                args[MobaSkillTriggering.Args.TargetActorId] = ctx.GetTargetActorId();
                args[MobaSkillTriggering.Args.AimPos] = ctx.GetAimPos();
                args[MobaSkillTriggering.Args.AimDir] = ctx.GetAimDir();
                args["effect.id"] = effectId2;
            }

            if (needInternal)
            {
                var args = PooledTriggerArgs.Rent();
                FillArgs(args, effectId, wrappedContext);
                RunByTriggerId(effectId, args, wrappedContext);
                args.Dispose();
            }

            if (needPublish)
            {
                var args1 = PooledTriggerArgs.Rent();
                FillArgs(args1, effectId, wrappedContext);
                _eventBus.Publish(new TriggerEvent(MobaTriggerEventIds.EffectExecute, payload: wrappedContext, args: args1));

                var args2 = PooledTriggerArgs.Rent();
                FillArgs(args2, effectId, wrappedContext);
                _eventBus.Publish(new TriggerEvent(MobaTriggerEventIds.EffectExecuteById(effectId), payload: wrappedContext, args: args2));
            }
        }

        // Active invocation: execute triggers by triggerId directly (no event subscription involved).
        // Note: This is intentionally different from publishing an event; it is used by systems like projectiles that own their triggers.
        public void ExecuteTriggerId(int triggerId, object source = null, object target = null, object payload = null, PooledTriggerArgs args = null)
        {
            if (triggerId <= 0) return;
            if (_triggers == null) return;
            if (_index == null) return;
            if (!_index.TryGetByTriggerId(triggerId, out var list) || list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                var def = e.Def;
                if (def == null) continue;

                if (!string.IsNullOrEmpty(def.EventId))
                {
                    Log.Warning($"[MobaEffectExecutionService] ExecuteTriggerId found trigger with EventId set. triggerId={triggerId}, eventId={def.EventId} (should be empty for active triggers)");
                }

                try
                {
                    _triggers.RunOnce(def, source: source, target: target, payload: payload, args: args, initialLocalVars: e.InitialLocalVars);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaEffectExecutionService] ExecuteTriggerId exception: triggerId={triggerId}");
                }
            }
        }

        private void RunByTriggerId(int triggerId, PooledTriggerArgs args, IAbilityPipelineContext context)
        {
            if (_triggers == null) return;
            if (_index == null) return;
            if (!_index.TryGetByTriggerId(triggerId, out var list) || list == null) return;

            object caster = null;
            if (context is IEffectContext ec && ec.TryGetSkill(out var skill))
            {
                caster = skill.CasterUnit;
            }

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
