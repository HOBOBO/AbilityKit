#nullable enable

using System;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.Documents;

using UnityEngine.Scripting.APIUpdating;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// Behavior Tree compatibility facade over the platform document session.
    /// Existing consumers retain the BT-specific API while lifecycle and bounded
    /// history behavior are supplied by the shared platform implementation.
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringDocumentSession")]
    public sealed class AuthoringDocumentSession
    {
        private readonly EditorDocumentSession<AuthoringSourceDocument> _session;

        public AuthoringDocumentSession(int historyLimit = 64)
        {
            _session = new EditorDocumentSession<AuthoringSourceDocument>(
                new AuthoringDocumentSerializer(),
                historyLimit);
            _session.Open(new AuthoringSourceDocument());
        }

        public event Action<EditorDocumentChangeKind> Changed
        {
            add => _session.Changed += value;
            remove => _session.Changed -= value;
        }

        public AuthoringSourceDocument Document => _session.Document;
        public bool IsReadOnly => _session.IsReadOnly;
        public bool IsDirty => _session.IsDirty;
        public bool CanUndo => _session.CanUndo;
        public bool CanRedo => _session.CanRedo;

        public void Open(AuthoringSourceDocument document, bool isReadOnly = false)
        {
            _session.Open(document, isReadOnly);
        }

        public bool RecordChange()
        {
            return _session.RecordChange();
        }

        public bool RecordChange(string beforeChangeSnapshot)
        {
            return _session.RecordChange(beforeChangeSnapshot);
        }

        public bool Undo()
        {
            return _session.Undo();
        }

        public bool Redo()
        {
            return _session.Redo();
        }

        public void RefreshDirtyState()
        {
            _session.RefreshDirtyState();
        }

        public void MarkSaved()
        {
            _session.MarkSaved();
        }

        public bool DiscardChanges()
        {
            return _session.DiscardChanges();
        }

        public bool TrySwitch(
            AuthoringSourceDocument nextDocument,
            bool nextIsReadOnly = false,
            Func<EditorDocumentSwitchContext<AuthoringSourceDocument>, EditorDocumentSwitchDecision>? decide = null,
            Action<AuthoringSourceDocument>? saveCurrent = null)
        {
            return _session.TrySwitch(nextDocument, nextIsReadOnly, decide, saveCurrent);
        }
    }
}
