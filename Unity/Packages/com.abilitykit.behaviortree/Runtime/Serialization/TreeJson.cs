using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree.Serialization
{
    using AbilityKit.BehaviorTree.Definition;
    using AbilityKit.BehaviorTree.Execution;

    public static class TreeJson
    {
        public static string Save(TreeDefinition definition)
            => CanonicalTreeJson.Save(definition);

        public static TreeDefinition Load(string json)
            => CanonicalTreeJson.Load(json);

        public static string SaveSnapshot(TreeRuntimeSnapshot snapshot)
            => CanonicalTreeJson.SaveSnapshot(snapshot);

        public static TreeRuntimeSnapshot LoadSnapshot(string json)
            => CanonicalTreeJson.LoadSnapshot(json);
    }
}
