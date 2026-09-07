using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree.Nodes;

namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 节点描述符：注册中心的登记项。编辑器只认识描述符（菜端口/属性面板全部由
    /// PropertySchema 驱动生成），不认CLR 继承链——这是包外扩展零编辑器代码的基础    /// </summary>
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public sealed class BtNodeDescriptor
    {
        public string TypeId { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public BtNodeKind Kind { get; }
        public int MinChildren { get; }
        /// <summary>最大子节点数；-1 表示不限</summary>
        public int MaxChildren { get; }
        public IReadOnlyList<BtPropertyField> PropertySchema { get; }
        public IReadOnlyList<BtBlackboardKeyRef> BlackboardKeys { get; }
        /// <summary>节点主题色（hex，如 "#4a90d9"）；null 时编辑器Kind 给默认色</summary>
        public string? ColorHint { get; }
        /// <summary>菜单内同分类排序权重（升序）</summary>
        public int MenuOrder { get; }
        public Func<NodeBase> Factory { get; }

        public BtNodeDescriptor(
            string typeId,
            string displayName,
            string category,
            BtNodeKind kind,
            int minChildren,
            int maxChildren,
            Func<NodeBase> factory,
            IReadOnlyList<BtPropertyField>? propertySchema = null,
            IReadOnlyList<BtBlackboardKeyRef>? blackboardKeys = null,
            string? colorHint = null,
            int menuOrder = 0)
        {
            TypeId = typeId;
            DisplayName = displayName;
            Category = category;
            Kind = kind;
            MinChildren = minChildren;
            MaxChildren = maxChildren;
            Factory = factory;
            PropertySchema = propertySchema ?? Array.Empty<BtPropertyField>();
            BlackboardKeys = blackboardKeys ?? Array.Empty<BtBlackboardKeyRef>();
            ColorHint = colorHint;
            MenuOrder = menuOrder;
        }
    }
}

