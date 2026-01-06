using System;
using System.Collections.Generic;
using AbilityKit.Ability;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Common.TagSystem;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Share.Effect.Components;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class SkillCastApplyEffectPhase : AbilityInstantPhaseBase
    {
        private readonly IWorldServices _services;
        private readonly IFrameTime _time;
        private readonly IEventBus _eventBus;
        private readonly IUnitResolver _units;

        public SkillCastApplyEffectPhase(
            AbilityPipelinePhaseId phaseId,
            IWorldServices services,
            IFrameTime time,
            IEventBus eventBus,
            IUnitResolver units)
            : base(phaseId)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _eventBus = eventBus;
            _units = units ?? throw new ArgumentNullException(nameof(units));
        }

        protected override void OnInstantExecute(IAbilityPipelineContext context)
        {
            if (context == null) return;

            var skillId = context.GetData<int>(MobaSkillPipelineSharedKeys.SkillId);
            var slot = context.GetData<int>(MobaSkillPipelineSharedKeys.SkillSlot);
            var casterActorId = context.GetData<int>(MobaSkillPipelineSharedKeys.CasterActorId);
            var targetActorId = context.GetData<int>(MobaSkillPipelineSharedKeys.TargetActorId);
            var aimPos = context.GetData<Vec3>(MobaSkillPipelineSharedKeys.AimPos);
            var aimDir = context.GetData<Vec3>(MobaSkillPipelineSharedKeys.AimDir);

            if (skillId <= 0 || casterActorId <= 0) return;
            if (targetActorId <= 0) targetActorId = casterActorId;

            if (!_units.TryResolve(new EcsEntityId(casterActorId), out var caster) || caster == null) return;
            if (!_units.TryResolve(new EcsEntityId(targetActorId), out var target) || target == null) return;

            var args = new Dictionary<string, object>(6, StringComparer.Ordinal)
            {
                [MobaSkillTriggerArgs.SkillId] = skillId,
                [MobaSkillTriggerArgs.SkillSlot] = slot,
                [MobaSkillTriggerArgs.CasterActorId] = casterActorId,
                [MobaSkillTriggerArgs.TargetActorId] = targetActorId,
                [MobaSkillTriggerArgs.AimPos] = aimPos,
                [MobaSkillTriggerArgs.AimDir] = aimDir,
            };

            var spec = new GameplayEffectSpec(
                durationPolicy: EffectDurationPolicy.Duration,
                durationSeconds: 0.8f,
                periodSeconds: 0.2f,
                applicationRequirements: new GameplayTagRequirements(null, null),
                grantedTags: null,
                components: new IEffectComponent[]
                {
                    new TriggerEventEffectComponent(applyEventId: "skill.cast", args: args),
                },
                executePeriodicOnApply: true,
                cue: null
            );

            var sp = new WorldServiceProviderAdapter(_services);
            var exec = new EffectExecutionContext(
                services: sp,
                time: _time,
                source: caster,
                target: target,
                targetUnit: target,
                eventBus: _eventBus
            );

            target.Effects.Apply(spec, in exec);
        }
    }
}
