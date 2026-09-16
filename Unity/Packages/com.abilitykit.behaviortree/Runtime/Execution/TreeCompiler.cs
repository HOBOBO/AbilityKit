using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;

namespace AbilityKit.BehaviorTree.Execution
{
    /// <summary>按 treeId 解析行为树定义。</summary>
    public interface TreeDefinitionResolver
    {
        bool TryResolve(string treeId, out TreeDefinition definition);
    }

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

    public sealed class SubtreeExpansionException : InvalidOperationException
    {
        public string SourceTreeId { get; }
        public string ReferenceNodeId { get; }

        public SubtreeExpansionException(string sourceTreeId, string referenceNodeId, Exception inner)
            : base($"行为树 '{sourceTreeId}' 的子树节点 '{referenceNodeId}' 展开失败：{inner.Message}", inner)
        {
            SourceTreeId = sourceTreeId;
            ReferenceNodeId = referenceNodeId;
        }
    }

    /// <summary>把子树引用递归展开为单棵运行时树，并处理实例级黑板作用域。</summary>
    public static class TreeCompiler
    {
        /// <summary>兼容旧调用；仅支持共享黑板、不需要键重写的子树。</summary>
        public static ExpansionResult ExpandReferences(
            TreeDefinition definition,
            TreeDefinitionResolver resolver)
            => ExpandReferencesCore(definition, resolver, null);

        /// <summary>展开子树，并依据节点属性 Schema 安全重写黑板键引用。</summary>
        public static ExpansionResult ExpandReferences(
            TreeDefinition definition,
            TreeDefinitionResolver resolver,
            NodeRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            return ExpandReferencesCore(definition, resolver, registry);
        }

        private static ExpansionResult ExpandReferencesCore(
            TreeDefinition definition,
            TreeDefinitionResolver resolver,
            NodeRegistry? registry)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            var result = new TreeDefinition
            {
                TreeId = definition.TreeId,
                FormatVersion = definition.FormatVersion,
                Blackboard = new BlackboardSchema(),
            };
            var blackboardByName = new Dictionary<string, BlackboardKeyDefinition>(StringComparer.Ordinal);
            var rootKeyMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in definition.Blackboard.Keys)
            {
                rootKeyMap[key.Name] = key.Name;
                AddBlackboardKey(result.Blackboard, blackboardByName, key.Name, key);
            }

            var provenance = new Dictionary<string, string>(StringComparer.Ordinal);
            var sourceNodes = new Dictionary<string, string>(StringComparer.Ordinal);
            var subtreeInstances = new List<SubtreeInstance>();
            var visiting = new HashSet<string>(StringComparer.Ordinal) { definition.TreeId };
            result.RootNodeId = ExpandSubtree(
                definition.TreeId, definition.RootNodeId, "", definition, resolver, registry,
                rootKeyMap, result, blackboardByName, provenance, sourceNodes,
                subtreeInstances, visiting);

            return new ExpansionResult(result, provenance, sourceNodes, subtreeInstances);
        }

        private static string ExpandSubtree(
            string sourceTreeId,
            string sourceNodeId,
            string idPrefix,
            TreeDefinition sourceTree,
            TreeDefinitionResolver resolver,
            NodeRegistry? registry,
            IReadOnlyDictionary<string, string> keyMap,
            TreeDefinition result,
            Dictionary<string, BlackboardKeyDefinition> blackboardByName,
            Dictionary<string, string> provenance,
            Dictionary<string, string> sourceNodes,
            List<SubtreeInstance> subtreeInstances,
            HashSet<string> visiting)
        {
            var sourceNode = FindNode(sourceTree, sourceNodeId)
                ?? throw new InvalidOperationException(
                    $"展开子树时，行为树 '{sourceTreeId}' 中不存在节点 '{sourceNodeId}'。");

            if (sourceNode.Type == BuiltInNodeTypes.Subtree)
            {
                try
                {
                    var refTreeId = ReadReferencedTreeId(sourceNode, sourceTreeId);
                    if (!resolver.TryResolve(refTreeId, out var refTree))
                        throw new InvalidOperationException(
                            $"子树节点 '{sourceNode.Id}' 引用了不存在的行为树 '{refTreeId}'。");
                    if (!visiting.Add(refTree.TreeId))
                        throw new InvalidOperationException(
                            $"检测到子树循环引用，涉及行为树 '{refTree.TreeId}'。");

                    var childPrefix = idPrefix.Length == 0
                        ? sourceNode.Id
                        : idPrefix + "." + sourceNode.Id;
                    var childKeyMap = BuildChildKeyMap(
                        sourceNode, sourceTree, refTree, childPrefix, keyMap, registry,
                        result.Blackboard, blackboardByName);
                    var expandedRootId = ExpandSubtree(
                        refTree.TreeId, refTree.RootNodeId, childPrefix, refTree, resolver, registry,
                        childKeyMap, result, blackboardByName, provenance, sourceNodes,
                        subtreeInstances, visiting);
                    visiting.Remove(refTree.TreeId);
                    subtreeInstances.Add(new SubtreeInstance(expandedRootId, refTree.TreeId));
                    return expandedRootId;
                }
                catch (SubtreeExpansionException ex) when (ex.SourceTreeId == sourceTreeId
                    && ex.ReferenceNodeId == sourceNode.Id)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new SubtreeExpansionException(sourceTreeId, sourceNode.Id, ex);
                }
            }

            var newId = idPrefix.Length == 0 ? sourceNode.Id : idPrefix + "." + sourceNode.Id;
            var newNode = new NodeDefinition
            {
                Id = newId,
                Type = sourceNode.Type,
                Properties = CloneAndRewriteProperties(sourceNode, keyMap, registry),
            };
            foreach (var childId in sourceNode.ChildIds)
            {
                newNode.ChildIds.Add(ExpandSubtree(
                    sourceTreeId, childId, idPrefix, sourceTree, resolver, registry, keyMap,
                    result, blackboardByName, provenance, sourceNodes, subtreeInstances, visiting));
            }

            result.Nodes.Add(newNode);
            provenance[newId] = sourceTreeId;
            sourceNodes[newId] = sourceNode.Id;
            return newId;
        }

        private static Dictionary<string, string> BuildChildKeyMap(
            NodeDefinition subtreeNode,
            TreeDefinition parentTree,
            TreeDefinition childTree,
            string childPrefix,
            IReadOnlyDictionary<string, string> parentKeyMap,
            NodeRegistry? registry,
            BlackboardSchema resultSchema,
            Dictionary<string, BlackboardKeyDefinition> blackboardByName)
        {
            var configuration = subtreeNode.SubtreeBlackboard;
            if (configuration != null && registry == null)
                throw new InvalidOperationException(
                    $"子树节点 '{subtreeNode.Id}' 需要 NodeRegistry 才能编译黑板绑定。");

            var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
            if (configuration != null)
            {
                if (configuration.Bindings == null)
                    throw new InvalidOperationException(
                        $"子树节点 '{subtreeNode.Id}' 的黑板绑定列表无效。");
                foreach (var binding in configuration.Bindings)
                {
                    if (binding == null
                        || string.IsNullOrEmpty(binding.SubtreeKey)
                        || string.IsNullOrEmpty(binding.ParentKey))
                    {
                        throw new InvalidOperationException(
                            $"子树节点 '{subtreeNode.Id}' 包含空的黑板绑定。");
                    }
                    if (!bindings.TryAdd(binding.SubtreeKey, binding.ParentKey))
                        throw new InvalidOperationException(
                            $"子树节点 '{subtreeNode.Id}' 重复绑定了子树黑板键 '{binding.SubtreeKey}'。");
                }
            }

            var childKeyMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var childKey in childTree.Blackboard.Keys)
            {
                string runtimeKey;
                if (bindings.TryGetValue(childKey.Name, out var parentKey))
                {
                    var parentDefinition = FindBlackboardKey(parentTree.Blackboard, parentKey);
                    if (parentDefinition == null)
                        throw new InvalidOperationException(
                            $"子树节点 '{subtreeNode.Id}' 绑定了未声明的父树黑板键 '{parentKey}'。");
                    if (parentDefinition.Type != childKey.Type)
                        throw new InvalidOperationException(
                            $"子树黑板键 '{childKey.Name}' 与父树黑板键 '{parentKey}' 的类型不一致。");
                    if (!parentKeyMap.TryGetValue(parentKey, out runtimeKey!))
                        throw new InvalidOperationException(
                            $"父树黑板键 '{parentKey}' 没有运行时映射。");
                }
                else if (configuration?.IsolateUnmappedKeys == true)
                {
                    runtimeKey = childPrefix + "." + childKey.Name;
                    AddBlackboardKey(resultSchema, blackboardByName, runtimeKey, childKey);
                }
                else
                {
                    runtimeKey = parentKeyMap.TryGetValue(childKey.Name, out var inherited)
                        ? inherited
                        : childKey.Name;
                    AddBlackboardKey(resultSchema, blackboardByName, runtimeKey, childKey);
                }

                childKeyMap[childKey.Name] = runtimeKey;
            }

            foreach (var boundChildKey in bindings.Keys)
            {
                if (!childKeyMap.ContainsKey(boundChildKey))
                    throw new InvalidOperationException(
                        $"子树节点 '{subtreeNode.Id}' 绑定了未声明的子树黑板键 '{boundChildKey}'。");
            }
            return childKeyMap;
        }

        private static PropertyBag CloneAndRewriteProperties(
            NodeDefinition sourceNode,
            IReadOnlyDictionary<string, string> keyMap,
            NodeRegistry? registry)
        {
            var clone = new PropertyBag();
            foreach (var pair in sourceNode.Properties.Values)
                clone.Set(pair.Key, TreeDefinition.CloneValue(pair.Value));

            if (registry == null || !registry.TryGetDescriptor(sourceNode.Type, out var descriptor))
                return clone;

            foreach (var field in descriptor.PropertySchema)
            {
                if (field.Kind != PropertyFieldKind.BlackboardKeyRef
                    || !clone.TryGet(field.Name, out var value)
                    || !value.TryGetString(out var sourceKey)
                    || !keyMap.TryGetValue(sourceKey, out var runtimeKey)
                    || string.Equals(sourceKey, runtimeKey, StringComparison.Ordinal))
                {
                    continue;
                }
                clone.Set(field.Name, PropertyValue.Of(runtimeKey));
            }
            return clone;
        }

        private static void AddBlackboardKey(
            BlackboardSchema schema,
            Dictionary<string, BlackboardKeyDefinition> byName,
            string runtimeName,
            BlackboardKeyDefinition source)
        {
            if (byName.TryGetValue(runtimeName, out var existing))
            {
                if (existing.Type != source.Type)
                    throw new InvalidOperationException(
                        $"子树黑板键 '{runtimeName}' 类型冲突：{existing.Type} 与 {source.Type}。");
                if (!DefaultValuesEqual(existing, source))
                    throw new InvalidOperationException(
                        $"共享黑板键 '{runtimeName}' 的初始值冲突：已有 {existing.Default?.ToString() ?? "类型默认值"}，子树为 {source.Default?.ToString() ?? "类型默认值"}。请显式绑定或隔离该键。");
                return;
            }

            var clone = new BlackboardKeyDefinition
            {
                Name = runtimeName,
                Type = source.Type,
                Default = source.Default == null ? null : TreeDefinition.CloneValue(source.Default),
            };
            byName.Add(runtimeName, clone);
            schema.Keys.Add(clone);
        }

        private static bool DefaultValuesEqual(BlackboardKeyDefinition left, BlackboardKeyDefinition right)
        {
            return left.Type switch
            {
                ValueType.Bool => (left.Default?.BoolValue ?? false) == (right.Default?.BoolValue ?? false),
                ValueType.Int64 => (left.Default?.Int64Value ?? 0L) == (right.Default?.Int64Value ?? 0L),
                ValueType.Fixed64 => (left.Default?.Fixed64Raw ?? 0L) == (right.Default?.Fixed64Raw ?? 0L),
                ValueType.String => string.Equals(left.Default?.StringValue ?? "", right.Default?.StringValue ?? "", StringComparison.Ordinal),
                _ => true,
            };
        }

        private static string ReadReferencedTreeId(NodeDefinition node, string sourceTreeId)
        {
            if (!node.Properties.TryGet(SubtreeNode.TreeIdProperty, out var value)
                || !value.TryGetString(out var treeId)
                || string.IsNullOrEmpty(treeId))
            {
                throw new InvalidOperationException(
                    $"行为树 '{sourceTreeId}' 中的子树节点 '{node.Id}' 未配置 treeId。");
            }
            return treeId;
        }

        private static NodeDefinition? FindNode(TreeDefinition tree, string nodeId)
        {
            foreach (var node in tree.Nodes)
            {
                if (string.Equals(node.Id, nodeId, StringComparison.Ordinal)) return node;
            }
            return null;
        }

        private static BlackboardKeyDefinition? FindBlackboardKey(BlackboardSchema schema, string keyName)
        {
            foreach (var key in schema.Keys)
            {
                if (string.Equals(key.Name, keyName, StringComparison.Ordinal)) return key;
            }
            return null;
        }
    }
}
