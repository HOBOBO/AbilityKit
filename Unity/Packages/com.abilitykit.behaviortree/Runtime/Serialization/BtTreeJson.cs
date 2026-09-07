using System;

namespace AbilityKit.BehaviorTree
{
    /// <summary>运行IR 与快照的 JSON 读写兼容 facade：转发到 canonical <see cref="AbilityKit.BehaviorTree.Serialization.TreeJson"/>。</summary>
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public static class BtTreeJson
    {
        public static string Save(BtTreeDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return AbilityKit.BehaviorTree.Serialization.TreeJson.Save(
                AbilityKit.BehaviorTree.Definition.TreeDefinition.FromLegacy(definition));
        }

        public static BtTreeDefinition Load(string json)
            => AbilityKit.BehaviorTree.Serialization.TreeJson.Load(json).ToLegacy();

        public static string SaveSnapshot(BtTreeRuntimeSnapshot snapshot)
            => AbilityKit.BehaviorTree.Serialization.TreeJson.SaveSnapshot(snapshot.ToCanonical());

        public static BtTreeRuntimeSnapshot LoadSnapshot(string json)
            => BtTreeRuntimeSnapshot.FromCanonical(
                AbilityKit.BehaviorTree.Serialization.TreeJson.LoadSnapshot(json));
    }
}
