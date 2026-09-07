// ============================================================================
// Behavior Descriptor Implementations - 行为描述器实现
// 将现有的 BehaviorItem 适配到描述器接口
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace AbilityKit.HFSM.Graph.Descriptor.Impl
{

    /// <summary>
    /// 行为描述器工厂
    /// </summary>
    public static class BehaviorDescriptorFactory
    {
        public static IBehaviorDescriptor Create(BehaviorItem item)
        {
            return new BehaviorDescriptor(item);
        }

        public static List<IBehaviorDescriptor> CreateRange(IEnumerable<BehaviorItem> items)
        {
            return items?.Select(Create).ToList() ?? new List<IBehaviorDescriptor>();
        }
    }
}
