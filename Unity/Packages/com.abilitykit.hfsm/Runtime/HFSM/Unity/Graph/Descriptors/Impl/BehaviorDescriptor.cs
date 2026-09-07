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
    /// 行为描述器实现
    /// </summary>
    [Serializable]
    public class BehaviorDescriptor : IBehaviorDescriptor
    {
        private readonly BehaviorItem _item;
        private List<IBehaviorParameterDescriptor> _paramDescriptors;

        public BehaviorDescriptor(BehaviorItem item)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            InitializeParameterDescriptors();
        }

        public string Id => _item.id;
        public string Name => _item.displayName;
        public string TypeName => _item.TypeName;
        public string ParentId => _item.parentId;
        public IReadOnlyList<string> ChildIds => _item.childIds;
        public bool IsExpanded => _item.isExpanded;

        public IReadOnlyList<IBehaviorParameterDescriptor> GetParameters() => _paramDescriptors;

        public bool HasParameter(string name)
        {
            return _item.parameters?.Any(p => p.name == name) ?? false;
        }

        public IBehaviorParameterDescriptor GetParameter(string name)
        {
            var param = _item.GetParameter(name);
            return param != null ? new BehaviorParameterDescriptor(param) : null;
        }

        private void InitializeParameterDescriptors()
        {
            _paramDescriptors = new List<IBehaviorParameterDescriptor>();
            if (_item.parameters != null)
            {
                foreach (var p in _item.parameters)
                {
                    _paramDescriptors.Add(new BehaviorParameterDescriptor(p));
                }
            }
        }

    }
}
