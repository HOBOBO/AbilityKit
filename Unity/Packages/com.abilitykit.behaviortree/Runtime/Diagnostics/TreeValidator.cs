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
                Add("BT0001", "行为树定义为空。");
                return diagnostics;
            }
            if (registry == null)
            {
                Add("BT0002", "节点注册表为空。");
                return diagnostics;
            }

            if (definition.FormatVersion != TreeDefinition.CurrentFormatVersion)
            {
                Add("BT0100", $"不支持格式版本 {definition.FormatVersion}，期望版本为 {TreeDefinition.CurrentFormatVersion}。");
            }
            if (string.IsNullOrEmpty(definition.TreeId))
            {
                Add("BT0101", "Tree ID 不能为空。");
            }
            if (string.IsNullOrEmpty(definition.RootNodeId))
            {
                Add("BT0102", "根节点 ID 不能为空。");
            }
            if (definition.Nodes.Count == 0)
            {
                Add("BT0103", "行为树至少需要一个节点。");
                return diagnostics;
            }

            var byId = new Dictionary<string, NodeDefinition>(definition.Nodes.Count, StringComparer.Ordinal);
            NodeDefinition? root = null;
            foreach (var node in definition.Nodes)
            {
                if (string.IsNullOrEmpty(node.Id))
                {
                    Add("BT0200", "节点 ID 不能为空。");
                    continue;
                }
                if (!byId.TryAdd(node.Id, node))
                {
                    Add("BT0201", $"节点 ID '{node.Id}' 重复。", node.Id);
                }
                if (string.Equals(node.Id, definition.RootNodeId, StringComparison.Ordinal))
                {
                    root = node;
                }
            }

            if (root == null)
            {
                Add("BT0202", $"找不到根节点 '{definition.RootNodeId}'。", definition.RootNodeId);
            }

            var blackboardKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in definition.Blackboard.Keys)
            {
                if (string.IsNullOrEmpty(key.Name))
                {
                    Add("BT0300", "黑板键名称不能为空。");
                    continue;
                }
                if (!blackboardKeys.Add(key.Name))
                {
                    Add("BT0301", $"黑板键 '{key.Name}' 重复。", blackboardKey: key.Name);
                }
                if (key.Default != null && key.Default.Type != key.Type)
                {
                    Add("BT0302", $"黑板键 '{key.Name}' 的默认值类型 {key.Default.Type} 与声明类型 {key.Type} 不一致。", blackboardKey: key.Name);
                }
            }

            foreach (var node in definition.Nodes)
            {
                if (string.IsNullOrEmpty(node.Id)) continue;
                if (!registry.TryGetDescriptor(node.Type, out var descriptor))
                {
                    Add("BT0400", $"节点 '{node.Id}' 引用了未知类型 '{node.Type}'。", node.Id);
                    continue;
                }

                if (node.SubtreeBlackboard != null)
                {
                    if (!string.Equals(node.Type, AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.Subtree, StringComparison.Ordinal))
                    {
                        Add("BT0550", $"节点 '{node.Id}' 不是子树节点，不能配置子树黑板。", node.Id);
                    }
                    else if (node.SubtreeBlackboard.Bindings == null)
                    {
                        Add("BT0551", $"子树节点 '{node.Id}' 的黑板绑定列表无效。", node.Id);
                    }
                    else
                    {
                        var boundChildKeys = new HashSet<string>(StringComparer.Ordinal);
                        foreach (var binding in node.SubtreeBlackboard.Bindings)
                        {
                            if (binding == null
                                || string.IsNullOrEmpty(binding.SubtreeKey)
                                || string.IsNullOrEmpty(binding.ParentKey))
                            {
                                Add("BT0552", $"子树节点 '{node.Id}' 包含未填写完整的黑板绑定。", node.Id);
                                continue;
                            }
                            if (!boundChildKeys.Add(binding.SubtreeKey))
                            {
                                Add("BT0553", $"子树节点 '{node.Id}' 重复绑定了子树键 '{binding.SubtreeKey}'。", node.Id);
                            }
                            if (!definition.Blackboard.TryGetType(binding.ParentKey, out _))
                            {
                                Add(
                                    "BT0554",
                                    $"子树节点 '{node.Id}' 的绑定引用了未声明的父树键 '{binding.ParentKey}'。",
                                    node.Id,
                                    blackboardKey: binding.ParentKey);
                            }
                        }
                    }
                }

                var childCount = node.ChildIds.Count;
                if (childCount < descriptor.MinChildren
                    || (descriptor.MaxChildren >= 0 && childCount > descriptor.MaxChildren))
                {
                    Add(
                        "BT0401",
                        $"节点 '{node.Id}'（{descriptor.TypeId}）有 {childCount} 个子节点，允许范围为 [{descriptor.MinChildren}, {(descriptor.MaxChildren < 0 ? "无限制" : descriptor.MaxChildren.ToString())}]。",
                        node.Id);
                }

                foreach (var childId in node.ChildIds)
                {
                    if (!byId.ContainsKey(childId))
                    {
                        Add("BT0402", $"节点 '{node.Id}' 引用了不存在的子节点 '{childId}'。", node.Id);
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
                                $"节点 '{node.Id}' 的属性 '{pair.Key}' 类型为 {pair.Value.Type}，属性定义要求 {field.Type}。",
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
                                        $"节点 '{node.Id}' 的属性 '{pair.Key}' 枚举索引超出范围 [0, {field.Options.Count - 1}]。",
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
                                        $"节点 '{node.Id}' 的属性 '{pair.Key}' 引用了未声明的黑板键 '{keyName}'。",
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
                        Add("BT0503", $"节点 '{node.Id}' 包含类型 '{node.Type}' 未定义的属性 '{pair.Key}'。", node.Id, pair.Key);
                    }
                }

                if (descriptor.Kind == NodeKind.Composite && node.Properties.TryGet(AbilityKit.BehaviorTree.Nodes.CompositeNode.AbortTypeProperty, out var abortValue))
                {
                    if (!abortValue.TryGetInt64(out var abort) || abort is < 0 or > (long)AbortType.Both)
                    {
                        Add("BT0504", $"节点 '{node.Id}' 的中止类型值无效。", node.Id, AbilityKit.BehaviorTree.Nodes.CompositeNode.AbortTypeProperty);
                    }
                }

                foreach (var keyRef in descriptor.BlackboardKeys)
                {
                    if (!definition.Blackboard.TryGetType(keyRef.Key, out var actual) || actual != keyRef.Type)
                    {
                        Add(
                            "BT0600",
                            $"节点类型 '{descriptor.TypeId}' 需要类型为 {keyRef.Type} 的黑板键 '{keyRef.Key}'，行为树中声明的是 {(definition.Blackboard.TryGetType(keyRef.Key, out var declared) ? declared.ToString() : "未声明")}。",
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
                        Add("BT0700", $"节点 '{childId}' 存在多个父节点。", childId);
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
                        Add("BT0702", $"无法从根节点到达节点 '{node.Id}'。", node.Id);
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
                        $"在节点 '{childId}' 处检测到循环连接。",
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
