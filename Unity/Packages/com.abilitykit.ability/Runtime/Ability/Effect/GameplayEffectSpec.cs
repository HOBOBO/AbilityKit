using System;
using System.Collections.Generic;
using AbilityKit.GameplayTags;
using GameplayTagRequirements = AbilityKit.GameplayTags.GameplayTagRequirements;
using GameplayTagContainer = AbilityKit.GameplayTags.GameplayTagContainer;

namespace AbilityKit.Ability.Share.Effect
{
    public sealed class GameplayEffectSpec
    {
        public GameplayEffectSpec(
            EffectDurationPolicy durationPolicy,
            float durationSeconds,
            float periodSeconds,
            GameplayTagRequirements applicationRequirements,
            GameplayTagContainer grantedTags,
            IReadOnlyList<IEffectComponent> components,
            bool executePeriodicOnApply = false,
            IGameplayEffectCue cue = null)
        {
            DurationPolicy = durationPolicy;
            DurationSeconds = durationSeconds;
            PeriodSeconds = periodSeconds;
            ApplicationRequirements = applicationRequirements;
            GrantedTags = grantedTags;
            Components = components ?? Array.Empty<IEffectComponent>();
            ExecutePeriodicOnApply = executePeriodicOnApply;
            Cue = cue;
        }

        public EffectDurationPolicy DurationPolicy { get; }
        public float DurationSeconds { get; }
        public float PeriodSeconds { get; }
        public bool ExecutePeriodicOnApply { get; }

        public GameplayTagRequirements ApplicationRequirements { get; }
        public GameplayTagContainer GrantedTags { get; }

        public IGameplayEffectCue Cue { get; }

        public IReadOnlyList<IEffectComponent> Components { get; }
    }
}
