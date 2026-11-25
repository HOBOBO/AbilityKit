using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Services.Behavior
{
    /// <summary>
    /// 决定大脑如何对行为树筛选后冷却就绪的技能排序。
    /// </summary>
    public enum MobaBrainSkillSelectionPolicy
    {
        FirstReady = 0,
        HighestRange = 1,
    }

    public readonly struct MobaSkillSelectionCandidate
    {
        public MobaSkillSelectionCandidate(int skillId, int slot, float range)
        {
            SkillId = skillId;
            Slot = slot;
            Range = range;
        }

        public int SkillId { get; }
        public int Slot { get; }
        public float Range { get; }
    }

    /// <summary>
    /// 无状态、确定性的大脑级技能排序策略。
    /// </summary>
    public static class MobaBrainSkillSelectionPolicies
    {
        public static bool TrySelect(
            MobaBrainSkillSelectionPolicy policy,
            IReadOnlyList<MobaSkillSelectionCandidate> candidates,
            out MobaSkillSelectionCandidate selected)
        {
            selected = default;
            if (candidates == null || candidates.Count == 0) return false;

            var best = candidates[0];
            for (var i = 1; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (IsPreferred(policy, in candidate, in best)) best = candidate;
            }

            selected = best;
            return true;
        }

        private static bool IsPreferred(
            MobaBrainSkillSelectionPolicy policy,
            in MobaSkillSelectionCandidate candidate,
            in MobaSkillSelectionCandidate current)
        {
            if (policy == MobaBrainSkillSelectionPolicy.HighestRange)
            {
                if (candidate.Range != current.Range) return candidate.Range > current.Range;
            }

            return candidate.Slot < current.Slot;
        }
    }
}
