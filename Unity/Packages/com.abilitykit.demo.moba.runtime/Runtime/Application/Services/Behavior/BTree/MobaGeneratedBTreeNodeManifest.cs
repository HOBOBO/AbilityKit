using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
{
    internal static partial class MobaGeneratedBTreeNodeManifest
    {
        public static IReadOnlyDictionary<string, Type> CreateNodeTypes()
        {
            var nodeTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
            AddGenerated(nodeTypes);
            return nodeTypes;
        }

        static partial void AddGenerated(Dictionary<string, Type> nodeTypes);
    }
}
