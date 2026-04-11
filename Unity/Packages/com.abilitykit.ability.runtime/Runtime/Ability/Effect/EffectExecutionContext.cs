using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Common.AttributeSystem;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Triggering;
using AbilityKit.ECS;
using GameplayTagContainer = AbilityKit.GameplayTags.GameplayTagContainer;

namespace AbilityKit.Effect
{
    public readonly struct EffectExecutionContext
    {
        public readonly IServiceProvider Services;
        public readonly IFrameTime Time;

        public readonly object Source;
        public readonly object Target;
        public readonly long SourceContextId;

        public readonly IUnitFacade TargetUnit;

        public GameplayTagContainer TargetTags => TargetUnit.Tags;
        public AttributeContext TargetAttributes => TargetUnit.Attributes;
        public readonly IEventBus EventBus;

        public EffectExecutionContext(
            IServiceProvider services,
            IFrameTime time,
            object source,
            object target,
            long sourceContextId,
            IUnitFacade targetUnit,
            IEventBus eventBus)
        {
            Services = services;
            Time = time;
            Source = source;
            Target = target;
            SourceContextId = sourceContextId;
            TargetUnit = targetUnit ?? throw new ArgumentNullException(nameof(targetUnit));
            EventBus = eventBus;
        }

        public EffectExecutionContext WithSourceContextId(long sourceContextId)
        {
            return new EffectExecutionContext(
                Services, Time, Source, Target, sourceContextId, TargetUnit, EventBus);
        }
    }
}
