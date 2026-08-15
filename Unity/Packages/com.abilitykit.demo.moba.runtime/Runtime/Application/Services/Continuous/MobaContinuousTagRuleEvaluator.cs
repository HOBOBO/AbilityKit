using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Continuous;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaContinuousTagRuleEvaluator
    {
        MobaContinuousTagRuleResult Evaluate(IContinuous continuous);
        bool CanActivate(IContinuous continuous, out string reason);
    }

    [WorldService(typeof(IMobaContinuousTagRuleEvaluator))]
    internal sealed class MobaContinuousTagRuleEvaluator : IMobaContinuousTagRuleEvaluator
    {
        private readonly IMobaEffectiveTagQueryService _effectiveTags;

        public MobaContinuousTagRuleEvaluator(IMobaEffectiveTagQueryService effectiveTags)
        {
            _effectiveTags = effectiveTags;
        }

        public MobaContinuousTagRuleResult Evaluate(IContinuous continuous)
        {
            if (continuous == null || continuous.Config == null)
                return new MobaContinuousTagRuleResult(0, MobaContinuousTagRuleAction.None, false, "Continuous or config is null", null, null);

            if (!(continuous.Config is IMobaContinuousTagConfig tagConfig) || tagConfig.TagRequirements == null)
                return new MobaContinuousTagRuleResult(ResolveOwnerActorId(continuous), MobaContinuousTagRuleAction.None, true, "No tag requirements", null, null);

            var ownerActorId = ResolveOwnerActorId(continuous);
            var tags = _effectiveTags?.GetEffectiveTags(ownerActorId);
            var requirements = tagConfig.TagRequirements;
            var activationCheck = BuildRequirementCheck("Activation", requirements.ActivationRequired, tags);
            var removalCheck = BuildRemovalCheck(requirements, tags);
            var ongoingCheck = BuildRequirementCheck("Ongoing", requirements.OngoingRequired, tags);

            MobaContinuousTagRuleAction action;
            var allowed = true;
            var reason = "Tag rules satisfied";
            if (!activationCheck.Satisfied)
            {
                action = MobaContinuousTagRuleAction.BlockActivate;
                allowed = false;
                reason = "Activation requirements are not satisfied";
            }
            else if (removalCheck.Satisfied)
            {
                action = MobaContinuousTagRuleAction.Remove;
                allowed = false;
                reason = "Removal requirements are satisfied";
            }
            else if (!ongoingCheck.Satisfied)
            {
                action = MobaContinuousTagRuleAction.Pause;
                allowed = false;
                reason = "Ongoing requirements are not satisfied";
            }
            else
            {
                action = continuous.IsPaused ? MobaContinuousTagRuleAction.Resume : MobaContinuousTagRuleAction.KeepActive;
            }

            return BuildResult(ownerActorId, action, allowed, reason, tags, activationCheck, removalCheck, ongoingCheck);
        }

        public bool CanActivate(IContinuous continuous, out string reason)
        {
            var result = Evaluate(continuous);
            reason = result.Reason;
            return result.Allowed;
        }

        private static MobaContinuousTagRuleResult BuildResult(int ownerActorId, MobaContinuousTagRuleAction action, bool allowed, string reason, GameplayTagContainer tags, params MobaContinuousTagRequirementCheck[] checks)
        {
            return new MobaContinuousTagRuleResult(ownerActorId, action, allowed, reason, CopyTagViews(tags), checks);
        }

        private static MobaContinuousTagRequirementCheck BuildRemovalCheck(ContinuousTagRequirements requirements, GameplayTagContainer tags)
        {
            var removal = requirements?.RemovalRequired ?? new GameplayTagRequirements();
            var hasRequired = removal.Required != null && !removal.Required.IsEmpty;
            var hasBlocked = removal.Blocked != null && !removal.Blocked.IsEmpty;
            var requiredSatisfied = !hasRequired || (tags != null && tags.HasAll(removal.Required, removal.Exact));
            var blockedSatisfied = !hasBlocked || tags == null || !tags.HasAny(removal.Blocked, removal.Exact);
            var satisfied = (hasRequired || hasBlocked) && requiredSatisfied && blockedSatisfied;
            return new MobaContinuousTagRequirementCheck("Removal", satisfied, requiredSatisfied, blockedSatisfied, removal.Exact, CopyTagViews(removal.Required), CopyTagViews(removal.Blocked));
        }

        private static MobaContinuousTagRequirementCheck BuildRequirementCheck(string category, GameplayTagRequirements requirements, GameplayTagContainer tags)
        {
            var requiredSatisfied = requirements.Required == null || requirements.Required.IsEmpty || (tags != null && tags.HasAll(requirements.Required, requirements.Exact));
            var blockedSatisfied = requirements.Blocked == null || requirements.Blocked.IsEmpty || tags == null || !tags.HasAny(requirements.Blocked, requirements.Exact);
            return new MobaContinuousTagRequirementCheck(category, requiredSatisfied && blockedSatisfied, requiredSatisfied, blockedSatisfied, requirements.Exact, CopyTagViews(requirements.Required), CopyTagViews(requirements.Blocked));
        }

        private static IReadOnlyList<MobaContinuousTagRuleTagView> CopyTagViews(GameplayTagContainer tags)
        {
            if (tags == null || tags.Count == 0) return new List<MobaContinuousTagRuleTagView>(0);

            var result = new List<MobaContinuousTagRuleTagView>(tags.Count);
            foreach (var tag in tags)
            {
                if (!tag.IsValid) continue;
                result.Add(new MobaContinuousTagRuleTagView(tag.Value, tag.TagName));
            }

            return result;
        }

        private static int ResolveOwnerActorId(IContinuous continuous)
        {
            if (continuous?.Config is IMobaContinuousProjectionConfig projection && projection.OwnerActorId > 0)
                return projection.OwnerActorId;

            var ownerId = continuous?.Config?.OwnerId ?? 0L;
            return ownerId > 0 && ownerId <= int.MaxValue ? (int)ownerId : 0;
        }
    }
}
