#if UNITY_EDITOR
#nullable enable

using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.Documents;
using NUnit.Framework;

using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Editor;
namespace AbilityKit.BehaviorTree.Editor.Tests
{
    public sealed class AuthoringDocumentSessionTests
    {
        [Test]
        public void UndoRedo_RestoresDocumentAndTracksDirtyState()
        {
            var session = new AuthoringDocumentSession();
            session.Open(CreateDocument("before"));

            session.RecordChange();
            session.Document.Metadata.Description = "after";

            Assert.That(session.IsDirty, Is.True);
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.Document.Metadata.Description, Is.EqualTo("before"));
            Assert.That(session.Redo(), Is.True);
            Assert.That(session.Document.Metadata.Description, Is.EqualTo("after"));

            session.MarkSaved();
            Assert.That(session.IsDirty, Is.False);
            session.RecordChange();
            session.Document.Metadata.Description = "discarded";
            Assert.That(session.DiscardChanges(), Is.True);
            Assert.That(session.Document.Metadata.Description, Is.EqualTo("after"));
            Assert.That(session.IsDirty, Is.False);
        }

        [Test]
        public void Open_ClearsHistoryAndReadOnlySessionRejectsChanges()
        {
            var session = new AuthoringDocumentSession();
            session.Open(CreateDocument("editable"));
            session.RecordChange();

            session.Open(CreateDocument("observed"), isReadOnly: true);

            Assert.That(session.IsReadOnly, Is.True);
            Assert.That(session.RecordChange(), Is.False);
            Assert.That(session.CanUndo, Is.False);
            Assert.That(session.CanRedo, Is.False);
            Assert.That(session.IsDirty, Is.False);
        }

        [Test]
        public void HistoryLimit_DropsOldestSnapshot()
        {
            var session = new AuthoringDocumentSession(historyLimit: 2);
            session.Open(CreateDocument("0"));
            for (var i = 1; i <= 3; i++)
            {
                session.RecordChange();
                session.Document.Metadata.Description = i.ToString();
            }

            Assert.That(session.Undo(), Is.True);
            Assert.That(session.Document.Metadata.Description, Is.EqualTo("2"));
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.Document.Metadata.Description, Is.EqualTo("1"));
            Assert.That(session.Undo(), Is.False);
        }

        [Test]
        public void TrySwitch_CancelPreservesDirtyCurrentDocument()
        {
            var session = new AuthoringDocumentSession();
            session.Open(CreateDocument("current"));
            session.RecordChange();
            session.Document.Metadata.Description = "dirty";

            var switched = session.TrySwitch(
                CreateDocument("next"),
                decide: _ => EditorDocumentSwitchDecision.Cancel);

            Assert.That(switched, Is.False);
            Assert.That(session.Document.Metadata.Description, Is.EqualTo("dirty"));
            Assert.That(session.IsDirty, Is.True);
            Assert.That(session.CanUndo, Is.True);
        }

        [Test]
        public void TrySwitch_DiscardOpensNextAndClearsHistory()
        {
            var session = new AuthoringDocumentSession();
            session.Open(CreateDocument("current"));
            session.RecordChange();
            session.Document.Metadata.Description = "dirty";

            var switched = session.TrySwitch(
                CreateDocument("next"),
                decide: _ => EditorDocumentSwitchDecision.Discard);

            Assert.That(switched, Is.True);
            Assert.That(session.Document.Metadata.Description, Is.EqualTo("next"));
            Assert.That(session.IsDirty, Is.False);
            Assert.That(session.CanUndo, Is.False);
            Assert.That(session.CanRedo, Is.False);
        }

        [Test]
        public void TrySwitch_SavePersistsCurrentThenOpensReadOnlyNext()
        {
            var session = new AuthoringDocumentSession();
            session.Open(CreateDocument("current"));
            session.RecordChange();
            session.Document.Metadata.Description = "saved-before-switch";
            string? savedDescription = null;

            var switched = session.TrySwitch(
                CreateDocument("observed"),
                nextIsReadOnly: true,
                decide: _ => EditorDocumentSwitchDecision.Save,
                saveCurrent: document => savedDescription = document.Metadata.Description);

            Assert.That(switched, Is.True);
            Assert.That(savedDescription, Is.EqualTo("saved-before-switch"));
            Assert.That(session.Document.Metadata.Description, Is.EqualTo("observed"));
            Assert.That(session.IsReadOnly, Is.True);
            Assert.That(session.IsDirty, Is.False);
            Assert.That(session.CanUndo, Is.False);
        }

        [Test]
        public void UndoRedo_ConvergesAgainstSavedBaseline()
        {
            var session = new AuthoringDocumentSession();
            session.Open(CreateDocument("baseline"));
            session.RecordChange();
            session.Document.Metadata.Description = "changed";

            Assert.That(session.IsDirty, Is.True);
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.IsDirty, Is.False);
            Assert.That(session.Redo(), Is.True);
            Assert.That(session.IsDirty, Is.True);
        }

        private static AuthoringSourceDocument CreateDocument(string description)
        {
            var document = new AuthoringSourceDocument();
            document.Metadata.Description = description;
            return document;
        }
    }
}
#endif
