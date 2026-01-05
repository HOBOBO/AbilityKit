using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.TagSystem;

namespace AbilityKit.Ability.Share.Effect
{
    public sealed class GameplayEffectSpec
    {
        public GameplayEffectSpec(
            EffectDurationPolicy durationPolicy,
            int durationFrames,
            int periodFrames,
            GameplayTagRequirements applicationRequirements,
            GameplayTagContainer grantedTags,
            IReadOnlyList<IEffectComponent> components,
            bool executePeriodicOnApply = false)
        {
            DurationPolicy = durationPolicy;
            DurationFrames = durationFrames;
            PeriodFrames = periodFrames;
            ApplicationRequirements = applicationRequirements;
            GrantedTags = grantedTags;
            Components = components ?? Array.Empty<IEffectComponent>();
            ExecutePeriodicOnApply = executePeriodicOnApply;
        }

        public EffectDurationPolicy DurationPolicy { get; }
        public int DurationFrames { get; }
        public int PeriodFrames { get; }
        public bool ExecutePeriodicOnApply { get; }

        public GameplayTagRequirements ApplicationRequirements { get; }
        public GameplayTagContainer GrantedTags { get; }

        public IReadOnlyList<IEffectComponent> Components { get; }
    }
}
