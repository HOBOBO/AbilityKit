using System.Collections.Generic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 运行IR 的节点定义。Type 是注册中心类id（字符串，无 CLR 类型名）    /// ChildIds 直接内嵌有序子节id，无独立边表    /// </summary>
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public sealed class BtNodeDefinition
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public BtPropertyBag Properties { get; set; } = new();
        public List<string> ChildIds { get; set; } = new();
    }
}
