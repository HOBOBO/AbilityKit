using System.Collections.Generic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 树定义加载校验：结构（根/唯一 id/无环/可达/单父/端口数量）、类型注册、
    /// 属性 schema、黑板 schema 与节点声明的黑板引用。全部通过才允许建实例。
    /// </summary>
    public static class BtTreeValidator
    {
        public static List<string> Validate(BtTreeDefinition definition, BtNodeRegistry registry)
        {
            var errors = new List<string>();

            if (definition == null)
            {
                errors.Add("Tree definition is null.");
                return errors;
            }
            if (registry == null)
            {
                errors.Add("Node registry is null.");
                return errors;
            }

            if (definition.FormatVersion != BtTreeDefinition.CurrentFormatVersion)
            {
                errors.Add($"Unsupported format version {definition.FormatVersion} (expected {BtTreeDefinition.CurrentFormatVersion}).");
            }
            if (string.IsNullOrEmpty(definition.TreeId))
            {
                errors.Add("TreeId must not be empty.");
            }
            if (string.IsNullOrEmpty(definition.RootNodeId))
            {
                errors.Add("RootNodeId must not be empty.");
            }
            if (definition.Nodes.Count == 0)
            {
                errors.Add("Tree must contain at least one node.");
                return errors;
            }

            var byId = new Dictionary<string, BtNodeDefinition>(definition.Nodes.Count);
            BtNodeDefinition? root = null;
            foreach (var node in definition.Nodes)
            {
                if (string.IsNullOrEmpty(node.Id))
                {
                    errors.Add("Node id must not be empty.");
                    continue;
                }
                if (!byId.TryAdd(node.Id, node))
                {
                    errors.Add($"Node id '{node.Id}' is duplicated.");
                }
                if (string.Equals(node.Id, definition.RootNodeId, System.StringComparison.Ordinal))
                {
                    root = node;
                }
            }

            if (root == null)
            {
                errors.Add($"Root node '{definition.RootNodeId}' not found.");
            }

            // 黑板 schema：重名与默认值类型
            var blackboardKeys = new HashSet<string>();
            foreach (var key in definition.Blackboard.Keys)
            {
                if (string.IsNullOrEmpty(key.Name))
                {
                    errors.Add("Blackboard key name must not be empty.");
                    continue;
                }
                if (!blackboardKeys.Add(key.Name))
                {
                    errors.Add($"Blackboard key '{key.Name}' is duplicated.");
                }
                if (key.Default != null && key.Default.Type != key.Type)
                {
                    errors.Add($"Blackboard key '{key.Name}' default value type {key.Default.Type} does not match declared {key.Type}.");
                }
            }

            // 节点类型、端口数量、属性 schema、黑板引用
            foreach (var node in definition.Nodes)
            {
                if (string.IsNullOrEmpty(node.Id)) continue;
                if (!registry.TryGetDescriptor(node.Type, out var descriptor))
                {
                    errors.Add($"Node '{node.Id}' references unknown type '{node.Type}'.");
                    continue;
                }

                var childCount = node.ChildIds.Count;
                if (childCount < descriptor.MinChildren
                    || (descriptor.MaxChildren >= 0 && childCount > descriptor.MaxChildren))
                {
                    errors.Add(
                        $"Node '{node.Id}' ({descriptor.TypeId}) has {childCount} children; allowed [{descriptor.MinChildren}, {(descriptor.MaxChildren < 0 ? "∞" : descriptor.MaxChildren.ToString())}].");
                }

                foreach (var childId in node.ChildIds)
                {
                    if (!byId.ContainsKey(childId))
                    {
                        errors.Add($"Node '{node.Id}' references missing child '{childId}'.");
                    }
                }

                foreach (var pair in node.Properties.Values)
                {
                    var matched = false;
                    foreach (var field in descriptor.PropertySchema)
                    {
                        if (!string.Equals(field.Name, pair.Key, System.StringComparison.Ordinal)) continue;
                        matched = true;
                        if (pair.Value.Type != field.Type)
                        {
                            errors.Add(
                                $"Node '{node.Id}' property '{pair.Key}' is {pair.Value.Type}, schema expects {field.Type}.");
                        }

                        switch (field.Kind)
                        {
                            case BtPropertyFieldKind.Enum:
                                if (!pair.Value.TryGetInt64(out var enumIndex)
                                    || enumIndex < 0
                                    || enumIndex >= field.Options.Count)
                                {
                                    errors.Add(
                                        $"Node '{node.Id}' property '{pair.Key}' has enum index out of range [0, {field.Options.Count - 1}].");
                                }
                                break;

                            case BtPropertyFieldKind.BlackboardKeyRef:
                                if (pair.Value.TryGetString(out var keyName)
                                    && keyName.Length > 0
                                    && !definition.Blackboard.TryGetType(keyName, out _))
                                {
                                    errors.Add(
                                        $"Node '{node.Id}' property '{pair.Key}' references undeclared blackboard key '{keyName}'.");
                                }
                                break;
                        }
                        break;
                    }
                    if (!matched)
                    {
                        errors.Add($"Node '{node.Id}' has unknown property '{pair.Key}' for type '{node.Type}'.");
                    }
                }

                if (descriptor.Kind == BtNodeKind.Composite && node.Properties.TryGet(BtCompositeNode.AbortTypeProperty, out var abortValue))
                {
                    if (!abortValue.TryGetInt64(out var abort) || abort is < 0 or > (long)BtAbortType.Both)
                    {
                        errors.Add($"Node '{node.Id}' has invalid abortType value.");
                    }
                }

                foreach (var keyRef in descriptor.BlackboardKeys)
                {
                    if (!definition.Blackboard.TryGetType(keyRef.Key, out var actual) || actual != keyRef.Type)
                    {
                        errors.Add(
                            $"Node type '{descriptor.TypeId}' requires blackboard key '{keyRef.Key}' of type {keyRef.Type}; tree declares {(definition.Blackboard.TryGetType(keyRef.Key, out var declared) ? declared.ToString() : "nothing")}.");
                    }
                }
            }

            // 结构：单父、无环、可达
            var inDegree = new Dictionary<string, int>();
            foreach (var node in definition.Nodes)
            {
                if (!string.IsNullOrEmpty(node.Id) && !inDegree.ContainsKey(node.Id)) inDegree[node.Id] = 0;
            }
            foreach (var node in definition.Nodes)
            {
                if (string.IsNullOrEmpty(node.Id)) continue;
                foreach (var childId in node.ChildIds)
                {
                    if (!inDegree.ContainsKey(childId)) continue;
                    inDegree[childId]++;
                    if (inDegree[childId] > 1)
                    {
                        errors.Add($"Node '{childId}' has multiple parents.");
                    }
                }
            }

            if (root != null)
            {
                var visited = new HashSet<string>();
                var visiting = new HashSet<string>();
                Visit(root, visited, visiting, byId, errors);
                foreach (var node in definition.Nodes)
                {
                    if (string.IsNullOrEmpty(node.Id)) continue;
                    if (!visited.Contains(node.Id))
                    {
                        errors.Add($"Node '{node.Id}' is unreachable from the root.");
                    }
                }
            }

            return errors;
        }

        private static void Visit(
            BtNodeDefinition node,
            HashSet<string> visited,
            HashSet<string> visiting,
            Dictionary<string, BtNodeDefinition> byId,
            List<string> errors)
        {
            visiting.Add(node.Id);
            foreach (var childId in node.ChildIds)
            {
                if (!byId.TryGetValue(childId, out var child)) continue;
                if (visiting.Contains(childId))
                {
                    errors.Add($"Cycle detected at node '{childId}'.");
                    continue;
                }
                if (visited.Contains(childId)) continue;
                Visit(child, visited, visiting, byId, errors);
            }
            visiting.Remove(node.Id);
            visited.Add(node.Id);
        }
    }
}
