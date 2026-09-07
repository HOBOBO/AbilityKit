using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Definition
{
    public sealed class NodeDefinition
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public PropertyBag Properties { get; set; } = new();
        public List<string> ChildIds { get; set; } = new();

        internal AbilityKit.BehaviorTree.BtNodeDefinition ToLegacy() => new()
        {
            Id = Id,
            Type = Type,
            Properties = Properties.ToLegacy(),
            ChildIds = new List<string>(ChildIds),
        };

        internal static NodeDefinition FromLegacy(AbilityKit.BehaviorTree.BtNodeDefinition source) => new()
        {
            Id = source.Id,
            Type = source.Type,
            Properties = PropertyBag.FromLegacy(source.Properties),
            ChildIds = new List<string>(source.ChildIds),
        };
    }
}
