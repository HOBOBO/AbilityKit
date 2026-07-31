using System;
using System.Collections.Generic;

namespace AbilityKit.Combat.MotionSystem.Core
{
    public sealed class MotionPipelinePolicy
    {
        private readonly Dictionary<int, int[]> _suppressedByGroup;

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
    }
}
