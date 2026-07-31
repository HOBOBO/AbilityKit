using System;
using System.Collections.Generic;
using System.Globalization;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using BTCore.Runtime;
using BTCore.Runtime.Externals;

namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
{
    /// <summary>
    /// Selects a cooldown-ready skill candidate. Range and cast-state checks are intentionally
    /// separate conditions so the same candidate can drive cast, approach, or hold branches.
    /// </summary>
    public sealed class MobaSelectReadySkillAction : ExternalAction, IMobaBTreeContextNode
    {
        internal const float DefaultApproachRange = 0.5f;
        private const string SkillIdProperty = "skillId";
        private const string RequiredTagProperty = "requiredTag";

        private MobaBTreeRuntimeContext _context;

        public void Bind(MobaBTreeRuntimeContext context)
        {
            _context = context;
        }

        protected override NodeState OnUpdate()
        {
            if (Blackboard == null) return NodeState.Failure;
            MobaBTreeBlackboard.ClearSkill(Blackboard, DefaultApproachRange);

            var behavior = _context?.Behavior;
            var registry = _context?.Registry;
            var config = _context?.Config;
            if (behavior == null || registry == null || config == null
                || behavior.OwnerId.Value <= 0 || behavior.OwnerId.Value > int.MaxValue)
                return NodeState.Success;
            if (!registry.TryGet((int)behavior.OwnerId.Value, out var owner)
                || owner == null || !owner.hasSkillLoadout)
                return NodeState.Success;

            var skills = owner.skillLoadout.ActiveSkills;
            if (skills == null || skills.Length == 0) return NodeState.Success;
 
            var requiredSkillId = ReadIntProperty(SkillIdProperty);
            var requiredTag = ReadIntProperty(RequiredTagProperty);
            var nowMs = _context.GetCurrentTimeMs();
            var maxConfiguredRange = 0f;
            var candidates = new List<MobaSkillSelectionCandidate>();
 
            for (var i = 0; i < skills.Length; i++)
            {
                var runtime = skills[i];
                if (runtime == null || runtime.SkillId <= 0) continue;
                if (!config.TryGetSkill(runtime.SkillId, out var skill) || skill == null) continue;
 
                var range = Math.Max(0f, skill.Range);
                maxConfiguredRange = Math.Max(maxConfiguredRange, range);
                if (requiredSkillId > 0 && skill.Id != requiredSkillId) continue;
                if (requiredTag > 0 && !HasTag(skill, requiredTag)) continue;
                if (runtime.CooldownEndTimeMs > nowMs) continue;
 
                candidates.Add(new MobaSkillSelectionCandidate(skill.Id, i + 1, range));
            }
 
            if (MobaBrainSkillSelectionPolicies.TrySelect(
                    _context.SkillSelectionPolicy,
                    candidates,
                    out var selected)
                && config.TryGetSkill(selected.SkillId, out var selectedSkill)
                && selectedSkill != null)
            {
                Blackboard.SetValue(MobaBTreeKeys.SkillId, selected.SkillId);
                Blackboard.SetValue(MobaBTreeKeys.SkillSlot, selected.Slot);
                Blackboard.SetValue(MobaBTreeKeys.SkillRange, selected.Range);
                Blackboard.SetValue(MobaBTreeKeys.SkillApproachRange,
                    selected.Range > 0f ? selected.Range : DefaultApproachRange);
                Blackboard.SetValue(MobaBTreeKeys.SkillCategory, selectedSkill.Category);
                Blackboard.SetValue(MobaBTreeKeys.SkillType, (int)selectedSkill.SkillType);
                Blackboard.SetValue(MobaBTreeKeys.SkillTargetQueryId, selectedSkill.RequiredTargetQueryId);
                Blackboard.SetValue(MobaBTreeKeys.SkillValid, true);
                return NodeState.Success;
            }
 
            if (maxConfiguredRange > 0f)
                Blackboard.SetValue(MobaBTreeKeys.SkillApproachRange, maxConfiguredRange);
            return NodeState.Success;
        }

        private int ReadIntProperty(string name)
        {
            return Properties != null
                   && Properties.TryGetValue(name, out var value)
                   && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
        }

        private static bool HasTag(SkillMO skill, int requiredTag)
        {
            if (skill?.Tags == null) return false;
            for (var i = 0; i < skill.Tags.Count; i++)
            {
                if (skill.Tags[i] == requiredTag) return true;
            }

            return false;
        }
    }
}
