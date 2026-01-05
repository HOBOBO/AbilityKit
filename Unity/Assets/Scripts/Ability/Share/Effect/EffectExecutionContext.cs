using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Common.AttributeSystem;
using AbilityKit.Ability.Share.Common.TagSystem;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Triggering;

namespace AbilityKit.Ability.Share.Effect
{
    public readonly struct EffectExecutionContext
    {
        public readonly IServiceProvider Services;
        public readonly IFrameTime Time;

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
            IEventBus eventBus)
        {
            Services = services;
            Time = time;
            Source = source;
            Target = target;
            TargetUnit = targetUnit ?? throw new ArgumentNullException(nameof(targetUnit));
            EventBus = eventBus;
        }
    }
}
