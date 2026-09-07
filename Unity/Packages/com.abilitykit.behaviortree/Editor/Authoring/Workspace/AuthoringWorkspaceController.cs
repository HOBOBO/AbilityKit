#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.Diagnostics;
using AbilityKit.Editor.Platform.State;
using UnityEngine;

using AbilityKit.BehaviorTree.Editor;
using AbilityKit.BehaviorTree.Editor.Bootstrap;
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
    /// <summary>Persistent authoring UI state; it does not enter runtime definitions or authoring documents.</summary>
    internal sealed class AuthoringWorkspaceState
    {
        private const string SelectedNodeKey = "selected-node";
        private const string SearchKey = "node-search";
        private const string RecentNodeTypesKey = "recent-node-types";
        private const string FavoriteNodeTypesKey = "favorite-node-types";
        private const string InspectorWidthKey = "inspector-width";
        private const string RecentDocumentKey = "recent-document";
        private const string ViewportRecordedKey = "viewport.recorded";
        private const string ViewportXKey = "viewport.x";
        private const string ViewportYKey = "viewport.y";
        private const string ViewportScaleKey = "viewport.scale";
        private const string FoldoutPrefix = "foldout.";
        private const string PanelPrefix = "panel.";
        private readonly IEditorUserStateStore _store;
        private string _documentScope = "global";

        public AuthoringWorkspaceState(IEditorUserStateStore? store = null)
        {
            _store = store ?? new EditorPrefsUserStateStore(EditorModule.ModuleId, "authoring-workspace");
        }

        public string SelectedNodeId
        {
            get => _store.GetString(DocumentKey(SelectedNodeKey), string.Empty);
            set => _store.SetString(DocumentKey(SelectedNodeKey), value ?? string.Empty);
        }

        public string NodeSearch
        {
            get => _store.GetString(DocumentKey(SearchKey), string.Empty);
            set => _store.SetString(DocumentKey(SearchKey), value ?? string.Empty);
        }

        public string RecentNodeTypes
        {
            get => _store.GetString(DocumentKey(RecentNodeTypesKey), string.Empty);
            set => _store.SetString(DocumentKey(RecentNodeTypesKey), value ?? string.Empty);
        }

        public string FavoriteNodeTypes
        {
            get => _store.GetString(DocumentKey(FavoriteNodeTypesKey), string.Empty);
            set => _store.SetString(DocumentKey(FavoriteNodeTypesKey), value ?? string.Empty);
        }

        public float InspectorWidth
        {
            get => _store.GetFloat(InspectorWidthKey, 340f);
            set => _store.SetFloat(InspectorWidthKey, Math.Max(240f, value));
        }

        public string RecentDocumentId
        {
            get => _store.GetString(RecentDocumentKey, string.Empty);
            set => _store.SetString(RecentDocumentKey, value ?? string.Empty);
        }

        public void SetDocumentScope(string documentId)
        {
            _documentScope = string.IsNullOrWhiteSpace(documentId) ? "global" : Sanitize(documentId);
            RecentDocumentId = _documentScope;
        }

        public bool TryGetViewport(out AuthoringViewportState viewport)
        {
            if (!_store.GetBool(DocumentKey(ViewportRecordedKey), false))
            {
                viewport = default;
                return false;
            }

            viewport = new AuthoringViewportState(
                _store.GetFloat(DocumentKey(ViewportXKey), 0f),
                _store.GetFloat(DocumentKey(ViewportYKey), 0f),
                Math.Max(0.05f, _store.GetFloat(DocumentKey(ViewportScaleKey), 1f)));
            return true;
        }

        public void SetViewport(float x, float y, float scale)
        {
            _store.SetBool(DocumentKey(ViewportRecordedKey), true);
            _store.SetFloat(DocumentKey(ViewportXKey), x);
            _store.SetFloat(DocumentKey(ViewportYKey), y);
            _store.SetFloat(DocumentKey(ViewportScaleKey), Math.Max(0.05f, scale));
        }

        public bool GetFoldoutExpanded(string foldoutId, bool defaultValue = true)
        {
            return _store.GetBool(DocumentKey(FoldoutPrefix + Sanitize(foldoutId)), defaultValue);
        }

        public void SetFoldoutExpanded(string foldoutId, bool expanded)
        {
            _store.SetBool(DocumentKey(FoldoutPrefix + Sanitize(foldoutId)), expanded);
        }

        public bool GetPanelVisible(string panelId, bool defaultValue = true)
        {
            return _store.GetBool(DocumentKey(PanelPrefix + Sanitize(panelId) + ".visible"), defaultValue);
        }

        public void SetPanelVisible(string panelId, bool visible)
        {
            _store.SetBool(DocumentKey(PanelPrefix + Sanitize(panelId) + ".visible"), visible);
        }

        private string DocumentKey(string key) => "document." + _documentScope + "." + key;

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "global";
            var chars = value.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (char.IsLetterOrDigit(chars[i]) || chars[i] == '-' || chars[i] == '_' || chars[i] == '.')
                    continue;
                chars[i] = '_';
            }
            return new string(chars);
        }
    }

    internal readonly struct AuthoringViewportState
    {
        public AuthoringViewportState(float x, float y, float scale)
        {
            X = x;
            Y = y;
            Scale = Math.Max(0.05f, scale);
        }

        public float X { get; }
        public float Y { get; }
        public float Scale { get; }
    }

    /// <summary>
    /// Centralizes document session, selection, pre-mutation snapshots, undo/redo,
    /// and diagnostic invalidation for authoring workspace operations.
    /// </summary>
    internal sealed class AuthoringWorkspaceController
    {
        private readonly AuthoringDocumentSession _session;
        private readonly EditorDiagnosticCollection _diagnostics;
        private string? _selectedNodeId;

        public AuthoringWorkspaceController(
            AuthoringDocumentSession? session = null,
            EditorDiagnosticCollection? diagnostics = null,
            AuthoringWorkspaceState? state = null)
        {
            _session = session ?? new AuthoringDocumentSession();
            _diagnostics = diagnostics ?? new EditorDiagnosticCollection();
            State = state ?? new AuthoringWorkspaceState();
            _selectedNodeId = State.SelectedNodeId;
        }

        public event Action? DocumentChanged;
        public event Action? SelectionChanged;
        public event Action? DiagnosticsInvalidated;

        public AuthoringDocumentSession Session => _session;
        public AuthoringSourceDocument Document => _session.Document;
        public EditorDiagnosticCollection Diagnostics => _diagnostics;
        public AuthoringWorkspaceState State { get; }
        public bool IsReadOnly => _session.IsReadOnly;
        public bool IsDirty => _session.IsDirty;
        public bool CanUndo => _session.CanUndo;
        public bool CanRedo => _session.CanRedo;
        public string? SelectedNodeId => _selectedNodeId;

        public void Open(AuthoringSourceDocument document, bool isReadOnly = false)
        {
            _session.Open(document ?? throw new ArgumentNullException(nameof(document)), isReadOnly);
            SetSelection(null);
            InvalidateDiagnostics();
            DocumentChanged?.Invoke();
        }

        public bool Mutate(Action<AuthoringSourceDocument> mutation)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            if (IsReadOnly) return false;
            var before = AuthoringJson.Save(Document);
            mutation(Document);
            return CompleteMutation(before);
        }

        public bool Mutate(string beforeChangeSnapshot, Action<AuthoringSourceDocument> mutation)
        {
            if (string.IsNullOrWhiteSpace(beforeChangeSnapshot))
                throw new ArgumentException("A pre-mutation snapshot is required.", nameof(beforeChangeSnapshot));
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            if (IsReadOnly) return false;
            mutation(Document);
            return CompleteMutation(beforeChangeSnapshot);
        }

        public bool RecordExternalMutation()
        {
            if (IsReadOnly) return false;
            var changed = _session.RecordChange();
            if (changed) NotifyDocumentChanged();
            return changed;
        }

        public bool RecordExternalMutation(string beforeChangeSnapshot)
        {
            if (IsReadOnly) return false;
            var changed = _session.RecordChange(beforeChangeSnapshot);
            if (changed) NotifyDocumentChanged();
            return changed;
        }

        public bool Undo()
        {
            if (!_session.Undo()) return false;
            SetSelection(null);
            NotifyDocumentChanged();
            return true;
        }

        public bool Redo()
        {
            if (!_session.Redo()) return false;
            SetSelection(null);
            NotifyDocumentChanged();
            return true;
        }

        public void MarkSaved()
        {
            _session.MarkSaved();
            DocumentChanged?.Invoke();
        }

        public bool DiscardChanges()
        {
            if (!_session.DiscardChanges()) return false;
            SetSelection(null);
            NotifyDocumentChanged();
            return true;
        }

        public bool SetConnected(string childId, string parentId, bool connected, out string error)
        {
            return SetConnected(childId, parentId, connected, recordHistory: true, out error);
        }

        public bool AddNode(NodeDescriptor descriptor, Vector2 graphPosition, out string nodeId)
        {
            nodeId = string.Empty;
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (IsReadOnly) return false;

            var createdId = string.Empty;
            var changed = Mutate(document =>
            {
                createdId = "n" + Guid.NewGuid().ToString("N").Substring(0, 12);
                var node = new NodeDefinition
                {
                    Id = createdId,
                    Type = descriptor.TypeId,
                };
                foreach (var field in descriptor.PropertySchema)
                {
                    if (field.Default != null)
                        node.Properties.Set(field.Name, AuthoringMutationService.CloneValue(field.Default));
                }
                document.Tree.Nodes.Add(node);
                document.NodeMetadata.Add(new AuthoringNodeMetadata
                {
                    NodeId = createdId,
                    DisplayName = descriptor.DisplayName,
                });
                document.Layout.Add(new NodeLayoutData
                {
                    NodeId = createdId,
                    X = graphPosition.x,
                    Y = graphPosition.y,
                });
                if (string.IsNullOrWhiteSpace(document.Tree.RootNodeId))
                    document.Tree.RootNodeId = createdId;
            });
            nodeId = createdId;
            return changed;
        }

        public AuthoringDeleteImpact AnalyzeDelete(
            IEnumerable<string>? nodeIds,
            IEnumerable<string>? groupIds = null,
            IEnumerable<string>? noteIds = null)
        {
            return AuthoringMutationService.AnalyzeDelete(Document, nodeIds, groupIds, noteIds);
        }

        public bool DeleteSelection(
            IEnumerable<string>? nodeIds,
            IEnumerable<string>? groupIds = null,
            IEnumerable<string>? noteIds = null)
        {
            return Mutate(document =>
            {
                AuthoringMutationService.DeleteSelection(document, nodeIds, groupIds, noteIds);
            });
        }

        public bool TryPasteSubgraph(
            string serializedSubgraph,
            Vector2 graphPosition,
            out AuthoringClipboardPasteResult result)
        {
            result = new AuthoringClipboardPasteResult();
            if (IsReadOnly) return false;
            if (!AuthoringMutationService.TryDeserializeSubgraph(serializedSubgraph, out var clipboard))
                return false;

            AuthoringClipboardPasteResult pasteResult = result;
            var changed = Mutate(document =>
            {
                pasteResult = AuthoringMutationService.PasteSubgraph(document, clipboard, graphPosition);
            });
            result = pasteResult;
            return changed;
        }

        public bool DuplicateSubgraph(
            IEnumerable<string>? nodeIds,
            IEnumerable<string>? groupIds,
            IEnumerable<string>? noteIds,
            Vector2 offset,
            out AuthoringClipboardPasteResult result)
        {
            result = new AuthoringClipboardPasteResult();
            if (IsReadOnly) return false;
            var serialized = AuthoringMutationService.SerializeSubgraph(Document, nodeIds, groupIds, noteIds);
            if (!AuthoringMutationService.TryDeserializeSubgraph(serialized, out var clipboard))
                return false;

            AuthoringClipboardPasteResult pasteResult = result;
            var changed = Mutate(document =>
            {
                pasteResult = AuthoringMutationService.PasteSubgraph(document, clipboard, offset);
            });
            result = pasteResult;
            return changed;
        }

        public AuthoringBatchPropertyModel AnalyzeBatchProperties(IEnumerable<string> nodeIds)
        {
            return AuthoringMutationService.AnalyzeBatchProperties(Document, EditorNodeCatalog.Registry, nodeIds);
        }

        public bool ApplyBatchProperty(IEnumerable<string> nodeIds, string propertyName, PropertyValue value)
        {
            return Mutate(document =>
            {
                AuthoringMutationService.ApplyBatchProperty(
                    document,
                    EditorNodeCatalog.Registry,
                    nodeIds,
                    propertyName,
                    value);
            });
        }

        public IReadOnlyList<BlackboardUsage> FindBlackboardUsages(string keyName)
        {
            return AuthoringMutationService.FindBlackboardUsages(Document, EditorNodeCatalog.Registry, keyName);
        }

        public BlackboardTypeChangeImpact AnalyzeBlackboardTypeChange(string keyName, ValueType toType)
        {
            return AuthoringMutationService.AnalyzeBlackboardTypeChange(
                Document,
                EditorNodeCatalog.Registry,
                keyName,
                toType);
        }

        public bool RenameBlackboardKey(string oldName, string newName, out IReadOnlyList<BlackboardUsage> affected)
        {
            affected = Array.Empty<BlackboardUsage>();
            if (IsReadOnly) return false;
            if (string.Equals(oldName, newName, StringComparison.Ordinal)) return false;
            var beforeUsages = FindBlackboardUsages(oldName);
            var changed = Mutate(document =>
            {
                KeyReferenceIndex.RenameKey(
                    document.Tree,
                    EditorNodeCatalog.Registry,
                    oldName,
                    newName);
            });
            affected = beforeUsages;
            return changed;
        }

        public bool ChangeBlackboardKeyType(string keyName, ValueType type, out BlackboardTypeChangeImpact impact)
        {
            impact = AnalyzeBlackboardTypeChange(keyName, type);
            if (IsReadOnly) return false;
            return Mutate(document =>
            {
                var key = document.Tree.Blackboard.Keys.Find(item =>
                    string.Equals(item.Name, keyName, StringComparison.Ordinal));
                if (key == null) return;
                key.Type = type;
                if (key.Default != null && key.Default.Type != type)
                    key.Default = null;
            });
        }

        /// <summary>
        /// Applies a GraphView connection intent after GraphView has already recorded
        /// the undo snapshot for that batch.
        /// </summary>
        public bool SetConnectedFromRecordedGraphChange(
            string childId,
            string parentId,
            bool connected,
            out string error)
        {
            return SetConnected(childId, parentId, connected, recordHistory: false, out error);
        }

        private bool SetConnected(
            string childId,
            string parentId,
            bool connected,
            bool recordHistory,
            out string error)
        {
            error = string.Empty;
            if (IsReadOnly) return false;
            if (string.IsNullOrWhiteSpace(childId) || string.IsNullOrWhiteSpace(parentId)) return false;
            var parent = Document.Tree.Nodes.Find(node => node.Id == parentId);
            if (parent == null) return false;

            if (connected)
            {
                if (!EditorNodeCatalog.Registry.TryGetDescriptor(parent.Type, out var descriptor))
                {
                    error = $"Parent node '{parentId}' type is not registered.";
                    return false;
                }
                if (!GraphOperations.CanConnect(
                        Document.Tree, parentId, childId, descriptor.MaxChildren, out error))
                    return false;
                if (parent.ChildIds.Contains(childId)) return false;
            }
            else if (!parent.ChildIds.Contains(childId))
            {
                return false;
            }

            if (recordHistory)
            {
                return Mutate(document => ApplyConnection(document, childId, parentId, connected));
            }

            ApplyConnection(Document, childId, parentId, connected);
            InvalidateDiagnostics();
            DocumentChanged?.Invoke();
            return true;
        }

        private static void ApplyConnection(
            AuthoringSourceDocument document,
            string childId,
            string parentId,
            bool connected)
        {
            AuthoringMutationService.SetConnected(document, childId, parentId, connected);
        }

        public void SetSelection(string? nodeId)
        {
            var normalized = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId;
            if (string.Equals(_selectedNodeId, normalized, StringComparison.Ordinal)) return;
            _selectedNodeId = normalized;
            State.SelectedNodeId = normalized ?? string.Empty;
            SelectionChanged?.Invoke();
        }

        public void ReplaceDiagnostics(EditorDiagnosticCollection diagnostics)
        {
            if (diagnostics == null) throw new ArgumentNullException(nameof(diagnostics));
            _diagnostics.Replace(diagnostics.Items);
        }

        private bool CompleteMutation(string beforeChangeSnapshot)
        {
            var changed = _session.RecordChange(beforeChangeSnapshot);
            if (changed) NotifyDocumentChanged();
            return changed;
        }

        private void NotifyDocumentChanged()
        {
            InvalidateDiagnostics();
            DocumentChanged?.Invoke();
        }

        private void InvalidateDiagnostics()
        {
            _diagnostics.Clear();
            DiagnosticsInvalidated?.Invoke();
        }
    }
}
