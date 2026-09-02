#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace AbilityKit.Editor.Platform.Documents
{
    public interface IEditorDocumentSerializer<TDocument>
        where TDocument : class
    {
        string Serialize(TDocument document);
        TDocument Deserialize(string snapshot);
    }

    public enum EditorDocumentChangeKind
    {
        Opened,
        Changed,
        Undone,
        Redone,
        Saved,
        Discarded,
        Closed
    }

    public enum EditorDocumentSwitchDecision
    {
        Cancel,
        Discard,
        Save
    }

    public sealed class EditorDocumentSwitchContext<TDocument>
        where TDocument : class
    {
        public EditorDocumentSwitchContext(TDocument currentDocument, TDocument nextDocument)
        {
            CurrentDocument = currentDocument;
            NextDocument = nextDocument;
        }

        public TDocument CurrentDocument { get; }
        public TDocument NextDocument { get; }
    }

    /// <summary>
    /// Owns one editor document and its bounded snapshot history. Serialization remains
    /// domain-owned through IEditorDocumentSerializer, so the platform never understands
    /// behavior-tree, HFSM, pipeline, or trigger semantics.
    /// </summary>
    public sealed class EditorDocumentSession<TDocument>
        where TDocument : class
    {
        private readonly IEditorDocumentSerializer<TDocument> _serializer;
        private readonly int _historyLimit;
        private readonly List<string> _undo = new List<string>();
        private readonly Stack<string> _redo = new Stack<string>();
        private string _savedSnapshot;

        public EditorDocumentSession(IEditorDocumentSerializer<TDocument> serializer, int historyLimit = 64)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            if (historyLimit <= 0) throw new ArgumentOutOfRangeException(nameof(historyLimit));
            _historyLimit = historyLimit;
        }

        public event Action<EditorDocumentChangeKind> Changed;

        public TDocument Document { get; private set; }
        public bool IsOpen => Document != null;
        public bool IsReadOnly { get; private set; }
        public bool IsDirty { get; private set; }
        public bool CanUndo => IsOpen && !IsReadOnly && _undo.Count > 0;
        public bool CanRedo => IsOpen && !IsReadOnly && _redo.Count > 0;
        public int UndoCount => _undo.Count;
        public int RedoCount => _redo.Count;

        public void Open(TDocument document, bool isReadOnly = false)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            IsReadOnly = isReadOnly;
            _savedSnapshot = SerializeCurrent();
            _undo.Clear();
            _redo.Clear();
            IsDirty = false;
            Changed?.Invoke(EditorDocumentChangeKind.Opened);
        }

        /// <summary>Records the current state immediately before a mutation.</summary>
        public bool RecordChange()
        {
            EnsureOpen();
            return RecordChange(SerializeCurrent());
        }

        /// <summary>Records a caller-captured state immediately before a mutation.</summary>
        public bool RecordChange(string beforeChangeSnapshot)
        {
            EnsureOpen();
            if (IsReadOnly) return false;
            if (beforeChangeSnapshot == null)
            {
                throw new ArgumentNullException(nameof(beforeChangeSnapshot));
            }

            _redo.Clear();
            PushUndo(beforeChangeSnapshot);
            IsDirty = true;
            Changed?.Invoke(EditorDocumentChangeKind.Changed);
            return true;
        }

        /// <summary>
        /// Re-evaluates dirty state after an external mutation. Call this when a mutation may
        /// result in content equal to the saved baseline.
        /// </summary>
        public void RefreshDirtyState()
        {
            EnsureOpen();
            var dirty = !SnapshotsEqual(SerializeCurrent(), _savedSnapshot);
            if (IsDirty == dirty) return;
            IsDirty = dirty;
            Changed?.Invoke(EditorDocumentChangeKind.Changed);
        }

        public bool Undo()
        {
            if (!CanUndo) return false;
            _redo.Push(SerializeCurrent());
            var snapshot = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            Document = Deserialize(snapshot);
            UpdateDirty(snapshot);
            Changed?.Invoke(EditorDocumentChangeKind.Undone);
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo) return false;
            var current = SerializeCurrent();
            var snapshot = _redo.Pop();
            PushUndo(current);
            Document = Deserialize(snapshot);
            UpdateDirty(snapshot);
            Changed?.Invoke(EditorDocumentChangeKind.Redone);
            return true;
        }

        public void MarkSaved()
        {
            EnsureOpen();
            _savedSnapshot = SerializeCurrent();
            IsDirty = false;
            Changed?.Invoke(EditorDocumentChangeKind.Saved);
        }

        public bool DiscardChanges()
        {
            EnsureOpen();
            if (IsReadOnly || !IsDirty) return false;
            Document = Deserialize(_savedSnapshot);
            _undo.Clear();
            _redo.Clear();
            IsDirty = false;
            Changed?.Invoke(EditorDocumentChangeKind.Discarded);
            return true;
        }

        public bool TrySwitch(
            TDocument nextDocument,
            bool nextIsReadOnly = false,
            Func<EditorDocumentSwitchContext<TDocument>, EditorDocumentSwitchDecision> decide = null,
            Action<TDocument> saveCurrent = null)
        {
            if (nextDocument == null) throw new ArgumentNullException(nameof(nextDocument));
            if (IsOpen && IsDirty)
            {
                var decision = decide?.Invoke(new EditorDocumentSwitchContext<TDocument>(Document, nextDocument))
                               ?? EditorDocumentSwitchDecision.Cancel;
                switch (decision)
                {
                    case EditorDocumentSwitchDecision.Cancel:
                        return false;
                    case EditorDocumentSwitchDecision.Discard:
                        break;
                    case EditorDocumentSwitchDecision.Save:
                        if (saveCurrent == null)
                        {
                            throw new InvalidOperationException("A save callback is required when the switch decision is Save.");
                        }

                        saveCurrent(Document);
                        MarkSaved();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown document switch decision.");
                }
            }

            Open(nextDocument, nextIsReadOnly);
            return true;
        }

        public void Close()
        {
            Document = null;
            IsReadOnly = false;
            IsDirty = false;
            _savedSnapshot = null;
            _undo.Clear();
            _redo.Clear();
            Changed?.Invoke(EditorDocumentChangeKind.Closed);
        }

        private void PushUndo(string snapshot)
        {
            _undo.Add(snapshot);
            if (_undo.Count > _historyLimit) _undo.RemoveAt(0);
        }

        private string SerializeCurrent()
        {
            var snapshot = _serializer.Serialize(Document);
            if (snapshot == null)
            {
                throw new InvalidOperationException("The document serializer returned a null snapshot.");
            }

            return snapshot;
        }

        private TDocument Deserialize(string snapshot)
        {
            var document = _serializer.Deserialize(snapshot);
            return document ?? throw new InvalidOperationException("The document serializer returned a null document.");
        }

        private void UpdateDirty(string currentSnapshot)
        {
            IsDirty = !SnapshotsEqual(currentSnapshot, _savedSnapshot);
        }

        private static bool SnapshotsEqual(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private void EnsureOpen()
        {
            if (!IsOpen) throw new InvalidOperationException("No editor document is open.");
        }
    }
}
#endif
