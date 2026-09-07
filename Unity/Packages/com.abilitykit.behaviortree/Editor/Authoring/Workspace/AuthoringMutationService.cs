#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Deterministic;
using UnityEngine;
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
    internal static class AuthoringMutationService
    {
        public const string ClipboardPrefix = "AbilityKit.BehaviorTree.Authoring.Subgraph:";

        public static string SerializeSubgraph(
            AuthoringSourceDocument document,
            IEnumerable<string>? nodeIds,
            IEnumerable<string>? groupIds,
            IEnumerable<string>? noteIds)
        {
            var clipboard = CreateSubgraphDocument(document, nodeIds, groupIds, noteIds);
            return clipboard.Tree.Nodes.Count == 0 && clipboard.Groups.Count == 0 && clipboard.Notes.Count == 0
                ? string.Empty
                : ClipboardPrefix + AuthoringJson.Save(clipboard);
        }

        public static bool TryDeserializeSubgraph(string serialized, out AuthoringSourceDocument clipboard)
        {
            clipboard = null!;
            if (string.IsNullOrWhiteSpace(serialized)) return false;
            if (!serialized.StartsWith(ClipboardPrefix, StringComparison.Ordinal)) return false;
            try
            {
                clipboard = AuthoringJson.Load(serialized.Substring(ClipboardPrefix.Length));
                return clipboard != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool CanDeserializeSubgraph(string serialized)
            => TryDeserializeSubgraph(serialized, out _);

        public static AuthoringClipboardPasteResult PasteSubgraph(
            AuthoringSourceDocument target,
            AuthoringSourceDocument clipboard,
            Vector2? targetOrigin = null)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (clipboard == null) throw new ArgumentNullException(nameof(clipboard));

            var result = new AuthoringClipboardPasteResult();
            var sourceNodeIds = clipboard.Tree.Nodes
                .Where(node => node != null && !string.IsNullOrWhiteSpace(node.Id))
                .Select(node => node.Id)
                .ToArray();
            if (sourceNodeIds.Length == 0 && clipboard.Groups.Count == 0 && clipboard.Notes.Count == 0)
                return result;

            var existingNodeIds = new HashSet<string>(
                target.Tree.Nodes.Select(node => node.Id),
                StringComparer.Ordinal);
            foreach (var sourceId in sourceNodeIds)
            {
                var createdId = UniqueId(existingNodeIds, sourceId + "_copy");
                result.NodeIdMap[sourceId] = createdId;
                result.CreatedNodeIds.Add(createdId);
            }

            var offset = ComputePasteOffset(clipboard, targetOrigin);
            var sourceSet = new HashSet<string>(sourceNodeIds, StringComparer.Ordinal);
            foreach (var source in clipboard.Tree.Nodes)
            {
                if (!result.NodeIdMap.TryGetValue(source.Id, out var newId)) continue;
                var node = CloneNode(source);
                node.Id = newId;
                node.ChildIds.Clear();
                foreach (var childId in source.ChildIds)
                {
                    if (sourceSet.Contains(childId) && result.NodeIdMap.TryGetValue(childId, out var newChildId))
                        node.ChildIds.Add(newChildId);
                }
                target.Tree.Nodes.Add(node);
            }

            foreach (var metadata in clipboard.NodeMetadata)
            {
                if (metadata == null || !result.NodeIdMap.TryGetValue(metadata.NodeId, out var nodeId)) continue;
                target.NodeMetadata.Add(new AuthoringNodeMetadata
                {
                    NodeId = nodeId,
                    DisplayName = metadata.DisplayName,
                    Comment = metadata.Comment,
                });
            }

            foreach (var layout in clipboard.Layout)
            {
                if (layout == null || !result.NodeIdMap.TryGetValue(layout.NodeId, out var nodeId)) continue;
                target.Layout.Add(new NodeLayoutData
                {
                    NodeId = nodeId,
                    X = layout.X + offset.x,
                    Y = layout.Y + offset.y,
                });
            }

            var existingGroupIds = new HashSet<string>(
                target.Groups.Select(group => group.Id),
                StringComparer.Ordinal);
            foreach (var group in clipboard.Groups)
            {
                if (group == null) continue;
                var mappedMembers = group.NodeIds
                    .Where(id => result.NodeIdMap.ContainsKey(id))
                    .Select(id => result.NodeIdMap[id])
                    .ToList();
                if (mappedMembers.Count == 0) continue;
                var groupId = UniqueId(existingGroupIds, string.IsNullOrWhiteSpace(group.Id) ? "group_copy" : group.Id + "_copy");
                result.CreatedGroupIds.Add(groupId);
                target.Groups.Add(new AuthoringGroupData
                {
                    Id = groupId,
                    Title = string.IsNullOrWhiteSpace(group.Title) ? "Group Copy" : group.Title,
                    X = group.X + offset.x,
                    Y = group.Y + offset.y,
                    Width = group.Width,
                    Height = group.Height,
                    NodeIds = mappedMembers,
                });
            }

            var existingNoteIds = new HashSet<string>(
                target.Notes.Select(note => note.Id),
                StringComparer.Ordinal);
            foreach (var note in clipboard.Notes)
            {
                if (note == null) continue;
                var noteId = UniqueId(existingNoteIds, string.IsNullOrWhiteSpace(note.Id) ? "note_copy" : note.Id + "_copy");
                result.CreatedNoteIds.Add(noteId);
                target.Notes.Add(new AuthoringNoteData
                {
                    Id = noteId,
                    Text = note.Text,
                    X = note.X + offset.x,
                    Y = note.Y + offset.y,
                    Width = note.Width,
                    Height = note.Height,
                });
            }

            if (string.IsNullOrWhiteSpace(target.Tree.RootNodeId) && result.CreatedNodeIds.Count > 0)
                target.Tree.RootNodeId = result.CreatedNodeIds[0];

            result.Changed = result.CreatedNodeIds.Count > 0
                || result.CreatedGroupIds.Count > 0
                || result.CreatedNoteIds.Count > 0;
            return result;
        }

        public static AuthoringDeleteImpact AnalyzeDelete(
            AuthoringSourceDocument document,
            IEnumerable<string>? nodeIds,
            IEnumerable<string>? groupIds,
            IEnumerable<string>? noteIds)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var nodes = OrderedDistinct(nodeIds);
            var groups = OrderedDistinct(groupIds);
            var notes = OrderedDistinct(noteIds);
            var nodeSet = new HashSet<string>(nodes, StringComparer.Ordinal);

            var impact = new AuthoringDeleteImpact();
            impact.DeletedNodeIds.AddRange(nodes);
            impact.DeletedGroupIds.AddRange(groups);
            impact.DeletedNoteIds.AddRange(notes);

            foreach (var nodeId in nodes)
            {
                if (string.Equals(document.Tree.RootNodeId, nodeId, StringComparison.Ordinal))
                    impact.RootNodeIds.Add(nodeId);
            }

            foreach (var parent in document.Tree.Nodes)
            {
                foreach (var childId in parent.ChildIds)
                {
                    if (!nodeSet.Contains(parent.Id) && !nodeSet.Contains(childId)) continue;
                    impact.RemovedEdgeCount++;
                    if (nodeSet.Contains(parent.Id) && !nodeSet.Contains(childId))
                        impact.DetachedChildNodeIds.Add(childId);
                }
            }

            foreach (var group in document.Groups)
            {
                if (group.NodeIds.Any(nodeSet.Contains) && !impact.AffectedGroupIds.Contains(group.Id))
                    impact.AffectedGroupIds.Add(group.Id);
            }

            return impact;
        }

        public static AuthoringDeleteImpact DeleteSelection(
            AuthoringSourceDocument document,
            IEnumerable<string>? nodeIds,
            IEnumerable<string>? groupIds,
            IEnumerable<string>? noteIds)
        {
            var impact = AnalyzeDelete(document, nodeIds, groupIds, noteIds);
            var nodeSet = new HashSet<string>(impact.DeletedNodeIds, StringComparer.Ordinal);
            var groupSet = new HashSet<string>(impact.DeletedGroupIds, StringComparer.Ordinal);
            var noteSet = new HashSet<string>(impact.DeletedNoteIds, StringComparer.Ordinal);

            if (nodeSet.Count > 0)
            {
                document.Tree.Nodes.RemoveAll(node => nodeSet.Contains(node.Id));
                foreach (var node in document.Tree.Nodes)
                    node.ChildIds.RemoveAll(nodeSet.Contains);
                document.Layout.RemoveAll(layout => nodeSet.Contains(layout.NodeId));
                document.NodeMetadata.RemoveAll(metadata => nodeSet.Contains(metadata.NodeId));
                foreach (var group in document.Groups)
                    group.NodeIds.RemoveAll(nodeSet.Contains);
                if (nodeSet.Contains(document.Tree.RootNodeId))
                    document.Tree.RootNodeId = "";
            }

            if (groupSet.Count > 0)
                document.Groups.RemoveAll(group => groupSet.Contains(group.Id));
            if (noteSet.Count > 0)
                document.Notes.RemoveAll(note => noteSet.Contains(note.Id));
            return impact;
        }

        public static bool UpdateNodeLayout(AuthoringSourceDocument document, string nodeId, float x, float y)
        {
            var layout = document.Layout.Find(item => string.Equals(item.NodeId, nodeId, StringComparison.Ordinal));
            if (layout == null)
            {
                document.Layout.Add(new NodeLayoutData { NodeId = nodeId, X = x, Y = y });
                return true;
            }
            if (Math.Abs(layout.X - x) < 0.001f && Math.Abs(layout.Y - y) < 0.001f) return false;
            layout.X = x;
            layout.Y = y;
            return true;
        }

        public static bool UpdateGroupLayout(AuthoringGroupData group, Rect rect)
        {
            if (group == null) return false;
            var changed = Math.Abs(group.X - rect.x) >= 0.001f
                || Math.Abs(group.Y - rect.y) >= 0.001f
                || Math.Abs(group.Width - rect.width) >= 0.001f
                || Math.Abs(group.Height - rect.height) >= 0.001f;
            group.X = rect.x;
            group.Y = rect.y;
            group.Width = rect.width;
            group.Height = rect.height;
            return changed;
        }

        public static bool UpdateNoteLayout(AuthoringNoteData note, Rect rect)
        {
            if (note == null) return false;
            var changed = Math.Abs(note.X - rect.x) >= 0.001f
                || Math.Abs(note.Y - rect.y) >= 0.001f
                || Math.Abs(note.Width - rect.width) >= 0.001f
                || Math.Abs(note.Height - rect.height) >= 0.001f;
            note.X = rect.x;
            note.Y = rect.y;
            note.Width = rect.width;
            note.Height = rect.height;
            return changed;
        }

        public static bool SetConnected(
            AuthoringSourceDocument document,
            string childId,
            string parentId,
            bool connected)
        {
            var parent = document.Tree.Nodes.Find(node => string.Equals(node.Id, parentId, StringComparison.Ordinal));
            if (parent == null) return false;
            if (connected)
            {
                if (parent.ChildIds.Contains(childId)) return false;
                parent.ChildIds.Add(childId);
                return true;
            }

            return parent.ChildIds.Remove(childId);
        }

        public static AuthoringBatchPropertyModel AnalyzeBatchProperties(
            AuthoringSourceDocument document,
            NodeRegistry registry,
            IEnumerable<string> nodeIds)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var ids = OrderedDistinct(nodeIds);
            var nodes = ids
                .Select(id => document.Tree.Nodes.Find(node => string.Equals(node.Id, id, StringComparison.Ordinal)))
                .Where(node => node != null)
                .Cast<NodeDefinition>()
                .ToList();

            var fieldsByName = new Dictionary<string, List<PropertyField>>(StringComparer.Ordinal);
            foreach (var node in nodes)
            {
                if (!registry.TryGetDescriptor(node.Type, out var descriptor)) continue;
                foreach (var field in descriptor.PropertySchema)
                {
                    if (!fieldsByName.TryGetValue(field.Name, out var list))
                    {
                        list = new List<PropertyField>();
                        fieldsByName[field.Name] = list;
                    }
                    list.Add(field);
                }
            }

            var resultFields = new List<AuthoringBatchPropertyField>();
            foreach (var pair in fieldsByName.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var compatible = pair.Value
                    .GroupBy(field => field.Type.ToString() + "|" + field.Kind)
                    .OrderByDescending(group => group.Count())
                    .First();
                var schema = compatible.First();
                var available = compatible.Count();
                var values = nodes
                    .Where(node => registry.TryGetDescriptor(node.Type, out var descriptor)
                        && descriptor.PropertySchema.Any(field => IsCompatibleField(schema, field)))
                    .Select(node => node.Properties.TryGet(schema.Name, out var value)
                        ? value
                        : (schema.Default ?? DefaultOf(schema.Type)))
                    .ToList();
                var state = values.Count == 0
                    ? AuthoringBatchValueState.Missing
                    : values.All(value => PropertyValueEquals(value, values[0]))
                        ? AuthoringBatchValueState.Same
                        : AuthoringBatchValueState.Mixed;
                resultFields.Add(new AuthoringBatchPropertyField(
                    schema,
                    state,
                    state == AuthoringBatchValueState.Same ? CloneValue(values[0]) : null,
                    available));
            }

            return new AuthoringBatchPropertyModel(ids, resultFields);
        }

        public static int ApplyBatchProperty(
            AuthoringSourceDocument document,
            NodeRegistry registry,
            IEnumerable<string> nodeIds,
            string propertyName,
            PropertyValue value)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (value == null) throw new ArgumentNullException(nameof(value));
            var count = 0;
            foreach (var nodeId in OrderedDistinct(nodeIds))
            {
                var node = document.Tree.Nodes.Find(item => string.Equals(item.Id, nodeId, StringComparison.Ordinal));
                if (node == null || !registry.TryGetDescriptor(node.Type, out var descriptor)) continue;
                var field = descriptor.PropertySchema.FirstOrDefault(item =>
                    string.Equals(item.Name, propertyName, StringComparison.Ordinal)
                    && item.Type == value.Type);
                if (field == null) continue;
                if (node.Properties.TryGet(propertyName, out var existing) && PropertyValueEquals(existing, value)) continue;
                node.Properties.Set(propertyName, CloneValue(value));
                count++;
            }
            return count;
        }

        public static IReadOnlyList<BlackboardUsage> FindBlackboardUsages(
            AuthoringSourceDocument document,
            NodeRegistry registry,
            string keyName)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var result = new List<BlackboardUsage>();
            if (string.IsNullOrWhiteSpace(keyName)) return result;
            var declaredType = document.Tree.Blackboard.TryGetType(keyName, out var type) ? type : (ValueType?)null;

            foreach (var node in document.Tree.Nodes)
            {
                if (!registry.TryGetDescriptor(node.Type, out var descriptor)) continue;
                foreach (var field in descriptor.PropertySchema)
                {
                    if (field.Kind != PropertyFieldKind.BlackboardKeyRef) continue;
                    if (!node.Properties.TryGet(field.Name, out var value)
                        || !value.TryGetString(out var referencedKey)
                        || !string.Equals(referencedKey, keyName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    result.Add(new BlackboardUsage(
                        keyName,
                        node.Id,
                        node.Type,
                        field.Name,
                        declaredType,
                        ClassifyBlackboardAccess(node.Type, field.Name),
                        new AuthoringJumpTarget(node.Id, field.Name)));
                }
            }

            return result;
        }

        public static BlackboardTypeChangeImpact AnalyzeBlackboardTypeChange(
            AuthoringSourceDocument document,
            NodeRegistry registry,
            string keyName,
            ValueType toType)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (!document.Tree.Blackboard.TryGetType(keyName, out var fromType))
                fromType = toType;
            var impact = new BlackboardTypeChangeImpact(keyName, fromType, toType);
            impact.Usages.AddRange(FindBlackboardUsages(document, registry, keyName));
            if (fromType == toType) return impact;
            if (impact.Usages.Count > 0)
                impact.Warnings.Add("Referenced nodes should be revalidated after this key type changes.");
            var key = document.Tree.Blackboard.Keys.Find(item => string.Equals(item.Name, keyName, StringComparison.Ordinal));
            if (key?.Default != null && key.Default.Type != toType)
                impact.Warnings.Add("The key default value type does not match the new type and should be reset.");
            return impact;
        }

        public static int ClearBlackboardReferences(
            AuthoringSourceDocument document,
            NodeRegistry registry,
            string keyName)
        {
            var references = FindBlackboardUsages(document, registry, keyName);
            foreach (var reference in references)
            {
                var node = document.Tree.Nodes.Find(item => string.Equals(item.Id, reference.NodeId, StringComparison.Ordinal));
                node?.Properties.Set(reference.PropertyName, PropertyValue.Of(""));
            }
            return references.Count;
        }

        public static PropertyValue CloneValue(PropertyValue value) => value.Type switch
        {
            ValueType.Bool => PropertyValue.Of(value.BoolValue),
            ValueType.Int64 => PropertyValue.Of(value.Int64Value),
            ValueType.Fixed64 => PropertyValue.Of(Fixed64.FromRaw(value.Fixed64Raw)),
            ValueType.String => PropertyValue.Of(value.StringValue),
            _ => PropertyValue.Of(0L),
        };

        private static AuthoringSourceDocument CreateSubgraphDocument(
            AuthoringSourceDocument document,
            IEnumerable<string>? nodeIds,
            IEnumerable<string>? groupIds,
            IEnumerable<string>? noteIds)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var selectedNodeIds = new HashSet<string>(OrderedDistinct(nodeIds), StringComparer.Ordinal);
            var selectedGroupIds = new HashSet<string>(OrderedDistinct(groupIds), StringComparer.Ordinal);
            var selectedNoteIds = new HashSet<string>(OrderedDistinct(noteIds), StringComparer.Ordinal);
            var clipboard = new AuthoringSourceDocument
            {
                Metadata = new AuthoringMetadata
                {
                    Author = document.Metadata.Author,
                    Description = "Clipboard",
                },
                Tree =
                {
                    TreeId = document.Tree.TreeId,
                    FormatVersion = document.Tree.FormatVersion,
                    RootNodeId = selectedNodeIds.Contains(document.Tree.RootNodeId) ? document.Tree.RootNodeId : "",
                },
            };

            foreach (var key in document.Tree.Blackboard.Keys)
            {
                clipboard.Tree.Blackboard.Keys.Add(new BlackboardKeyDefinition
                {
                    Name = key.Name,
                    Type = key.Type,
                    Default = key.Default == null ? null : CloneValue(key.Default),
                });
            }

            foreach (var node in document.Tree.Nodes)
            {
                if (!selectedNodeIds.Contains(node.Id)) continue;
                var clone = CloneNode(node);
                clone.ChildIds.RemoveAll(childId => !selectedNodeIds.Contains(childId));
                clipboard.Tree.Nodes.Add(clone);
            }

            foreach (var layout in document.Layout)
            {
                if (selectedNodeIds.Contains(layout.NodeId))
                    clipboard.Layout.Add(new NodeLayoutData { NodeId = layout.NodeId, X = layout.X, Y = layout.Y });
            }

            foreach (var metadata in document.NodeMetadata)
            {
                if (selectedNodeIds.Contains(metadata.NodeId))
                {
                    clipboard.NodeMetadata.Add(new AuthoringNodeMetadata
                    {
                        NodeId = metadata.NodeId,
                        DisplayName = metadata.DisplayName,
                        Comment = metadata.Comment,
                    });
                }
            }

            foreach (var group in document.Groups)
            {
                var memberIds = group.NodeIds.Where(selectedNodeIds.Contains).ToList();
                if (memberIds.Count == 0 && !selectedGroupIds.Contains(group.Id)) continue;
                clipboard.Groups.Add(new AuthoringGroupData
                {
                    Id = group.Id,
                    Title = group.Title,
                    X = group.X,
                    Y = group.Y,
                    Width = group.Width,
                    Height = group.Height,
                    NodeIds = memberIds,
                });
            }

            foreach (var note in document.Notes)
            {
                if (!selectedNoteIds.Contains(note.Id)) continue;
                clipboard.Notes.Add(new AuthoringNoteData
                {
                    Id = note.Id,
                    Text = note.Text,
                    X = note.X,
                    Y = note.Y,
                    Width = note.Width,
                    Height = note.Height,
                });
            }

            return clipboard;
        }

        private static NodeDefinition CloneNode(NodeDefinition source)
        {
            var clone = new NodeDefinition
            {
                Id = source.Id,
                Type = source.Type,
            };
            foreach (var property in source.Properties.Values)
                clone.Properties.Set(property.Key, CloneValue(property.Value));
            clone.ChildIds.AddRange(source.ChildIds);
            return clone;
        }

        private static Vector2 ComputePasteOffset(AuthoringSourceDocument clipboard, Vector2? targetOrigin)
        {
            if (targetOrigin == null) return new Vector2(32f, 32f);

            // 节点是子图粘贴的主锚点；组框可能有额外 padding，不应把首个节点推离光标。
            if (clipboard.Layout.Count > 0)
            {
                var minNodeX = clipboard.Layout.Min(layout => layout.X);
                var minNodeY = clipboard.Layout.Min(layout => layout.Y);
                return targetOrigin.Value - new Vector2(minNodeX, minNodeY);
            }

            var hasAny = false;
            var minX = float.MaxValue;
            var minY = float.MaxValue;
            foreach (var group in clipboard.Groups)
            {
                hasAny = true;
                minX = Math.Min(minX, group.X);
                minY = Math.Min(minY, group.Y);
            }
            foreach (var note in clipboard.Notes)
            {
                hasAny = true;
                minX = Math.Min(minX, note.X);
                minY = Math.Min(minY, note.Y);
            }
            return hasAny ? targetOrigin.Value - new Vector2(minX, minY) : Vector2.zero;
        }

        private static string UniqueId(HashSet<string> existing, string requested)
        {
            var baseId = SanitizeId(requested);
            var candidate = baseId;
            var suffix = 1;
            while (!existing.Add(candidate))
            {
                candidate = baseId + "_" + suffix++;
            }
            return candidate;
        }

        private static string SanitizeId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "item_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            return id.Replace(" ", "_").Replace("\t", "_");
        }

        private static List<string> OrderedDistinct(IEnumerable<string>? ids)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (ids == null) return result;
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) continue;
                result.Add(id);
            }
            return result;
        }

        private static bool IsCompatibleField(PropertyField left, PropertyField right)
            => string.Equals(left.Name, right.Name, StringComparison.Ordinal)
                && left.Type == right.Type
                && left.Kind == right.Kind;

        private static bool PropertyValueEquals(PropertyValue left, PropertyValue right)
        {
            if (left.Type != right.Type) return false;
            return left.Type switch
            {
                ValueType.Bool => left.BoolValue == right.BoolValue,
                ValueType.Int64 => left.Int64Value == right.Int64Value,
                ValueType.Fixed64 => left.Fixed64Raw == right.Fixed64Raw,
                ValueType.String => string.Equals(left.StringValue, right.StringValue, StringComparison.Ordinal),
                _ => false,
            };
        }

        private static PropertyValue DefaultOf(ValueType type) => type switch
        {
            ValueType.Bool => PropertyValue.Of(false),
            ValueType.Int64 => PropertyValue.Of(0L),
            ValueType.Fixed64 => PropertyValue.Of(Fixed64.Zero),
            ValueType.String => PropertyValue.Of(""),
            _ => PropertyValue.Of(0L),
        };

        private static AuthoringBlackboardAccess ClassifyBlackboardAccess(string nodeType, string propertyName)
        {
            if (string.Equals(nodeType, BuiltInNodeTypes.SetBlackboard, StringComparison.Ordinal))
            {
                if (string.Equals(propertyName, SetBlackboardNode.KeyProperty, StringComparison.Ordinal))
                    return AuthoringBlackboardAccess.Write;
                if (string.Equals(propertyName, SetBlackboardNode.FromKeyProperty, StringComparison.Ordinal))
                    return AuthoringBlackboardAccess.Read;
            }

            if (string.Equals(nodeType, BuiltInNodeTypes.BlackboardCompare, StringComparison.Ordinal)
                || string.Equals(nodeType, BuiltInNodeTypes.BlackboardHasKey, StringComparison.Ordinal))
            {
                return AuthoringBlackboardAccess.Read;
            }

            var lower = propertyName.ToLowerInvariant();
            if (lower.Contains("target") || lower.Contains("write") || lower.Contains("set"))
                return AuthoringBlackboardAccess.Write;
            if (lower.Contains("from") || lower.Contains("source") || lower.Contains("left") || lower.Contains("right"))
                return AuthoringBlackboardAccess.Read;
            return AuthoringBlackboardAccess.Unknown;
        }
    }
}
