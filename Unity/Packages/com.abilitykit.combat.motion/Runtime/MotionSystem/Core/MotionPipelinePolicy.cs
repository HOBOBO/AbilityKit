using System;
using System.Collections.Generic;

namespace AbilityKit.Combat.MotionSystem.Core
{
    public sealed class MotionPipelinePolicy
    {
        private Dictionary<int, int[]> _suppressedByGroup;

        public MotionPipelinePolicy()
        {
            _suppressedByGroup = new Dictionary<int, int[]>(8);
        }

        public static MotionPipelinePolicy CreateDefault()
        {
            var p = new MotionPipelinePolicy();
            // 主动技能位移运行期间应接管常规移动。
            p.SetSuppressedGroups(MotionGroups.Ability, MotionGroups.Locomotion);
            // 寻路跟随运行期间接管常规移动（AI 导航时压制直线摇杆输入）。
            p.SetSuppressedGroups(MotionGroups.Path, MotionGroups.Locomotion);
            // 硬控位移会压制主动移动，但允许被动位移来源继续叠加。
            p.SetSuppressedGroups(MotionGroups.Control, MotionGroups.Locomotion, MotionGroups.Ability, MotionGroups.Path);
            return p;
        }

        public void SetSuppressedGroups(int suppressorGroupId, params int[] suppressedGroupIds)
        {
            if (suppressedGroupIds == null || suppressedGroupIds.Length == 0)
            {
                _suppressedByGroup.Remove(suppressorGroupId);
                return;
            }

            var copy = new int[suppressedGroupIds.Length];
            Array.Copy(suppressedGroupIds, copy, suppressedGroupIds.Length);
            _suppressedByGroup[suppressorGroupId] = copy;
        }

        public bool TryGetSuppressedGroups(int suppressorGroupId, out int[] suppressedGroupIds)
        {
            return _suppressedByGroup.TryGetValue(suppressorGroupId, out suppressedGroupIds);
        }

        /// <summary>
        /// 捕获完整策略值，并为规则和每条规则的组数组创建独立副本。
        /// </summary>
        public MotionPipelinePolicyState CaptureState()
        {
            var rules = new MotionGroupSuppressionState[_suppressedByGroup.Count];
            var index = 0;
            foreach (var pair in _suppressedByGroup)
            {
                var groups = new int[pair.Value.Length];
                Array.Copy(pair.Value, groups, groups.Length);
                rules[index++] = new MotionGroupSuppressionState
                {
                    SuppressorGroupId = pair.Key,
                    SuppressedGroupIds = groups,
                };
            }

            Array.Sort(rules, CompareRules);
            return new MotionPipelinePolicyState { Rules = rules };
        }

        /// <summary>
        /// 只读检查完整策略状态，不访问或修改运行中的策略。
        /// </summary>
        public static void ValidateState(in MotionPipelinePolicyState state)
        {
            if (state.Rules == null)
                throw new ArgumentException("Rules must not be null.", nameof(state));

            var previousKey = 0;
            for (var i = 0; i < state.Rules.Length; i++)
            {
                var rule = state.Rules[i];
                if (rule.SuppressedGroupIds == null)
                    throw new ArgumentException($"Rules[{i}].SuppressedGroupIds must not be null.", nameof(state));
                if (rule.SuppressedGroupIds.Length == 0)
                    throw new ArgumentException($"Rules[{i}].SuppressedGroupIds must not be empty.", nameof(state));
                if (i > 0 && rule.SuppressorGroupId <= previousKey)
                    throw new ArgumentException($"Rules[{i}].SuppressorGroupId must be unique and strictly ascending.", nameof(state));
                previousKey = rule.SuppressorGroupId;
            }
        }

        /// <summary>
        /// 完整替换策略映射；验证或准备失败时保留当前映射不变。
        /// </summary>
        public void RestoreState(in MotionPipelinePolicyState state)
        {
            ValidateState(in state);

            var restored = new Dictionary<int, int[]>(state.Rules.Length);
            for (var i = 0; i < state.Rules.Length; i++)
            {
                var rule = state.Rules[i];
                var groups = new int[rule.SuppressedGroupIds.Length];
                Array.Copy(rule.SuppressedGroupIds, groups, groups.Length);
                restored.Add(rule.SuppressorGroupId, groups);
            }

            _suppressedByGroup = restored;
        }

        private static int CompareRules(MotionGroupSuppressionState left, MotionGroupSuppressionState right)
        {
            return left.SuppressorGroupId.CompareTo(right.SuppressorGroupId);
        }
    }
}
