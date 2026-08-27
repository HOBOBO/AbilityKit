using System;
using System.Collections.Generic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>树定义解析器：子树引用节点按 treeId 取被引用树定义（宿主实现，如从目录/资源加载）。</summary>
    public interface IBtTreeDefinitionResolver
    {
        bool TryResolve(string treeId, out BtTreeDefinition definition);
    }

    /// <summary>一次子树内联的实例：内联根节点 id -> 被引用 treeId（观察端标记子树边界/跨树跳转）。</summary>
    public sealed class BtSubtreeInstance
    {
        public string InlinedRootNodeId { get; }
        public string ReferencedTreeId { get; }

        public BtSubtreeInstance(string inlinedRootNodeId, string referencedTreeId)
        {
            InlinedRootNodeId = inlinedRootNodeId;
            ReferencedTreeId = referencedTreeId;
        }
    }

    /// <summary>展开结果：自包含定义 + 每个展开节点 id 到来源 treeId 的溯源 + 子树实例列表。</summary>
    public sealed class BtExpansionResult
    {
        public BtTreeDefinition Definition { get; }
        public IReadOnlyDictionary<string, string> NodeSourceTree { get; }
        public IReadOnlyList<BtSubtreeInstance> SubtreeInstances { get; }

        public BtExpansionResult(
            BtTreeDefinition definition,
            Dictionary<string, string> nodeSourceTree,
            List<BtSubtreeInstance> subtreeInstances)
        {
            Definition = definition;
            NodeSourceTree = nodeSourceTree;
            SubtreeInstances = subtreeInstances;
        }
    }

    /// <summary>
    /// 子树引用展开：把 <see cref="BtSubtreeNode"/> 递归内联成自包含扁平树。
    /// - 节点 id 加前缀（`subtreeNodeId.childId`），冲突天然避免；
    /// - 黑板 key 取各树并集，同名不同类型报错；
    /// - 环检测（同一条展开路径上重复引用同一 treeId）；
    /// - 未知 treeId 报错。
    /// 展开后运行时仍是单扁平树，快照/确定性语义不变。
    /// </summary>
    public static class BtTreeCompiler
    {
        public static BtExpansionResult ExpandReferences(
            BtTreeDefinition definition,
            IBtTreeDefinitionResolver resolver)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            var result = new BtTreeDefinition
            {
                TreeId = definition.TreeId,
                FormatVersion = definition.FormatVersion,
                Blackboard = MergeBlackboard(definition, resolver, new HashSet<string>(StringComparer.Ordinal)),
            };

            var provenance = new Dictionary<string, string>(StringComparer.Ordinal);
            var subtreeInstances = new List<BtSubtreeInstance>();
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            result.RootNodeId = ExpandSubtree(
                definition.TreeId, definition.RootNodeId, "", definition, resolver, result, provenance, subtreeInstances, visiting);

            return new BtExpansionResult(result, provenance, subtreeInstances);
        }

        private static string ExpandSubtree(
            string sourceTreeId,
            string sourceNodeId,
            string idPrefix,
            BtTreeDefinition sourceTree,
            IBtTreeDefinitionResolver resolver,
            BtTreeDefinition result,
            Dictionary<string, string> provenance,
            List<BtSubtreeInstance> subtreeInstances,
            HashSet<string> visiting)
        {
            var sourceNode = FindNode(sourceTree, sourceNodeId)
                ?? throw new InvalidOperationException(
                    $"Subtree expansion: node '{sourceNodeId}' not found in tree '{sourceTreeId}'.");

            // 子树引用节点：内联其引用树的根（沿同一展开路径环检测）
            if (sourceNode.Type == BtBuiltInNodeTypes.Subtree)
            {
                if (!sourceNode.Properties.TryGet(BtSubtreeNode.TreeIdProperty, out var treeIdValue)
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
                    refTree.TreeId, refTree.RootNodeId, childPrefix, refTree, resolver, result, provenance, subtreeInstances, visiting);
                visiting.Remove(refTree.TreeId);
                subtreeInstances.Add(new BtSubtreeInstance(expandedRootId, refTree.TreeId));
                return expandedRootId;
            }

            // 普通节点：复制 + 递归子节点
            var newId = idPrefix.Length == 0 ? sourceNode.Id : idPrefix + "." + sourceNode.Id;
            var newNode = new BtNodeDefinition
            {
                Id = newId,
                Type = sourceNode.Type,
                Name = sourceNode.Name,
                Comment = sourceNode.Comment,
                Properties = CloneProperties(sourceNode.Properties),
            };
            foreach (var childId in sourceNode.ChildIds)
            {
                newNode.ChildIds.Add(ExpandSubtree(
                    sourceTreeId, childId, idPrefix, sourceTree, resolver, result, provenance, subtreeInstances, visiting));
            }

            result.Nodes.Add(newNode);
            provenance[newId] = sourceTreeId;
            return newId;
        }

        private static BtNodeDefinition? FindNode(BtTreeDefinition tree, string nodeId)
        {
            foreach (var node in tree.Nodes)
            {
                if (string.Equals(node.Id, nodeId, StringComparison.Ordinal)) return node;
            }
            return null;
        }

        private static BtPropertyBag CloneProperties(BtPropertyBag source)
        {
            var clone = new BtPropertyBag();
            foreach (var pair in source.Values)
            {
                clone.Set(pair.Key, CloneValue(pair.Value));
            }
            return clone;
        }

        private static BtPropertyValue CloneValue(BtPropertyValue value) => value.Type switch
        {
            BtValueType.Bool => BtPropertyValue.Of(value.BoolValue),
            BtValueType.Int64 => BtPropertyValue.Of(value.Int64Value),
            BtValueType.Fixed64 => BtPropertyValue.Of(Deterministic.Fixed64.FromRaw(value.Fixed64Raw)),
            BtValueType.String => BtPropertyValue.Of(value.StringValue),
            _ => BtPropertyValue.Of(value.Int64Value),
        };

        private static BtBlackboardSchema MergeBlackboard(
            BtTreeDefinition root,
            IBtTreeDefinitionResolver resolver,
            HashSet<string> visited)
        {
            var merged = new BtBlackboardSchema();
            var byName = new Dictionary<string, BtBlackboardKeyDefinition>(StringComparer.Ordinal);

            MergeInto(root, byName, resolver, visited);
            foreach (var pair in byName)
            {
                merged.Keys.Add(pair.Value);
            }
            return merged;
        }

        private static void MergeInto(
            BtTreeDefinition tree,
            Dictionary<string, BtBlackboardKeyDefinition> byName,
            IBtTreeDefinitionResolver resolver,
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
                byName[key.Name] = new BtBlackboardKeyDefinition
                {
                    Name = key.Name,
                    Type = key.Type,
                    Default = key.Default != null ? CloneValue(key.Default) : null,
                };
            }

            // 递归引用树的黑板（子树节点会引用其它树）
            foreach (var node in tree.Nodes)
            {
                if (node.Type != BtBuiltInNodeTypes.Subtree) continue;
                if (node.Properties.TryGet(BtSubtreeNode.TreeIdProperty, out var value)
                    && value.TryGetString(out var refTreeId)
                    && resolver.TryResolve(refTreeId, out var refTree))
                {
                    MergeInto(refTree, byName, resolver, visited);
                }
            }
        }
    }
}
