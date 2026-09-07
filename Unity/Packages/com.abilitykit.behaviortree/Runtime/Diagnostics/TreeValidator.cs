using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree.Diagnostics
{
    using AbilityKit.BehaviorTree.Blackboard;
    using AbilityKit.BehaviorTree.Definition;
    using AbilityKit.BehaviorTree.Execution;
    using AbilityKit.BehaviorTree.Registry;

    public static class TreeValidator
    {
        public static List<string> Validate(TreeDefinition definition, NodeRegistry registry)
        {
            var diagnostics = ValidateDiagnostics(definition, registry);
            var errors = new List<string>(diagnostics.Count);
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Severity == ValidationSeverity.Error)
                {
                    errors.Add(diagnostic.Message);
                }
            }
            return errors;
        }

        public static List<ValidationDiagnostic> ValidateDiagnostics(TreeDefinition definition, NodeRegistry registry)
        {
            var diagnostics = new List<ValidationDiagnostic>();
            void Add(string code, string message, string? nodeId = null, string? propertyName = null, string? blackboardKey = null)
            {
                diagnostics.Add(new ValidationDiagnostic(
                    code,
                    ValidationSeverity.Error,
                    message,
                    nodeId,
                    propertyName,
                    blackboardKey));
            }

            if (definition == null)
            {
                Add("BT0001", "Tree definition is null.");
                return diagnostics;
            }
            if (registry == null)
            {
                Add("BT0002", "Node registry is null.");
                return diagnostics;
            }

            if (definition.FormatVersion != TreeDefinition.CurrentFormatVersion)
            {
                Add("BT0100", $"Unsupported format version {definition.FormatVersion} (expected {TreeDefinition.CurrentFormatVersion}).");
            }
            if (string.IsNullOrEmpty(definition.TreeId))
            {
                Add("BT0101", "TreeId must not be empty.");
            }
            if (string.IsNullOrEmpty(definition.RootNodeId))
            {
                Add("BT0102", "RootNodeId must not be empty.");
            }
            if (definition.Nodes.Count == 0)
            {
                Add("BT0103", "Tree must contain at least one node.");
                return diagnostics;
            }

            var byId = new Dictionary<string, NodeDefinition>(definition.Nodes.Count, StringComparer.Ordinal);
            NodeDefinition? root = null;
            foreach (var node in definition.Nodes)
            {
                if (string.IsNullOrEmpty(node.Id))
                {
                    Add("BT0200", "Node id must not be empty.");
                    continue;
                }
                if (!byId.TryAdd(node.Id, node))
                {
                    Add("BT0201", $"Node id '{node.Id}' is duplicated.", node.Id);
                }
                if (string.Equals(node.Id, definition.RootNodeId, StringComparison.Ordinal))
                {
                    root = node;
                }
            }

            if (root == null)
            {
                Add("BT0202", $"Root node '{definition.RootNodeId}' not found.", definition.RootNodeId);
            }

            var blackboardKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in definition.Blackboard.Keys)
            {
                if (string.IsNullOrEmpty(key.Name))
                {
                    Add("BT0300", "Blackboard key name must not be empty.");
                    continue;
                }
                if (!blackboardKeys.Add(key.Name))
                {
                    Add("BT0301", $"Blackboard key '{key.Name}' is duplicated.", blackboardKey: key.Name);
                }
                if (key.Default != null && key.Default.Type != key.Type)
                {
                    Add("BT0302", $"Blackboard key '{key.Name}' default value type {key.Default.Type} does not match declared {key.Type}.", blackboardKey: key.Name);
                }
            }

            foreach (var node in definition.Nodes)
            {
                if (string.IsNullOrEmpty(node.Id)) continue;
                if (!registry.TryGetDescriptor(node.Type, out var descriptor))
                {
                    Add("BT0400", $"Node '{node.Id}' references unknown type '{node.Type}'.", node.Id);
                    continue;
                }

                var childCount = node.ChildIds.Count;
                if (childCount < descriptor.MinChildren
                    || (descriptor.MaxChildren >= 0 && childCount > descriptor.MaxChildren))
                {
                    Add(
                        "BT0401",
                        $"Node '{node.Id}' ({descriptor.TypeId}) has {childCount} children; allowed [{descriptor.MinChildren}, {(descriptor.MaxChildren < 0 ? "unbounded" : descriptor.MaxChildren.ToString())}].",
                        node.Id);
                }

                foreach (var childId in node.ChildIds)
                {
                    if (!byId.ContainsKey(childId))
                    {
                        Add("BT0402", $"Node '{node.Id}' references missing child '{childId}'.", node.Id);
                    }
                }

                foreach (var pair in node.Properties.Values)
                {
                    var matched = false;
                    foreach (var field in descriptor.PropertySchema)
                    {
                        if (!string.Equals(field.Name, pair.Key, StringComparison.Ordinal)) continue;
                        matched = true;
                        if (pair.Value.Type != field.Type)
                        {
                            Add(
                                "BT0500",
                                $"Node '{node.Id}' property '{pair.Key}' is {pair.Value.Type}, schema expects {field.Type}.",
                                node.Id,
                                pair.Key);
                        }

                        switch (field.Kind)
                        {
                            case PropertyFieldKind.Enum:
                                if (!pair.Value.TryGetInt64(out var enumIndex)
                                    || enumIndex < 0
                                    || enumIndex >= field.Options.Count)
                                {
                                    Add(
                                        "BT0501",
                                        $"Node '{node.Id}' property '{pair.Key}' has enum index out of range [0, {field.Options.Count - 1}].",
                                        node.Id,
                                        pair.Key);
                                }
                                break;

                            case PropertyFieldKind.BlackboardKeyRef:
                                if (pair.Value.TryGetString(out var keyName)
                                    && keyName.Length > 0
                                    && !definition.Blackboard.TryGetType(keyName, out _))
                                {
                                    Add(
                                        "BT0502",
                                        $"Node '{node.Id}' property '{pair.Key}' references undeclared blackboard key '{keyName}'.",
                                        node.Id,
                                        pair.Key,
                                        keyName);
                                }
                                break;
                        }
                        break;
                    }
                    if (!matched)
                    {
                        Add("BT0503", $"Node '{node.Id}' has unknown property '{pair.Key}' for type '{node.Type}'.", node.Id, pair.Key);
                    }
                }

                if (descriptor.Kind == NodeKind.Composite && node.Properties.TryGet(AbilityKit.BehaviorTree.Nodes.CompositeNode.AbortTypeProperty, out var abortValue))
                {
                    if (!abortValue.TryGetInt64(out var abort) || abort is < 0 or > (long)AbortType.Both)
                    {
                        Add("BT0504", $"Node '{node.Id}' has invalid abortType value.", node.Id, AbilityKit.BehaviorTree.Nodes.CompositeNode.AbortTypeProperty);
                    }
                }

                foreach (var keyRef in descriptor.BlackboardKeys)
                {
                    if (!definition.Blackboard.TryGetType(keyRef.Key, out var actual) || actual != keyRef.Type)
                    {
                        Add(
                            "BT0600",
                            $"Node type '{descriptor.TypeId}' requires blackboard key '{keyRef.Key}' of type {keyRef.Type}; tree declares {(definition.Blackboard.TryGetType(keyRef.Key, out var declared) ? declared.ToString() : "nothing")}.",
                            node.Id,
                            blackboardKey: keyRef.Key);
                    }
                }
            }

            var inDegree = new Dictionary<string, int>(StringComparer.Ordinal);
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
                        Add("BT0700", $"Node '{childId}' has multiple parents.", childId);
                    }
                }
            }

            if (root != null)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal);
                var visiting = new HashSet<string>(StringComparer.Ordinal);
                Visit(root, visited, visiting, byId, diagnostics);
                foreach (var node in definition.Nodes)
                {
                    if (string.IsNullOrEmpty(node.Id)) continue;
                    if (!visited.Contains(node.Id))
                    {
                        Add("BT0702", $"Node '{node.Id}' is unreachable from the root.", node.Id);
                    }
                }
            }

            return diagnostics;
        }

        private static void Visit(
            NodeDefinition node,
            HashSet<string> visited,
            HashSet<string> visiting,
            Dictionary<string, NodeDefinition> byId,
            List<ValidationDiagnostic> diagnostics)
        {
            visiting.Add(node.Id);
            foreach (var childId in node.ChildIds)
            {
                if (!byId.TryGetValue(childId, out var child)) continue;
                if (visiting.Contains(childId))
                {
                    diagnostics.Add(new ValidationDiagnostic(
                        "BT0701",
                        ValidationSeverity.Error,
                        $"Cycle detected at node '{childId}'.",
                        childId));
                    continue;
                }
                if (visited.Contains(childId)) continue;
                Visit(child, visited, visiting, byId, diagnostics);
            }
            visiting.Remove(node.Id);
            visited.Add(node.Id);
        }
    }
}
