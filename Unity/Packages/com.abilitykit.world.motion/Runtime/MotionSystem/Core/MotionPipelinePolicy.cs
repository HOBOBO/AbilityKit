using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Common.MotionSystem.Core
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
            // Typical rule: Control suppresses other groups.
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
