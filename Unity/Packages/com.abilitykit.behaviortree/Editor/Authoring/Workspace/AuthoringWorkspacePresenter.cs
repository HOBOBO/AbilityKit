#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring;

using AbilityKit.BehaviorTree.Editor;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
namespace AbilityKit.BehaviorTree.Editor.Authoring.Workspace
{
    internal sealed class AuthoringWorkspacePresenter
    {
        private readonly AuthoringWorkspaceController _workspace;
        private readonly Func<NodeRegistry> _registry;
        private readonly IAuthoringClipboardAdapter _clipboard;

        public AuthoringWorkspacePresenter(
            AuthoringWorkspaceController workspace,
            Func<NodeRegistry>? registry = null,
            IAuthoringClipboardAdapter? clipboard = null)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _registry = registry ?? (() => EditorNodeCatalog.Registry);
            _clipboard = clipboard ?? AuthoringClipboardAdapter.Unavailable;
        }

        public AuthoringSourceDocument Document => _workspace.Document;
        public AuthoringWorkspaceController Workspace => _workspace;
        public IAuthoringClipboardAdapter Clipboard => _clipboard;

        public string ResolveNodeDisplayName(NodeDefinition node)
        {
            if (node == null) return string.Empty;
            if (Document.TryGetNodeMetadata(node.Id, out var metadata)
                && !string.IsNullOrWhiteSpace(metadata.DisplayName))
            {
                return metadata.DisplayName;
            }

            return _registry().TryGetDescriptor(node.Type, out var descriptor)
                ? descriptor.DisplayName
                : node.Type;
        }

        public int ResolveChildOrder(string nodeId)
        {
            foreach (var parent in Document.Tree.Nodes)
            {
                var index = parent.ChildIds.IndexOf(nodeId);
                if (index >= 0) return index + 1;
            }
            return 0;
        }

        public bool CanConnect(string childId, string parentId, out string error)
        {
            if (!_registry().TryGetDescriptor(
                    Document.Tree.Nodes.Find(n => n.Id == parentId)?.Type ?? string.Empty,
                    out var descriptor))
            {
                error = $"Parent node '{parentId}' type is not registered.";
                return false;
            }

            return GraphOperations.CanConnect(
                Document.Tree,
                parentId,
                childId,
                descriptor.MaxChildren,
                out error);
        }

        public NodeDefinition? AddNode(NodeDescriptor descriptor, string nodeId, float x, float y)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (string.IsNullOrWhiteSpace(nodeId)) throw new ArgumentException("Node id is required.", nameof(nodeId));
            if (_workspace.IsReadOnly) return null;

            var node = new NodeDefinition
            {
                Id = nodeId,
                Type = descriptor.TypeId,
            };
            foreach (var field in descriptor.PropertySchema)
            {
                if (field.Default != null)
                    node.Properties.Set(field.Name, AuthoringMutationService.CloneValue(field.Default));
            }

            var changed = _workspace.Mutate(document =>
            {
                document.Tree.Nodes.Add(node);
                document.NodeMetadata.Add(new AuthoringNodeMetadata
                {
                    NodeId = nodeId,
                    DisplayName = descriptor.DisplayName,
                });
                document.Layout.Add(new NodeLayoutData { NodeId = nodeId, X = x, Y = y });
            });
            return changed ? node : null;
        }

        public bool ApplyLayout(
            AuthoringLayoutOptions options,
            IReadOnlyDictionary<string, AuthoringLayoutSize>? nodeSizes,
            out AuthoringLayoutResult result)
        {
            result = new AuthoringLayoutResult();
            if (_workspace.IsReadOnly || Document.Tree.Nodes.Count == 0) return false;

            var before = AuthoringJson.Save(Document);
            if (!AuthoringLayoutUtility.ApplyLayout(Document, options, nodeSizes, out result))
                return false;

            _workspace.RecordExternalMutation(before);
            return true;
        }

        public AuthoringSearchResult SearchNodes(string query, int maxResults = 64)
        {
            query ??= string.Empty;
            maxResults = Math.Max(1, maxResults);
            var hits = new List<AuthoringNodeSearchHit>();
            var parented = BuildParentedSet(Document);

            foreach (var node in Document.Tree.Nodes)
            {
                var descriptor = _registry().TryGetDescriptor(node.Type, out var found) ? found : null;
                var displayName = ResolveNodeDisplayName(node);
                var category = descriptor?.Category ?? string.Empty;
                if (!Matches(query, node.Id, node.Type, displayName, category)) continue;

                hits.Add(new AuthoringNodeSearchHit(
                    node.Id,
                    displayName,
                    node.Type,
                    category,
                    string.Equals(node.Id, Document.Tree.RootNodeId, StringComparison.Ordinal),
                    !string.Equals(node.Id, Document.Tree.RootNodeId, StringComparison.Ordinal)
                    && !parented.Contains(node.Id)));
                if (hits.Count >= maxResults) break;
            }

            return new AuthoringSearchResult(query, hits, Document.Tree.Nodes.Count);
        }

        public AuthoringOverviewModel BuildOverview(string query = "", int maxEntries = 24)
        {
            var document = Document;
            var parented = BuildParentedSet(document);
            var root = document.Tree.Nodes.Find(node =>
                string.Equals(node.Id, document.Tree.RootNodeId, StringComparison.Ordinal));
            var orphanIds = new List<string>();
            var subtreeRefs = new List<string>();
            var edgeCount = 0;

            foreach (var node in document.Tree.Nodes)
            {
                edgeCount += node.ChildIds.Count;
                if (!string.Equals(node.Id, document.Tree.RootNodeId, StringComparison.Ordinal)
                    && !parented.Contains(node.Id))
                {
                    orphanIds.Add(node.Id);
                }

                if (string.Equals(node.Type, BuiltInNodeTypes.Subtree, StringComparison.Ordinal)
                    && node.Properties.TryGet(SubtreeNode.TreeIdProperty, out var treeId)
                    && treeId.TryGetString(out var refTreeId)
                    && !string.IsNullOrWhiteSpace(refTreeId))
                {
                    subtreeRefs.Add(refTreeId);
                }
            }

            return new AuthoringOverviewModel(
                document.Tree.Nodes.Count,
                edgeCount,
                document.Groups.Count,
                document.Notes.Count,
                document.Tree.Blackboard.Keys.Count,
                root?.Id ?? string.Empty,
                root == null ? string.Empty : ResolveNodeDisplayName(root),
                orphanIds,
                subtreeRefs,
                SearchNodes(query, maxEntries),
                _workspace.Diagnostics.ErrorCount,
                _clipboard.IsAvailable);
        }

        private static HashSet<string> BuildParentedSet(AuthoringSourceDocument document)
        {
            var parented = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in document.Tree.Nodes)
            {
                foreach (var childId in node.ChildIds)
                {
                    if (!string.IsNullOrWhiteSpace(childId)) parented.Add(childId);
                }
            }
            return parented;
        }

        private static bool Matches(
            string query,
            string nodeId,
            string typeId,
            string displayName,
            string category)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            return Contains(nodeId, query)
                   || Contains(typeId, query)
                   || Contains(displayName, query)
                   || Contains(category, query);
        }

        private static bool Contains(string value, string query)
            => value?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

}
