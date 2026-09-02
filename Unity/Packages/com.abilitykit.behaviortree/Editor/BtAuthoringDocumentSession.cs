#nullable enable

using System;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.Documents;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// Behavior Tree compatibility facade over the platform document session.
    /// Existing consumers retain the BT-specific API while lifecycle and bounded
    /// history behavior are supplied by the shared platform implementation.
    /// </summary>
    public sealed class BtAuthoringDocumentSession
    {
        private readonly EditorDocumentSession<BtAuthoringSourceDocument> _session;

        public BtAuthoringDocumentSession(int historyLimit = 64)
        {
            _session = new EditorDocumentSession<BtAuthoringSourceDocument>(
                new BtAuthoringDocumentSerializer(),
                historyLimit);
            _session.Open(new BtAuthoringSourceDocument());
        }

        public event Action<EditorDocumentChangeKind> Changed
        {
            add => _session.Changed += value;
            remove => _session.Changed -= value;
        }

        public BtAuthoringSourceDocument Document => _session.Document;
        public bool IsReadOnly => _session.IsReadOnly;
        public bool IsDirty => _session.IsDirty;
        public bool CanUndo => _session.CanUndo;
        public bool CanRedo => _session.CanRedo;

        public void Open(BtAuthoringSourceDocument document, bool isReadOnly = false)
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
            BtAuthoringSourceDocument nextDocument,
            bool nextIsReadOnly = false,
            Func<EditorDocumentSwitchContext<BtAuthoringSourceDocument>, EditorDocumentSwitchDecision>? decide = null,
            Action<BtAuthoringSourceDocument>? saveCurrent = null)
        {
            return _session.TrySwitch(nextDocument, nextIsReadOnly, decide, saveCurrent);
        }
    }
}
