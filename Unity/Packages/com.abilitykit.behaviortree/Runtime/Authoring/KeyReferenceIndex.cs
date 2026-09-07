using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Registry;

namespace AbilityKit.BehaviorTree.Authoring
{
    public static class KeyReferenceIndex
    {
        public static List<(string NodeId, string PropertyName)> FindReferences(
            TreeDefinition definition,
            NodeRegistry registry,
            string keyName)
        {
            var result = new List<(string, string)>();
            if (definition == null || registry == null || string.IsNullOrEmpty(keyName)) return result;

            foreach (var node in definition.Nodes)
            {
                if (!registry.TryGetDescriptor(node.Type, out var descriptor)) continue;

                var keyRefFields = new HashSet<string>(StringComparer.Ordinal);
                foreach (var field in descriptor.PropertySchema)
                {
                    if (field.Kind == PropertyFieldKind.BlackboardKeyRef) keyRefFields.Add(field.Name);
                }

                foreach (var pair in node.Properties.Values)
                {
                    if (!keyRefFields.Contains(pair.Key)) continue;
                    if (pair.Value.TryGetString(out var value)
                        && string.Equals(value, keyName, StringComparison.Ordinal))
                    {
                        result.Add((node.Id, pair.Key));
                    }
                }
            }
            return result;
        }

        public static List<(string NodeId, string PropertyName)> RenameKey(
            TreeDefinition definition,
            NodeRegistry registry,
            string oldName,
            string newName)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (string.IsNullOrEmpty(oldName)) throw new ArgumentException("oldName 不能为空。", nameof(oldName));
            if (string.IsNullOrEmpty(newName)) throw new ArgumentException("newName 不能为空。", nameof(newName));
            if (string.Equals(oldName, newName, StringComparison.Ordinal)) return new List<(string, string)>();

            BlackboardKeyDefinition? target = null;
            foreach (var key in definition.Blackboard.Keys)
            {
                if (string.Equals(key.Name, oldName, StringComparison.Ordinal)) target = key;
                else if (string.Equals(key.Name, newName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"黑板 key '{newName}' 已存在。");
                }
            }
            if (target == null) throw new InvalidOperationException($"黑板 key '{oldName}' 不存在。");

            var affected = new List<(string, string)>();
            foreach (var node in definition.Nodes)
            {
                if (!registry.TryGetDescriptor(node.Type, out var descriptor)) continue;
                foreach (var field in descriptor.PropertySchema)
                {
                    if (field.Kind != PropertyFieldKind.BlackboardKeyRef) continue;
                    if (!node.Properties.TryGet(field.Name, out var value)) continue;
                    if (!value.TryGetString(out var refName)
                        || !string.Equals(refName, oldName, StringComparison.Ordinal)) continue;

                    node.Properties.Set(field.Name, PropertyValue.Of(newName));
                    affected.Add((node.Id, field.Name));
                }
            }

            target.Name = newName;
            return affected;
        }
    }

    [System.Obsolete("Use AbilityKit.BehaviorTree.Authoring.KeyReferenceIndex.", false)]
    public static class BtKeyReferenceIndex
    {
#pragma warning disable CS0618
        public static List<(string NodeId, string PropertyName)> FindReferences(
            BtTreeDefinition definition,
            BtNodeRegistry registry,
            string keyName)
            => KeyReferenceIndex.FindReferences(
                AuthoringCompatibility.ToModel(definition),
                AbilityKit.BehaviorTree.Registry.NodeRegistry.FromLegacy(registry),
                keyName);

        public static List<(string NodeId, string PropertyName)> RenameKey(
            BtTreeDefinition definition,
            BtNodeRegistry registry,
            string oldName,
            string newName)
        {
            var model = AuthoringCompatibility.ToModel(definition);
            var affected = KeyReferenceIndex.RenameKey(
                model,
                AbilityKit.BehaviorTree.Registry.NodeRegistry.FromLegacy(registry),
                oldName,
                newName);

            var migrated = AuthoringCompatibility.ToLegacy(model);
            definition.Blackboard = migrated.Blackboard;
            definition.Nodes = migrated.Nodes;
            definition.RootNodeId = migrated.RootNodeId;
            definition.FormatVersion = migrated.FormatVersion;
            definition.TreeId = migrated.TreeId;
            return affected;
        }
#pragma warning restore CS0618
    }
}
