using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Definition
{
    public sealed class NodeDefinition
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public PropertyBag Properties { get; set; } = new();
        public List<string> ChildIds { get; set; } = new();




    }
}
