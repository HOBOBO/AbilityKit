using System;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaEffectExecutionService : IService
    {
        private readonly IEventBus _eventBus;

        public MobaEffectExecutionService(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public void Execute(int effectId, SkillPipelineContext context)
        {
            if (_eventBus == null) return;
            if (effectId <= 0) return;
            if (context == null) return;

            var args = PooledTriggerArgs.Rent();
            args[MobaSkillTriggering.Args.SkillId] = context.SkillId;
            args[MobaSkillTriggering.Args.SkillSlot] = context.SkillSlot;
            args[MobaSkillTriggering.Args.CasterActorId] = context.CasterActorId;
            args[MobaSkillTriggering.Args.TargetActorId] = context.TargetActorId;
            args[MobaSkillTriggering.Args.AimPos] = context.AimPos;
            args[MobaSkillTriggering.Args.AimDir] = context.AimDir;
            args["effect.id"] = effectId;

            _eventBus.Publish(new TriggerEvent("effect.execute", payload: context, args: args));
            _eventBus.Publish(new TriggerEvent($"effect.execute.{effectId}", payload: context, args: args));
        }

        public void Dispose()
        {
        }
    }
}
