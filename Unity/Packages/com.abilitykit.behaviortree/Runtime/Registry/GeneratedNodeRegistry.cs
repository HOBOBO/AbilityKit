using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Registry
{
    public static class GeneratedNodeRegistry
    {
        public static void RegisterAll(NodeRegistry registry, IEnumerable<NodeRegistryModule> modules)
        {
            foreach (var module in modules)
            {
                module.Register(registry);
            }
        }
    }
}
