using System;
using System.Collections.Generic;
using AbilityKit.Continuous;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 可携带标签与修饰器的 MOBA 持续运行时基础配置。
    /// </summary>
    public abstract class MobaContinuousConfigBase : IContinuousConfig,
        IDurationConfig,
        IStackConfig,
        ITagConfig,
        IMobaContinuousTagConfig,
        IMobaContinuousModifierConfig,
        IMobaContinuousPeriodicConfig,
        IMobaContinuousProjectionConfig
    {
        protected MobaContinuousConfigBase(
            float durationSeconds,
            ContinuousTagRequirements tagRequirements,
            IReadOnlyList<IMobaContinuousModifierSpec> modifiers)
        {
            DurationSeconds = durationSeconds > 0f ? durationSeconds : (float?)null;
            TagRequirements = tagRequirements;
            Modifiers = modifiers ?? Array.Empty<IMobaContinuousModifierSpec>();
            Stack = 1;
            MaxStack = 1;
        }

        public abstract string Id { get; }
        public abstract long OwnerId { get; }
        public virtual bool CanBeInterrupted => true;
        public abstract int OwnerActorId { get; }
        public abstract int ModifierSourceId { get; }
        public abstract GameplayTagSource TagSource { get; }

        public ContinuousTagRequirements TagRequirements { get; set; }
        public IReadOnlyList<IMobaContinuousModifierSpec> Modifiers { get; }
        public virtual float IntervalSeconds => 0f;
        public virtual IReadOnlyList<int> IntervalEffectIds => Array.Empty<int>();
        public float? DurationSeconds { get; set; }
        public int Stack { get; set; }
        public int MaxStack { get; set; }
        public ITagContainer Tags => GameplayTagContainerAdapter.From(TagRequirements?.ApplicationTags);
        public ITagContainer PauseByTags => null;
        public ITagContainer BlockByTags => GameplayTagContainerAdapter.Empty;
    }
}
