using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Services
{
    public interface ISkillWaitCondition
    {
        string Id { get; }
        bool IsMet(SkillPipelineContext context, SkillWaitUntilPhaseDTO specification);
        bool TryValidate(SkillWaitUntilPhaseDTO specification, out string error);
    }

    public abstract class SkillWaitConditionBase : ISkillWaitCondition
    {
        public abstract string Id { get; }

        public abstract bool IsMet(SkillPipelineContext context, SkillWaitUntilPhaseDTO specification);

        public virtual bool TryValidate(SkillWaitUntilPhaseDTO specification, out string error)
        {
            error = null;
            return true;
        }

        protected static bool HasArguments(SkillWaitUntilPhaseDTO specification)
        {
            return specification?.Arguments != null && specification.Arguments.Length > 0;
        }
    }

    public static class SkillWaitConditionCatalog
    {
        private static readonly Dictionary<string, ISkillWaitCondition> Conditions =
            new Dictionary<string, ISkillWaitCondition>(StringComparer.OrdinalIgnoreCase);

        static SkillWaitConditionCatalog()
        {
            Register(new ObservedSlotsIdleSkillWaitCondition());
            Register(new InputReleasedSkillWaitCondition());
        }

        public static void Register(ISkillWaitCondition condition)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            if (string.IsNullOrWhiteSpace(condition.Id)) throw new ArgumentException("Skill wait condition id is required.", nameof(condition));

            Conditions[condition.Id] = condition;
        }

        public static bool TryGet(string id, out ISkillWaitCondition condition)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                condition = null;
                return false;
            }

            return Conditions.TryGetValue(id, out condition);
        }

        public static bool TryValidate(SkillWaitUntilPhaseDTO specification, out string error)
        {
            if (specification == null)
            {
                error = "wait condition specification is missing.";
                return false;
            }

            if (!TryGet(specification.Condition, out var condition))
            {
                error = $"unsupported wait condition '{specification.Condition ?? string.Empty}'.";
                return false;
            }

            return condition.TryValidate(specification, out error);
        }
    }

    public sealed class ObservedSlotsIdleSkillWaitCondition : SkillWaitConditionBase
    {
        public override string Id => "ObservedSlotsIdle";

        public override bool IsMet(SkillPipelineContext context, SkillWaitUntilPhaseDTO specification)
        {
            var observedSlots = specification?.ObservedSlots;
            if (context == null || observedSlots == null || observedSlots.Length == 0) return true;
            if (context.WorldServices == null) return true;
            if (!context.WorldServices.TryResolve<SkillCastCoordinator>(out var skills) || skills == null) return true;

            for (var i = 0; i < observedSlots.Length; i++)
            {
                var slot = observedSlots[i];
                if (slot <= 0 || slot == context.SkillSlot) continue;
                if (skills.TryGetRunningBySlot(context.CasterActorId, slot, out _)) return false;
            }

            return true;
        }

        public override bool TryValidate(SkillWaitUntilPhaseDTO specification, out string error)
        {
            if (HasArguments(specification))
            {
                error = "ObservedSlotsIdle does not accept arguments; use ObservedSlots.";
                return false;
            }

            error = null;
            return true;
        }
    }

    public sealed class InputReleasedSkillWaitCondition : SkillWaitConditionBase
    {
        public override string Id => "InputReleased";

        public override bool IsMet(SkillPipelineContext context, SkillWaitUntilPhaseDTO specification)
        {
            return context != null && context.IsInputReleased();
        }

        public override bool TryValidate(SkillWaitUntilPhaseDTO specification, out string error)
        {
            if (HasArguments(specification))
            {
                error = "InputReleased does not accept arguments.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
