using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Common.AttributeSystem;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Triggering;
using GameplayTagContainer = AbilityKit.GameplayTags.GameplayTagContainer;

namespace AbilityKit.Ability.Share.Effect
{
    public readonly struct EffectExecutionContext
    {
        public readonly IServiceProvider Services;
        public readonly IFrameTime Time;

        public readonly long SourceContextId;

        public readonly object Source;
        public readonly object Target;

        public readonly IUnitFacade TargetUnit;

        public GameplayTagContainer TargetTags => TargetUnit.Tags;
        public AttributeContext TargetAttributes => TargetUnit.Attributes;
        public readonly IEventBus EventBus;

        public EffectExecutionContext(
            IServiceProvider services,
            IFrameTime time,
            object source,
            object target,
            IUnitFacade targetUnit,
            IEventBus eventBus,
            long sourceContextId = 0)
        {
            Services = services;
            Time = time;
            SourceContextId = sourceContextId;
            Source = source;
            Target = target;
            TargetUnit = targetUnit ?? throw new ArgumentNullException(nameof(targetUnit));
            EventBus = eventBus;
        }
    }
}
