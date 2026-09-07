using System;

namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 标注一个节点类参与注册中心扫描。领域包用它声明包外节点    /// <code>[BtNodeType("moba.select_nearest_enemy", "选取最近敌, "MOBA", BtNodeKind.Action)]</code>
    /// 属schema 可由 <see cref="BtNodeDescriptorProvider"/> 在节点类上补充    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public sealed class BtNodeTypeAttribute : Attribute
    {
        public string NodeTypeId { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public BtNodeKind Kind { get; }

        public BtNodeTypeAttribute(string nodeTypeId, string displayName, string category, BtNodeKind kind)
        {
            NodeTypeId = nodeTypeId;
            DisplayName = displayName;
            Category = category;
            Kind = kind;
        }
    }

    /// <summary>
    /// 可选：节点类实现此接口以补充完整描述符（属schema、端口约束、黑板引用）    /// 未实现时attribute 参数推导最小描述符    /// </summary>
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public interface BtNodeDescriptorProvider
    {
        BtNodeDescriptor BuildDescriptor(BtNodeTypeAttribute attribute);
    }
}
