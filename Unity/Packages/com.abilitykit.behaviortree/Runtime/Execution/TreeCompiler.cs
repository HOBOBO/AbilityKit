using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree.Nodes;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Execution
{
    /// <summary>树定义解析器：子树引用节点按 treeId 取被引用树定义（宿主实现，如从目资源加载�?/summary>
    public interface TreeDefinitionResolver
    {
        bool TryResolve(string treeId, out TreeDefinition definition);
    }

    /// <summary>一次子树内联的实例：内联根节点 id -> 被引treeId（观察端标记子树边界/跨树跳转�?/summary>
    public sealed class SubtreeInstance
    {
        public string InlinedRootNodeId { get; }
        public string ReferencedTreeId { get; }

        public SubtreeInstance(string inlinedRootNodeId, string referencedTreeId)
        {
            InlinedRootNodeId = inlinedRootNodeId;
            ReferencedTreeId = referencedTreeId;
        }
    }

    /// <summary>展开结果：自包含定义 + 每个展开节点的来源树/原始节点溯源 + 子树实例列表</summary>
    public sealed class ExpansionResult
    {
        public TreeDefinition Definition { get; }
        public IReadOnlyDictionary<string, string> NodeSourceTree { get; }
        public IReadOnlyDictionary<string, string> NodeSourceNode { get; }
        public IReadOnlyList<SubtreeInstance> SubtreeInstances { get; }

        public ExpansionResult(
            TreeDefinition definition,
            Dictionary<string, string> nodeSourceTree,
            Dictionary<string, string> nodeSourceNode,
            List<SubtreeInstance> subtreeInstances)
        {
            Definition = definition;
            NodeSourceTree = nodeSourceTree;
            NodeSourceNode = nodeSourceNode;
            SubtreeInstances = subtreeInstances;
        }
    }

    /// <summary>
    /// 子树引用展开：把 <see cref="SubtreeNode"/> 递归内联成自包含扁平�?   /// - 节点 id 加前缀（`subtreeNodeId.childId`），冲突天然避免    /// - 黑板 key 取各树并集，同名不同类型报错    /// - 环检测（同一条展开路径上重复引用同一 treeId）；
    /// - 未知 treeId 报错    /// 展开后运行时仍是单扁平树，快确定性语义不�?   /// </summary>
    public static class TreeCompiler
    {
        public static ExpansionResult ExpandReferences(
            TreeDefinition definition,
            TreeDefinitionResolver resolver)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            var result = new TreeDefinition
            {
                TreeId = definition.TreeId,
                FormatVersion = definition.FormatVersion,
                Blackboard = MergeBlackboard(definition, resolver, new HashSet<string>(StringComparer.Ordinal)),
            };

            var provenance = new Dictionary<string, string>(StringComparer.Ordinal);
            var sourceNodes = new Dictionary<string, string>(StringComparer.Ordinal);
            var subtreeInstances = new List<SubtreeInstance>();
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            result.RootNodeId = ExpandSubtree(
                definition.TreeId, definition.RootNodeId, "", definition, resolver, result,
                provenance, sourceNodes, subtreeInstances, visiting);

            return new ExpansionResult(result, provenance, sourceNodes, subtreeInstances);
        }

        private static string ExpandSubtree(
            string sourceTreeId,
            string sourceNodeId,
            string idPrefix,
            TreeDefinition sourceTree,
            TreeDefinitionResolver resolver,
            TreeDefinition result,
            Dictionary<string, string> provenance,
            Dictionary<string, string> sourceNodes,
            List<SubtreeInstance> subtreeInstances,
            HashSet<string> visiting)
        {
            var sourceNode = FindNode(sourceTree, sourceNodeId)
                ?? throw new InvalidOperationException(
                    $"Subtree expansion: node '{sourceNodeId}' not found in tree '{sourceTreeId}'.");

            // 子树引用节点：内联其引用树的根（沿同一展开路径环检测）
            if (sourceNode.Type == BuiltInNodeTypes.Subtree)
            {
                if (!sourceNode.Properties.TryGet(SubtreeNode.TreeIdProperty, out var treeIdValue)
                    || !treeIdValue.TryGetString(out var refTreeId)
                    || string.IsNullOrEmpty(refTreeId))
                {
                    throw new InvalidOperationException(
                        $"Subtree node '{sourceNode.Id}' in tree '{sourceTreeId}' has no treeId.");
                }
                if (!resolver.TryResolve(refTreeId, out var refTree))
                {
                    throw new InvalidOperationException(
                        $"Subtree node '{sourceNode.Id}' references unknown tree '{refTreeId}'.");
                }
                if (!visiting.Add(refTree.TreeId))
                {
                    throw new InvalidOperationException(
                        $"Subtree reference cycle detected at tree '{refTree.TreeId}'.");
                }

                var childPrefix = idPrefix.Length == 0
                    ? sourceNode.Id
                    : idPrefix + "." + sourceNode.Id;
                var expandedRootId = ExpandSubtree(
                    refTree.TreeId, refTree.RootNodeId, childPrefix, refTree, resolver, result,
                    provenance, sourceNodes, subtreeInstances, visiting);
                visiting.Remove(refTree.TreeId);
                subtreeInstances.Add(new SubtreeInstance(expandedRootId, refTree.TreeId));
                return expandedRootId;
            }

            // Copy ordinary nodes and recurse into their children.
            var newId = idPrefix.Length == 0 ? sourceNode.Id : idPrefix + "." + sourceNode.Id;
            var newNode = new NodeDefinition
            {
                Id = newId,
                Type = sourceNode.Type,
                Properties = CloneProperties(sourceNode.Properties),
            };
            foreach (var childId in sourceNode.ChildIds)
            {
                newNode.ChildIds.Add(ExpandSubtree(
                    sourceTreeId, childId, idPrefix, sourceTree, resolver, result,
                    provenance, sourceNodes, subtreeInstances, visiting));
            }

            result.Nodes.Add(newNode);
            provenance[newId] = sourceTreeId;
            sourceNodes[newId] = sourceNode.Id;
            return newId;
        }

        private static NodeDefinition? FindNode(TreeDefinition tree, string nodeId)
        {
            foreach (var node in tree.Nodes)
            {
                if (string.Equals(node.Id, nodeId, StringComparison.Ordinal)) return node;
            }
            return null;
        }

        private static PropertyBag CloneProperties(PropertyBag source)
        {
            var clone = new PropertyBag();
            foreach (var pair in source.Values)
            {
                clone.Set(pair.Key, TreeDefinition.CloneValue(pair.Value));
            }
            return clone;
        }

        private static BlackboardSchema MergeBlackboard(
            TreeDefinition root,
            TreeDefinitionResolver resolver,
            HashSet<string> visited)
        {
            var merged = new BlackboardSchema();
            var byName = new Dictionary<string, BlackboardKeyDefinition>(StringComparer.Ordinal);

            MergeInto(root, byName, resolver, visited);
            foreach (var pair in byName)
            {
                merged.Keys.Add(pair.Value);
            }
            return merged;
        }

        private static void MergeInto(
            TreeDefinition tree,
            Dictionary<string, BlackboardKeyDefinition> byName,
            TreeDefinitionResolver resolver,
            HashSet<string> visited)
        {
            if (!visited.Add(tree.TreeId)) return;

            foreach (var key in tree.Blackboard.Keys)
            {
                if (byName.TryGetValue(key.Name, out var existing))
                {
                    if (existing.Type != key.Type)
                    {
                        throw new InvalidOperationException(
                            $"Subtree blackboard key '{key.Name}' conflicts: {existing.Type} vs {key.Type}.");
                    }
                    continue;
                }
                byName[key.Name] = new BlackboardKeyDefinition
                {
                    Name = key.Name,
                    Type = key.Type,
                    Default = key.Default != null ? TreeDefinition.CloneValue(key.Default) : null,
                };
            }

            // 递归引用树的黑板（子树节点会引用其它树）
            foreach (var node in tree.Nodes)
            {
                if (node.Type != BuiltInNodeTypes.Subtree) continue;
                if (node.Properties.TryGet(SubtreeNode.TreeIdProperty, out var value)
                    && value.TryGetString(out var refTreeId)
                    && resolver.TryResolve(refTreeId, out var refTree))
                {
                    MergeInto(refTree, byName, resolver, visited);
                }
            }
        }
    }
}
