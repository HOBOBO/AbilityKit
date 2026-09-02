#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Editor.Platform.Commands;
using AbilityKit.Editor.Platform.Core;
using AbilityKit.Editor.Platform.Diagnostics;
using AbilityKit.Editor.Platform.Documents;
using AbilityKit.Editor.Platform.Export;
using AbilityKit.Editor.Platform.Localization;
using AbilityKit.Editor.Platform.Synchronization;
using AbilityKit.Editor.Platform.State;
using AbilityKit.Editor.Platform.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace AbilityKit.Editor.Platform.Tests
{
    public sealed class EditorPlatformCoreTests
    {
        [Test]
        public void ServiceRegistry_RejectsDuplicateRegistration_AndResolvesService()
        {
            var registry = new EditorServiceRegistry();
            var service = new object();

            registry.Register(service);

            Assert.That(registry.Resolve<object>(), Is.SameAs(service));
            Assert.Throws<InvalidOperationException>(() => registry.Register(new object()));
            Assert.That(registry.Unregister<object>(), Is.True);
            Assert.That(registry.TryResolve<object>(out _), Is.False);
        }

        [Test]
        public void ModuleRegistry_RegistrationHandle_UnregistersSymmetrically()
        {
            var services = new EditorServiceRegistry();
            var registry = new EditorModuleRegistry(new EditorPlatformContext(services));
            var module = new TestModule("test.module", order: 5);

            var handle = registry.Register(module);

            Assert.That(module.RegisterCount, Is.EqualTo(1));
            Assert.That(registry.Modules, Has.Count.EqualTo(1));

            handle.Dispose();
            handle.Dispose();

            Assert.That(module.UnregisterCount, Is.EqualTo(1));
            Assert.That(registry.Modules, Is.Empty);
        }

        [Test]
        public void ModuleRegistry_OrdersByOrderThenId()
        {
            var registry = new EditorModuleRegistry(new EditorPlatformContext(new EditorServiceRegistry()));
            registry.Register(new TestModule("z", order: 0));
            registry.Register(new TestModule("a", order: 0));
            registry.Register(new TestModule("first", order: -1));

            Assert.That(registry.Modules[0].Descriptor.Id, Is.EqualTo("first"));
            Assert.That(registry.Modules[1].Descriptor.Id, Is.EqualTo("a"));
            Assert.That(registry.Modules[2].Descriptor.Id, Is.EqualTo("z"));
        }

        [Test]
        public void Localization_UsesUserProjectEnglishAndKeyFallbackOrder()
        {
            var service = new EditorLocalizationService { ProjectDefaultLanguage = "zh-CN" };
            service.RegisterSource(new DictionaryEditorLocalizationSource(
                "test",
                new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["en"] = new Dictionary<string, string> { ["test.english"] = "English", ["test.shared"] = "EN" },
                    ["zh-CN"] = new Dictionary<string, string> { ["test.shared"] = "中文" }
                }));

            service.UserLanguageOverride = "ja-JP";

            Assert.That(service.Get("test.shared"), Is.EqualTo("中文"));
            Assert.That(service.Get("test.english"), Is.EqualTo("English"));
            Assert.That(service.Get("test.missing"), Is.EqualTo("test.missing"));
        }

        [Test]
        public void Localization_LastRegisteredSourceOverridesEarlierSource()
        {
            var service = new EditorLocalizationService();
            service.RegisterSource(Source("first"));
            var overrideHandle = service.RegisterSource(Source("second"));

            Assert.That(service.Get("test.value"), Is.EqualTo("second"));

            overrideHandle.Dispose();

            Assert.That(service.Get("test.value"), Is.EqualTo("first"));
        }

        [Test]
        public void Diagnostics_CountAndFilterBySeverityAndSearch()
        {
            var diagnostics = new EditorDiagnosticCollection();
            diagnostics.Add(new EditorDiagnostic("AK001", EditorDiagnosticSeverity.Error, "Broken node", "root.node"));
            diagnostics.Add(new EditorDiagnostic("AK002", EditorDiagnosticSeverity.Warning, "Missing label", "root.label"));
            diagnostics.Add(new EditorDiagnostic("AK003", EditorDiagnosticSeverity.Info, "Details", "root"));

            Assert.That(diagnostics.ErrorCount, Is.EqualTo(1));
            Assert.That(diagnostics.WarningCount, Is.EqualTo(1));
            Assert.That(diagnostics.HasErrors, Is.True);
            Assert.That(diagnostics.Filter(EditorDiagnosticSeverity.Warning), Has.Count.EqualTo(2));
            Assert.That(diagnostics.Filter(EditorDiagnosticSeverity.Info, "label"), Has.Count.EqualTo(1));
        }

        [Test]
        public void CommandRegistry_RespectsCanExecuteAndDisposableRegistration()
        {
            var registry = new EditorCommandRegistry();
            var executions = 0;
            var enabled = false;
            var command = new EditorCommand(
                "test.execute",
                "test.execute.label",
                _ => executions++,
                canExecute: _ => enabled);

            var handle = registry.Register(command);
            Assert.That(registry.Execute(command.Id), Is.False);

            enabled = true;
            Assert.That(registry.Execute(command.Id), Is.True);
            Assert.That(executions, Is.EqualTo(1));

            handle.Dispose();
            Assert.That(registry.TryGet(command.Id, out _), Is.False);
        }

        [Test]
        public void SearchState_MatchesCandidatesAndRaisesOnlyForChanges()
        {
            var state = new EditorSearchState();
            var changes = 0;
            state.Changed += () => changes++;

            Assert.That(state.Matches("Anything"), Is.True);
            state.Text = "node";
            state.Text = "node";

            Assert.That(state.Matches("Root Node", "Other"), Is.True);
            Assert.That(state.Matches("Root", "Other"), Is.False);
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void SplitterState_ClampsRestoresAndPersistsPosition()
        {
            var store = new MemoryUserStateStore();
            store.SetFloat("split", 900f);
            var state = new EditorSplitterState(250f, 100f, 500f, store, "split");

            Assert.That(state.Position, Is.EqualTo(500f));
            state.Position = 50f;

            Assert.That(state.Position, Is.EqualTo(100f));
            Assert.That(store.GetFloat("split"), Is.EqualTo(100f));
        }

        [Test]
        public void TabState_OrdersRestoresSelectsAndPersistsAvailableTab()
        {
            var store = new MemoryUserStateStore();
            store.SetString("selected", "disabled");
            var tabs = new[]
            {
                new EditorTabDescriptor("second", "second", order: 5),
                new EditorTabDescriptor("first", "first", order: 0),
                new EditorTabDescriptor("disabled", "disabled", order: -1, isEnabled: () => false)
            };

            var state = new EditorTabState(tabs, store: store, stateKey: "selected");

            Assert.That(state.SelectedId, Is.EqualTo("first"));
            Assert.That(state.Tabs[0].Id, Is.EqualTo("disabled"));
            Assert.That(state.Select("second"), Is.True);
            Assert.That(store.GetString("selected"), Is.EqualTo("second"));
            Assert.That(state.Select("disabled"), Is.False);
        }

        [Test]
        public void UiToolkitControls_ReflectLocalizationAndDiagnostics()
        {
            var localization = new EditorLocalizationService();
            localization.RegisterSource(new DictionaryEditorLocalizationSource(
                "ui",
                new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["en"] = new Dictionary<string, string>
                    {
                        ["title"] = "Title",
                        ["message"] = "Message",
                        ["status"] = "Errors",
                        ["abilitykit.editor.search.tooltip"] = "Search",
                        ["abilitykit.editor.diagnostics.empty.title"] = "Empty",
                        ["abilitykit.editor.diagnostics.empty.message"] = "No diagnostics",
                        ["abilitykit.editor.diagnostics.locate"] = "Locate",
                        ["abilitykit.editor.diagnostics.fix"] = "Fix"
                    }
                }));
            var empty = new EditorEmptyState(localization, "title", "message");
            var badge = new EditorStatusBadge(new EditorStatusBadgeModel("status", EditorStatusKind.Error, 2), localization);
            var diagnostics = new EditorDiagnosticCollection();
            var list = new EditorDiagnosticsList(diagnostics, localization);

            Assert.That(empty.childCount, Is.EqualTo(2));
            Assert.That(badge.text, Is.EqualTo("Errors 2"));
            Assert.That(list.childCount, Is.EqualTo(2));

            empty.Dispose();
            badge.Dispose();
            list.Dispose();
        }

        [Test]
        public void DocumentSession_UndoRedoTracksSavedBaselineAndBoundedHistory()
        {
            var session = new EditorDocumentSession<TestDocument>(new TestDocumentSerializer(), historyLimit: 2);
            session.Open(new TestDocument("0"));
            for (var i = 1; i <= 3; i++)
            {
                session.RecordChange();
                session.Document.Value = i.ToString();
            }

            Assert.That(session.IsDirty, Is.True);
            Assert.That(session.UndoCount, Is.EqualTo(2));
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.Document.Value, Is.EqualTo("2"));
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.Document.Value, Is.EqualTo("1"));
            Assert.That(session.Undo(), Is.False);
            Assert.That(session.Redo(), Is.True);
            Assert.That(session.Document.Value, Is.EqualTo("2"));

            session.MarkSaved();
            Assert.That(session.IsDirty, Is.False);
            session.RecordChange();
            session.Document.Value = "changed";
            Assert.That(session.DiscardChanges(), Is.True);
            Assert.That(session.Document.Value, Is.EqualTo("2"));
            Assert.That(session.IsDirty, Is.False);
        }

        [Test]
        public void DocumentSession_ReadOnlyRejectsHistoryAndSwitchCanCancelDiscardOrSave()
        {
            var serializer = new TestDocumentSerializer();
            var session = new EditorDocumentSession<TestDocument>(serializer);
            session.Open(new TestDocument("current"));
            session.RecordChange();
            session.Document.Value = "dirty";

            Assert.That(session.TrySwitch(
                new TestDocument("cancelled"),
                decide: _ => EditorDocumentSwitchDecision.Cancel), Is.False);
            Assert.That(session.Document.Value, Is.EqualTo("dirty"));

            var saved = string.Empty;
            Assert.That(session.TrySwitch(
                new TestDocument("next"),
                nextIsReadOnly: true,
                decide: _ => EditorDocumentSwitchDecision.Save,
                saveCurrent: document => saved = document.Value), Is.True);
            Assert.That(saved, Is.EqualTo("dirty"));
            Assert.That(session.Document.Value, Is.EqualTo("next"));
            Assert.That(session.IsReadOnly, Is.True);
            Assert.That(session.RecordChange(), Is.False);
            Assert.That(session.CanUndo, Is.False);

            session.Open(new TestDocument("discard-source"));
            session.RecordChange();
            session.Document.Value = "discard-me";
            Assert.That(session.TrySwitch(
                new TestDocument("discard-target"),
                decide: _ => EditorDocumentSwitchDecision.Discard), Is.True);
            Assert.That(session.Document.Value, Is.EqualTo("discard-target"));
        }

        [Test]
        public void DocumentSession_SwitchSaveRequiresCallbackAndCloseClearsOwnership()
        {
            var session = new EditorDocumentSession<TestDocument>(new TestDocumentSerializer());
            session.Open(new TestDocument("before"));
            session.RecordChange();
            session.Document.Value = "after";

            Assert.Throws<InvalidOperationException>(() => session.TrySwitch(
                new TestDocument("next"),
                decide: _ => EditorDocumentSwitchDecision.Save));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.TrySwitch(
                new TestDocument("invalid"),
                decide: _ => (EditorDocumentSwitchDecision)999));

            session.Open(new TestDocument(string.Empty));
            session.RecordChange();
            session.Document.Value = "non-empty";
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.Document.Value, Is.Empty);

            session.Close();
            Assert.That(session.IsOpen, Is.False);
            Assert.That(session.IsDirty, Is.False);
            Assert.That(session.CanUndo, Is.False);
            Assert.Throws<InvalidOperationException>(() => session.RecordChange());
        }

        [Test]
        public void SourceSyncClassifier_ClassifiesThreeWayChangesAndConvergence()
        {
            Assert.That(InspectSync("same", "same", "old").State, Is.EqualTo(EditorSourceSyncState.InSync));
            Assert.That(InspectSync("local", "base", "base").State, Is.EqualTo(EditorSourceSyncState.LocalChanged));
            Assert.That(InspectSync("base", "source", "base").State, Is.EqualTo(EditorSourceSyncState.SourceChanged));
            Assert.That(InspectSync("local", "source", "base").State, Is.EqualTo(EditorSourceSyncState.Conflict));

            var conflict = InspectSync("local", "source", "base");
            Assert.That(conflict.LocalChanged, Is.True);
            Assert.That(conflict.SourceChanged, Is.True);
            Assert.That(conflict.CanImportWithoutForce, Is.False);
            Assert.That(conflict.CanExportWithoutForce, Is.False);
        }

        [Test]
        public void SourceSyncClassifier_ClassifiesTrackingAndSourceFailures()
        {
            Assert.That(InspectSync("local", "source", "", isTracked: false).State,
                Is.EqualTo(EditorSourceSyncState.Untracked));
            Assert.That(InspectSync("local", "", "base", sourceExists: false).State,
                Is.EqualTo(EditorSourceSyncState.SourceMissing));
            Assert.That(InspectSync("local", "", "base", sourceIsValid: false).State,
                Is.EqualTo(EditorSourceSyncState.InvalidSource));
            Assert.That(InspectSync("local", "source", "").State,
                Is.EqualTo(EditorSourceSyncState.Untracked));
        }

        [Test]
        public void SourceSyncOperationPolicy_RequiresForceOnlyForDestructiveOverwrites()
        {
            AssertOperation(
                InspectSync("local", "source", "base"),
                EditorSourceSyncDirection.Import,
                EditorSourceSyncOperationDisposition.RequiresForce);
            AssertOperation(
                InspectSync("local", "source", "base"),
                EditorSourceSyncDirection.Export,
                EditorSourceSyncOperationDisposition.RequiresForce);
            AssertOperation(
                InspectSync("base", "source", "base"),
                EditorSourceSyncDirection.Import,
                EditorSourceSyncOperationDisposition.Allowed);
            AssertOperation(
                InspectSync("local", "base", "base"),
                EditorSourceSyncDirection.Export,
                EditorSourceSyncOperationDisposition.Allowed);
            AssertOperation(
                InspectSync("local", "source", "", isTracked: false),
                EditorSourceSyncDirection.Import,
                EditorSourceSyncOperationDisposition.RequiresForce,
                localHasAuthoredContent: true);
            AssertOperation(
                InspectSync("local", "source", "", isTracked: false),
                EditorSourceSyncDirection.Export,
                EditorSourceSyncOperationDisposition.RequiresForce);
        }

        [Test]
        public void SourceSyncOperationPolicy_BlocksUnreadableImportAndAllowsCreatingMissingExport()
        {
            AssertOperation(
                InspectSync("local", "", "base", sourceExists: false),
                EditorSourceSyncDirection.Import,
                EditorSourceSyncOperationDisposition.Blocked);
            AssertOperation(
                InspectSync("local", "", "base", sourceExists: false),
                EditorSourceSyncDirection.Export,
                EditorSourceSyncOperationDisposition.Allowed);
            AssertOperation(
                InspectSync("local", "", "base", sourceIsValid: false),
                EditorSourceSyncDirection.Import,
                EditorSourceSyncOperationDisposition.Blocked);
            AssertOperation(
                InspectSync("local", "", "base", sourceIsValid: false),
                EditorSourceSyncDirection.Export,
                EditorSourceSyncOperationDisposition.RequiresForce);
        }

        [Test]
        public void SourceSyncCardModel_ExposesMessagesAndPathCapabilities()
        {
            var copyCount = 0;
            var inspection = EditorSourceSyncClassifier.Inspect(
                new EditorSourceSyncSnapshot(
                    "local",
                    string.Empty,
                    "base",
                    isTracked: true,
                    sourceExists: true,
                    sourceIsValid: false,
                    sourcePath: "source.json",
                    error: "Malformed JSON."));

            var model = new EditorSourceSyncCardModel(
                inspection,
                import: null,
                export: () => { },
                copyPath: () => copyCount++);

            Assert.That(model.StatusMessage, Is.EqualTo("Malformed JSON."));
            Assert.That(model.HasSourcePath, Is.True);
            Assert.That(model.CanImport, Is.False);
            Assert.That(model.CanExport, Is.True);
            Assert.That(model.CanCopyPath, Is.True);
            Assert.That(model.CanRevealPath, Is.False);
            model.CopyPath();
            Assert.That(copyCount, Is.EqualTo(1));
        }

        [Test]
        public void SourceSyncCardModel_RequiresInspectionAndFallsBackForInvalidSourceError()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new EditorSourceSyncCardModel(null, null, null));

            var inspection = EditorSourceSyncClassifier.Inspect(
                new EditorSourceSyncSnapshot(
                    "local",
                    string.Empty,
                    "base",
                    isTracked: true,
                    sourceExists: true,
                    sourceIsValid: false));
            var model = new EditorSourceSyncCardModel(
                inspection,
                import: null,
                export: null,
                copyPath: () => { },
                revealPath: () => { },
                title: " ");

            Assert.That(model.Title, Is.EqualTo("Source Sync"));
            Assert.That(model.StatusMessage, Is.EqualTo("The source cannot be read."));
            Assert.That(model.HasSourcePath, Is.False);
            Assert.That(model.CanCopyPath, Is.False);
            Assert.That(model.CanRevealPath, Is.False);
        }

        [Test]
        public void SourceSyncCard_ReflectsModelStateAndCapabilities()
        {
            var inspection = EditorSourceSyncClassifier.Inspect(
                new EditorSourceSyncSnapshot(
                    "local",
                    "source",
                    "base",
                    isTracked: true,
                    sourceExists: true,
                    sourcePath: "source.json"));
            var card = new EditorSourceSyncCard(
                new EditorSourceSyncCardModel(
                    inspection,
                    import: () => { },
                    export: null,
                    copyPath: () => { },
                    revealPath: null));

            Assert.That(
                card.Q<Label>("source-sync-status").text,
                Is.EqualTo(EditorSourceSyncState.Conflict.ToString()));
            Assert.That(
                card.Q<Label>("source-sync-path").text,
                Is.EqualTo("source.json"));
            Assert.That(
                card.Q<Button>("source-sync-import").enabledSelf,
                Is.True);
            Assert.That(
                card.Q<Button>("source-sync-export").enabledSelf,
                Is.False);
            Assert.That(
                card.Q<Button>("source-sync-copy-path").enabledSelf,
                Is.True);
            Assert.That(
                card.Q<Button>("source-sync-reveal-path").enabledSelf,
                Is.False);
        }

        [Test]
        public void ExportReport_AggregatesStatusesArtifactsAndMessages()
        {
            var report = new EditorExportReport(new[]
            {
                new EditorExportReportEntry(
                    "write",
                    "tree-a",
                    EditorExportStatus.Exported,
                    new[] { new EditorExportArtifact("tree-a.json", "json") }),
                new EditorExportReportEntry(
                    "same",
                    "tree-b",
                    EditorExportStatus.Unchanged,
                    new[] { new EditorExportArtifact("tree-b.json", "json") }),
                new EditorExportReportEntry(
                    "skip",
                    "tree-c",
                    EditorExportStatus.Skipped,
                    messages: new[] { "No export target." })
            });

            Assert.That(report.Success, Is.True);
            Assert.That(report.ExportedCount, Is.EqualTo(1));
            Assert.That(report.UnchangedCount, Is.EqualTo(1));
            Assert.That(report.SkippedCount, Is.EqualTo(1));
            Assert.That(report.FailedCount, Is.Zero);
            Assert.That(report.Artifacts, Has.Count.EqualTo(2));
            Assert.That(report.Messages, Is.EquivalentTo(new[] { "No export target." }));
        }

        [Test]
        public void ExportExecutor_ConvertsExceptionsAndNullResultsToFailures()
        {
            var report = EditorExportExecutor.Execute(new[]
            {
                new EditorExportJob(
                    "throws",
                    "tree-a",
                    "json",
                    () => throw new InvalidOperationException("Cannot serialize.")),
                new EditorExportJob(
                    "null",
                    "tree-b",
                    "json",
                    () => null)
            });

            Assert.That(report.Success, Is.False);
            Assert.That(report.HasFailures, Is.True);
            Assert.That(report.FailedCount, Is.EqualTo(2));
            Assert.That(report.Messages, Does.Contain("Cannot serialize."));
            Assert.That(report.Messages, Does.Contain("The export job returned no result."));
        }

        [Test]
        public void AtomicFileWriter_WritesAndDetectsUnchangedContent()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "AbilityKitEditorExportTests",
                Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "report.json");
            try
            {
                Assert.That(
                    EditorAtomicFileWriter.WriteAllText(path, "first"),
                    Is.EqualTo(EditorAtomicWriteStatus.Written));
                Assert.That(File.ReadAllText(path), Is.EqualTo("first"));
                Assert.That(
                    EditorAtomicFileWriter.WriteAllText(path, "first"),
                    Is.EqualTo(EditorAtomicWriteStatus.Unchanged));
                Assert.That(
                    EditorAtomicFileWriter.WriteAllText(path, "second"),
                    Is.EqualTo(EditorAtomicWriteStatus.Written));
                Assert.That(File.ReadAllText(path), Is.EqualTo("second"));
                Assert.That(
                    Directory.GetFiles(directory, "*.abilitykit.*"),
                    Is.Empty);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        private static void AssertOperation(
            EditorSourceSyncInspection inspection,
            EditorSourceSyncDirection direction,
            EditorSourceSyncOperationDisposition expected,
            bool localHasAuthoredContent = false)
        {
            var assessment = EditorSourceSyncOperationPolicy.Assess(
                inspection,
                direction,
                localHasAuthoredContent);
            Assert.That(assessment.Disposition, Is.EqualTo(expected));
            Assert.That(assessment.CanExecute,
                Is.EqualTo(expected != EditorSourceSyncOperationDisposition.Blocked));
            Assert.That(assessment.RequiresForce,
                Is.EqualTo(expected == EditorSourceSyncOperationDisposition.RequiresForce));
        }

        private static EditorSourceSyncInspection InspectSync(
            string localHash,
            string sourceHash,
            string baselineHash,
            bool isTracked = true,
            bool sourceExists = true,
            bool sourceIsValid = true)
        {
            return EditorSourceSyncClassifier.Inspect(new EditorSourceSyncSnapshot(
                localHash,
                sourceHash,
                baselineHash,
                isTracked,
                sourceExists,
                sourceIsValid));
        }

        private static DictionaryEditorLocalizationSource Source(string value)
        {
            return new DictionaryEditorLocalizationSource(
                "test",
                new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["en"] = new Dictionary<string, string> { ["test.value"] = value }
                });
        }

        private sealed class TestDocument
        {
            public TestDocument(string value)
            {
                Value = value;
            }

            public string Value { get; set; }
        }

        private sealed class TestDocumentSerializer : IEditorDocumentSerializer<TestDocument>
        {
            public string Serialize(TestDocument document) => document.Value;
            public TestDocument Deserialize(string snapshot) => new TestDocument(snapshot);
        }

        private sealed class MemoryUserStateStore : IEditorUserStateStore
        {
            private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

            public bool HasKey(string key) => _values.ContainsKey(key);
            public string GetString(string key, string defaultValue = "") => Get(key, defaultValue);
            public void SetString(string key, string value) => _values[key] = value;
            public int GetInt(string key, int defaultValue = 0) => Get(key, defaultValue);
            public void SetInt(string key, int value) => _values[key] = value;
            public float GetFloat(string key, float defaultValue = 0f) => Get(key, defaultValue);
            public void SetFloat(string key, float value) => _values[key] = value;
            public bool GetBool(string key, bool defaultValue = false) => Get(key, defaultValue);
            public void SetBool(string key, bool value) => _values[key] = value;
            public void DeleteKey(string key) => _values.Remove(key);

            private T Get<T>(string key, T defaultValue)
            {
                return _values.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;
            }
        }

        private sealed class TestModule : IEditorModule
        {
            public TestModule(string id, int order)
            {
                Descriptor = new EditorModuleDescriptor(id, id + ".name", order);
            }

            public EditorModuleDescriptor Descriptor { get; }
            public int RegisterCount { get; private set; }
            public int UnregisterCount { get; private set; }

            public void OnRegister(IEditorPlatformContext context)
            {
                RegisterCount++;
            }

            public void OnUnregister()
            {
                UnregisterCount++;
            }
        }
    }
}
#endif
