using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Common.AttributeSystem;
using AbilityKit.Ability.Share.Common.TagSystem;
using AbilityKit.Ability.Triggering;

namespace AbilityKit.Ability.Share.Effect
{
    public readonly struct EffectExecutionContext
    {
        public readonly IServiceProvider Services;
        public readonly IFrameTime Time;

        public readonly object Source;
        public readonly object Target;

        public readonly GameplayTagContainer TargetTags;
        public readonly AttributeContext TargetAttributes;
        public readonly IEventBus EventBus;

        public EffectExecutionContext(
            IServiceProvider services,
            IFrameTime time,
            object source,
            object target,
            GameplayTagContainer targetTags,
            AttributeContext targetAttributes,
            IEventBus eventBus)
        {
            Services = services;
            Time = time;
            Source = source;
            Target = target;
            TargetTags = targetTags;
            TargetAttributes = targetAttributes;
            EventBus = eventBus;
        }
    }
}
