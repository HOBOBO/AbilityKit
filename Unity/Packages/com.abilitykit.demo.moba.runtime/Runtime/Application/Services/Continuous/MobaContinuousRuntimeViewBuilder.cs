using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Continuous;
using AbilityKit.GameplayTags;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaContinuousRuntimeViewBuilder
    {
        MobaContinuousRuntimeView Build(IContinuous continuous);
    }

    [WorldService(typeof(IMobaContinuousRuntimeViewBuilder))]
    internal sealed class MobaContinuousRuntimeViewBuilder : IMobaContinuousRuntimeViewBuilder
    {
        private readonly IMobaContinuousTagRuleService _tagRules;
        private readonly IMobaContinuousModifierQueryService _modifiers;

        public MobaContinuousRuntimeViewBuilder(IMobaContinuousTagRuleService tagRules, IMobaContinuousModifierQueryService modifiers)
        {
            _tagRules = tagRules;
            _modifiers = modifiers;
        }

        public MobaContinuousRuntimeView Build(IContinuous continuous)
        {
            var config = continuous?.Config;
            if (config == null) return null;

            var debug = ResolveDebugInfo(continuous);
            var ownerActorId = ResolveOwnerActorId(config, in debug);
            var duration = config is IDurationConfig durationConfig && durationConfig.DurationSeconds.HasValue ? durationConfig.DurationSeconds.Value : 0f;
            var remaining = ResolveRemainingSeconds(continuous, duration);
            var stack = config is IStackConfig stackConfig ? stackConfig.Stack : 1;
            var maxStack = config is IStackConfig stackConfigForMax ? stackConfigForMax.MaxStack : 1;
            var periodic = config as IMobaContinuousPeriodicConfig;
            var intervalState = continuous as IMobaContinuousIntervalState;
            var projection = config as IMobaContinuousProjectionConfig;

            return new MobaContinuousRuntimeView(
                continuous,
                config.Id,
                string.IsNullOrEmpty(debug.Kind) ? continuous.GetType().Name : debug.Kind,
                debug.ConfigId,
                config.OwnerId,
                ownerActorId,
                debug.SourceActorId,
                debug.TargetActorId,
                debug.SourceContextId,
                debug.ParentContextId,
                debug.RootContextId,
                debug.OwnerContextId,
                debug.SkillRuntimeHandle,
                continuous.State,
                continuous.IsActive,
                continuous.IsPaused,
                continuous.IsTerminated,
                continuous.ElapsedSeconds,
                duration,
                remaining,
                stack,
                maxStack,
                periodic?.IntervalSeconds ?? 0f,
                intervalState?.IntervalRemainingSeconds ?? 0f,
                CopyIntervalEffectIds(periodic),
                BuildTagViews(config as IMobaContinuousTagConfig, projection),
                BuildModifierViews(config as IMobaContinuousModifierConfig, projection, stack),
                BuildModifierExplanations(continuous, config as IMobaContinuousModifierConfig, projection, stack),
                _tagRules?.GetLastResult(continuous) ?? MobaContinuousTagRuleResult.None,
                _tagRules?.Explain(continuous) ?? MobaContinuousTagRuleResult.None,
                ResolveContextSource(continuous, in debug));
        }

        private static MobaContinuousRuntimeDebugInfo ResolveDebugInfo(IContinuous continuous)
        {
            if (continuous is IMobaContinuousRuntimeDebugSource source && source.TryGetRuntimeDebugInfo(out var info))
                return info;

            return default;
        }

        private static MobaContextSourceView ResolveContextSource(IContinuous continuous, in MobaContinuousRuntimeDebugInfo debug)
        {
            if (debug.ContextSource.IsValid) return debug.ContextSource;
            if (continuous is IMobaContextSourceProvider provider && provider.TryGetContextSource(out var source) && source.IsValid) return source;
            if (debug.Kind != null || debug.ConfigId != 0 || debug.SourceActorId != 0 || debug.TargetActorId != 0 || debug.SourceContextId != 0 || debug.SkillRuntimeHandle.IsValid)
                return MobaContextSourceView.FromRuntimeDebug(in debug);
            return default;
        }

        private static int ResolveOwnerActorId(IContinuousConfig config, in MobaContinuousRuntimeDebugInfo debug)
        {
            if (config is IMobaContinuousProjectionConfig projection && projection.OwnerActorId > 0)
                return projection.OwnerActorId;

            if (debug.TargetActorId > 0)
                return debug.TargetActorId;

            var ownerId = config.OwnerId;
            return ownerId > 0 && ownerId <= int.MaxValue ? (int)ownerId : 0;
        }

        private static float ResolveRemainingSeconds(IContinuous continuous, float duration)
        {
            if (duration <= 0f) return 0f;
            var remaining = duration - continuous.ElapsedSeconds;
            return remaining > 0f ? remaining : 0f;
        }

        private static IReadOnlyList<int> CopyIntervalEffectIds(IMobaContinuousPeriodicConfig periodic)
        {
            var ids = periodic?.IntervalEffectIds;
            if (ids == null || ids.Count == 0) return Array.Empty<int>();

            var result = new int[ids.Count];
            for (int i = 0; i < ids.Count; i++)
                result[i] = ids[i];
            return result;
        }

        private static IReadOnlyList<MobaContinuousRuntimeTagView> BuildTagViews(IMobaContinuousTagConfig tagConfig, IMobaContinuousProjectionConfig projection)
        {
            var tags = tagConfig?.TagRequirements?.ApplicationTags;
            if (tags == null || tags.Count == 0) return Array.Empty<MobaContinuousRuntimeTagView>();

            var result = new List<MobaContinuousRuntimeTagView>(tags.Count);
            var source = projection?.TagSource ?? GameplayTagSource.System;
            foreach (var tag in tags)
            {
                if (!tag.IsValid) continue;
                result.Add(new MobaContinuousRuntimeTagView(tag.Value, tag.TagName, source));
            }

            return result;
        }

        private IReadOnlyList<MobaContinuousModifierExplainResult> BuildModifierExplanations(IContinuous continuous, IMobaContinuousModifierConfig modifierConfig, IMobaContinuousProjectionConfig projection, int stack)
        {
            var modifiers = modifierConfig?.Modifiers;
            if (continuous == null || modifiers == null || modifiers.Count == 0 || projection == null || _modifiers == null)
                return Array.Empty<MobaContinuousModifierExplainResult>();

            List<MobaContinuousModifierExplainResult> result = null;
            var normalizedStack = stack < 1 ? 1 : stack;
            for (int i = 0; i < modifiers.Count; i++)
            {
                var spec = modifiers[i];
                if (spec == null) continue;

                var entry = new MobaContinuousModifierEntry(continuous, projection, spec, normalizedStack);
                if (!_modifiers.TryExplainModifier(entry, out var explanation)) continue;

                result ??= new List<MobaContinuousModifierExplainResult>(modifiers.Count);
                result.Add(explanation);
            }

            return result ?? (IReadOnlyList<MobaContinuousModifierExplainResult>)Array.Empty<MobaContinuousModifierExplainResult>();
        }

        private static IReadOnlyList<MobaContinuousRuntimeModifierView> BuildModifierViews(IMobaContinuousModifierConfig modifierConfig, IMobaContinuousProjectionConfig projection, int stack)
        {
            var modifiers = modifierConfig?.Modifiers;
            if (modifiers == null || modifiers.Count == 0) return Array.Empty<MobaContinuousRuntimeModifierView>();

            var result = new List<MobaContinuousRuntimeModifierView>(modifiers.Count);
            var sourceId = projection?.ModifierSourceId ?? 0;
            for (int i = 0; i < modifiers.Count; i++)
            {
                var spec = modifiers[i];
                if (spec == null) continue;
                var magnitude = spec.Magnitude;
                result.Add(new MobaContinuousRuntimeModifierView(
                    spec.TargetKind,
                    spec.TargetId,
                    spec.Op,
                    spec.Value,
                    magnitude.Type,
                    magnitude.BaseValue,
                    magnitude.Coefficient,
                    spec.EvaluationPolicy,
                    spec.Priority,
                    stack < 1 ? 1 : stack,
                    sourceId));
            }

            return result;
        }
    }
}
