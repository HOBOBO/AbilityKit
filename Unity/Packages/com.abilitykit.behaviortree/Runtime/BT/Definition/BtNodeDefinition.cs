using System.Collections.Generic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 运行时 IR 的节点定义。Type 是注册中心类型 id（字符串，无 CLR 类型名）；
    /// ChildIds 直接内嵌有序子节点 id，无独立边表。
    /// </summary>
    public sealed class BtNodeDefinition
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public string Comment { get; set; } = "";
        public BtPropertyBag Properties { get; set; } = new();
        public List<string> ChildIds { get; set; } = new();
    }
}
