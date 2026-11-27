using System;
using System.IO;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Editor.BattleScenario;
using AbilityKit.Game.Editor;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class BattleDebugWorkspaceLayoutTests
    {
        [Test]
        public void ScenarioRunCatalog_ListsPublishedBatchesWithoutOpeningEvidence()
        {
            var root = Path.Combine(Path.GetTempPath(), "moba-run-catalog-" + Guid.NewGuid().ToString("N"));
            try
            {
                WriteRun(root, "run-01");
                WriteRun(root, "run-02");
                WriteRun(root, ".writing-run-03");
                var broken = Directory.CreateDirectory(Path.Combine(root, "broken"));
                File.WriteAllText(Path.Combine(broken.FullName, "batch_summary_v1.json"), "{");

                var catalog = new BattleDebugScenarioRunCatalog();
                catalog.Refresh(root);
                Assert.That(catalog.UnreadableCount, Is.EqualTo(1));
                Assert.That(catalog.Runs.Count, Is.EqualTo(2));
                Assert.That(catalog.Runs[0].runId, Is.EqualTo("run-02"));
                Assert.That(catalog.Find("run-01")?.runId, Is.EqualTo("run-01"));
                Assert.That(catalog.Find(".writing-run-03"), Is.Null);
                var run = catalog.Runs[0];
                Assert.That(run.cases[0].status, Is.EqualTo("failed"));
                Assert.Throws<FileNotFoundException>(() => catalog.ResolveFile(run, run.cases[0].diagnosticUri));
                Assert.Throws<InvalidDataException>(() => catalog.ResolveFile(run, "../run-01/batch_summary_v1.json"));
                Assert.Throws<InvalidDataException>(() => catalog.ResolveFile(run, "C:\\outside.json"));
                Assert.That(catalog.ResolveFile(run, "batch_summary_v1.json"),
                    Is.EqualTo(Path.Combine(root, "run-02", "batch_summary_v1.json")));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void ScenarioRunArchive_KeepsPresetsSeparateAndDoesNotCreateLibraryOnBrowse()
        {
            var parent = Path.Combine(Path.GetTempPath(), "moba-analysis-" + Guid.NewGuid().ToString("N"));
            var presets = Path.Combine(parent, "presets");
            var output = Path.Combine(parent, "presets-library");
            Assert.DoesNotThrow(() => BattleDebugScenarioRuns.ValidateBatchDirectories(presets, output));
            Assert.Throws<IOException>(() => BattleDebugScenarioRuns.ValidateBatchDirectories(presets, presets));
            Assert.Throws<IOException>(() => BattleDebugScenarioRuns.ValidateBatchDirectories(presets, Path.Combine(presets, "output")));

            var catalog = new BattleDebugScenarioRunCatalog();
            catalog.Refresh(output);
            Assert.That(catalog.Runs, Is.Empty);
            Assert.That(Directory.Exists(output), Is.False);
            Assert.That(BattleDebugScenarioRuns.DefaultAnalysisRoot(), Does.EndWith(Path.Combine("BattleAnalysis", "MOBA", "runs")));
        }

        [Test, Explicit("需要本仓库预设和 .NET SDK；仅由 Console 执行实战。")]
        public void ScenarioRunArchive_ConsoleHostPublishesSevenHeroBatch()
        {
            var presets = MobaBattleScenarioRunner.DefaultArchivePresetDirectory();
            var root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "local", "Logs",
                "moba-scenario-editor-verification", "analysis library " + Guid.NewGuid().ToString("N")));
            var cases = new[] { "annie-q", "gragas-q", "jhin-q", "jinx-w", "karthus-q-two-targets", "lux-q-two-targets", "veigar-q-two-targets" };
            var execution = MobaBattleScenarioRunner.ExecuteArchiveBatch(presets, root, "all", cases).GetAwaiter().GetResult();
            Assert.That(execution.Error, Is.Null, execution.LogDirectory);
            Assert.That(execution.ExitCode, Is.EqualTo(0), execution.LogDirectory);

            var catalog = new BattleDebugScenarioRunCatalog();
            catalog.Refresh(root);
            var run = catalog.Find(execution.RunId);
            Assert.That(run, Is.Not.Null);
            Assert.That(run.host, Is.EqualTo("dotnet-console"));
            Assert.That(run.counts.passed, Is.EqualTo(cases.Length));
            foreach (var entry in run.cases)
            {
                Assert.That(entry.evidenceStatus, Is.EqualTo("available"));
                Assert.That(File.Exists(catalog.ResolveFile(run, entry.diagnosticUri)), Is.True);
            }
            TestContext.WriteLine($"Analysis library: {root}\nConsole logs: {execution.LogDirectory}");
        }

        private static void WriteRun(string root, string runId)
        {
            var directory = Directory.CreateDirectory(Path.Combine(root, runId));
            File.WriteAllText(Path.Combine(directory.FullName, "batch_summary_v1.json"),
                "{\"schemaVersion\":1,\"suiteId\":\"moba-battle-scenario\",\"runId\":\"" + runId +
                "\",\"counts\":{\"total\":1,\"failed\":1},\"cases\":[{\"caseId\":\"sample\",\"status\":\"failed\"," +
                "\"diagnosticUri\":\"missing.json\"}]}");
        }

        [Test]
        public void ModuleCatalog_ExplainsUnsupportedSourceAndMissingData()
        {
            var live = new BattleDebugContext(null, default, null, null);
            var offline = new BattleDebugContext(null, default, null, null, isOffline: true);
            var metadata = new MetadataPanel();
            Assert.That(BattleDebugModuleCatalog.GetUnavailableReason(metadata, in live), Is.EqualTo("不支持实时来源"));
            Assert.That(BattleDebugModuleCatalog.GetUnavailableReason(metadata, in offline), Is.EqualTo("当前来源未提供所需诊断数据"));
            Assert.That(BattleDebugModuleCatalog.GetUnavailableReason(new BattleDebugTagsPanel(), in offline), Is.Null);

            var attributes = new BattleDebugAttributesPanel();
            Assert.That(BattleDebugModuleCatalog.GetStableId(attributes), Is.EqualTo(BattleDebugModuleIds.ActorAttributes));
            Assert.That(BattleDebugNavigation.GroupOf(attributes), Is.EqualTo(BattleDebugNavigationGroup.Actor));
        }

        [Test]
        public void TraceAttributeRoute_RestoresTheNodeAndEvidenceFrameThroughHistory()
        {
            var scope = new BattleDiagnosticSessionScope("session", "world", 1);
            var otherScope = new BattleDiagnosticSessionScope("session", "world", 2);
            var node = new BattleDiagnosticTraceNodeSummary(scope, 100, 101, 100, 0, 9,
                default, actorId: 11, targetActorId: 12);
            Assert.That(BattleDebugNavigation.TryCreateTraceAttributeRoute(otherScope, in node, 12, out _, out _), Is.False);
            Assert.That(BattleDebugNavigation.TryCreateTraceAttributeRoute(scope, in node, 13, out _, out _), Is.False);
            Assert.That(BattleDebugNavigation.TryCreateTraceAttributeRoute(scope, in node, 12, out var origin, out var target), Is.True);
            var workspace = new BattleDiagnosticWorkspaceState();
            workspace.AttachSession(scope, 20);
            workspace.Select(origin);
            workspace.Select(target);
            Assert.That(workspace.Selection.Kind, Is.EqualTo(BattleDiagnosticSelectionKind.ActorAttributeEvidence));
            Assert.That(workspace.Selection.Id, Is.EqualTo(12));
            Assert.That(workspace.Selection.RelatedId, Is.EqualTo(101));
            Assert.That(workspace.FrameCursor.Frame, Is.Zero);
            Assert.That(workspace.GoBack(), Is.True);
            Assert.That(workspace.Selection.Kind, Is.EqualTo(BattleDiagnosticSelectionKind.TraceNode));
            Assert.That(workspace.Selection.Id, Is.EqualTo(101));
            Assert.That(workspace.GoForward(), Is.True);
            Assert.That(workspace.Selection, Is.EqualTo(target));
        }

        [Test]
        public void ModuleCatalog_UsesMetadataForStableIdentityAndCapabilities()
        {
            var descriptor = BattleDebugModuleCatalog.Describe(new MetadataPanel());

            Assert.That(descriptor.StableId, Is.EqualTo("tests.metadata"));
            Assert.That(descriptor.Category, Is.EqualTo("Tests"));
            Assert.That(descriptor.RequiredCapabilities, Is.EqualTo(BattleDiagnosticCapabilities.Events));
            Assert.That(descriptor.SupportsSource(false), Is.False);
            Assert.That(descriptor.SupportsSource(true), Is.True);
            Assert.That(
                descriptor.Selections,
                Is.EqualTo(BattleDebugModuleSelectionSupport.Event));
        }

        [Test]
        public void BuiltInPresets_KeepStableModuleComposition()
        {
            Assert.That(BattleDebugWorkspacePresets.TryGet(
                "combat-investigation",
                out var combat), Is.True);
            Assert.That(combat.PrimaryModuleId, Is.EqualTo(BattleDebugModuleIds.DiagnosticEvents));
            Assert.That(combat.PrimaryWidgetId, Is.EqualTo(BattleDebugWidgetIds.EventsList));
            Assert.That(combat.SecondaryModuleId, Is.EqualTo(BattleDebugModuleIds.DiagnosticTrace));
            Assert.That(combat.SecondaryWidgetId, Is.EqualTo(BattleDebugWidgetIds.TraceWaterfall));
            Assert.That(combat.ShowsSecondary, Is.True);

            Assert.That(BattleDebugWorkspacePresets.TryGet(
                "trigger-investigation",
                out var triggering), Is.True);
            Assert.That(triggering.PrimaryModuleId, Is.EqualTo(BattleDebugModuleIds.DiagnosticEvents));
            Assert.That(triggering.PrimaryWidgetId, Is.EqualTo(BattleDebugWidgetIds.EventsTriggerFlow));
            Assert.That(triggering.SecondaryModuleId, Is.EqualTo(BattleDebugModuleIds.DiagnosticEvents));
            Assert.That(triggering.SecondaryWidgetId, Is.EqualTo(BattleDebugWidgetIds.EventsDetails));

            Assert.That(BattleDebugWorkspacePresets.TryGet(
                "frame-sync",
                out var frameSync), Is.True);
            Assert.That(frameSync.PrimaryModuleId, Is.EqualTo(BattleDebugModuleIds.FrameSyncPrediction));
            Assert.That(frameSync.SecondaryModuleId, Is.EqualTo(BattleDebugModuleIds.FrameSyncNetwork));
        }

        [Test]
        public void NavigationGroups_ClassifyModulesAndPreserveSavedValues()
        {
            Assert.That((int)BattleDebugNavigationGroup.Actor, Is.EqualTo(0));
            Assert.That((int)BattleDebugNavigationGroup.Investigation, Is.EqualTo(1));
            Assert.That((int)BattleDebugNavigationGroup.FrameSync, Is.EqualTo(2));
            Assert.That(BattleDebugNavigation.OrderedGroups, Is.EqualTo(new[]
            {
                BattleDebugNavigationGroup.Actor,
                BattleDebugNavigationGroup.Runtime,
                BattleDebugNavigationGroup.Investigation,
                BattleDebugNavigationGroup.FrameSync,
                BattleDebugNavigationGroup.Session
            }));
            Assert.That(BattleDebugNavigation.UseCompactNavigation(500f), Is.True);
            Assert.That(BattleDebugNavigation.UseCompactNavigation(800f), Is.False);
            Assert.That(BattleDebugNavigation.GroupOf(new BattleDebugAttributesPanel()),
                Is.EqualTo(BattleDebugNavigationGroup.Actor));
            Assert.That(BattleDebugNavigation.GroupOf(new BattleDebugDiagnosticEventsPanel()),
                Is.EqualTo(BattleDebugNavigationGroup.Investigation));
            Assert.That(BattleDebugNavigation.GroupOf(new BattleDebugRuntimeObjectsPanel()),
                Is.EqualTo(BattleDebugNavigationGroup.Runtime));
            Assert.That(BattleDebugNavigation.GroupOf(new BattleDebugSkillRuntimesPanel()),
                Is.EqualTo(BattleDebugNavigationGroup.Runtime));
            Assert.That(BattleDebugNavigation.GroupOf(new BattleDebugActiveObjectsPanel()),
                Is.EqualTo(BattleDebugNavigationGroup.Runtime));
            Assert.That(BattleDebugNavigation.GroupOf(new BattleDebugDiagnosticStatePanel()),
                Is.EqualTo(BattleDebugNavigationGroup.Session));
            Assert.That(BattleDebugNavigation.GroupOf(new BattleDebugDiagnosticsHealthPanel()),
                Is.EqualTo(BattleDebugNavigationGroup.Session));
            Assert.That(BattleDebugNavigation.GroupOf(new BattleDebugFrameSyncPredictionPanel()),
                Is.EqualTo(BattleDebugNavigationGroup.FrameSync));
            Assert.That(BattleDebugNavigation.GroupOfModuleId(BattleDebugModuleIds.SkillRuntimes),
                Is.EqualTo(BattleDebugNavigationGroup.Runtime));
            Assert.That(BattleDebugNavigation.GroupOfModuleId(BattleDebugModuleIds.DiagnosticHealth),
                Is.EqualTo(BattleDebugNavigationGroup.Session));
            Assert.That(BattleDebugNavigation.GroupOfModuleId(BattleDebugModuleIds.FrameSyncNetwork),
                Is.EqualTo(BattleDebugNavigationGroup.FrameSync));
            Assert.That(BattleDebugNavigation.GroupOfModuleId(BattleDebugModuleIds.DiagnosticTrace),
                Is.EqualTo(BattleDebugNavigationGroup.Investigation));
            Assert.That(BattleDebugNavigation.NavigationName(new BattleDebugFrameSyncPredictionPanel()),
                Is.EqualTo("预测"));
        }

        [Test]
        public void PaneLayout_KeepsActorAndDiagnosticVisibilityIndependent()
        {
            Assert.That(BattleDebugNavigation.GroupOf(new BattleDebugTagsPanel()),
                Is.EqualTo(BattleDebugNavigationGroup.Actor));
            Assert.That(BattleDebugPaneLayout.ShowEntityPane(
                BattleDebugNavigationGroup.Actor, true, false), Is.True);
            Assert.That(BattleDebugPaneLayout.ShowEntityPane(
                BattleDebugNavigationGroup.Investigation, true, false), Is.False);
            Assert.That(BattleDebugPaneLayout.ShowEntityPane(
                BattleDebugNavigationGroup.FrameSync, true, false), Is.False);
            Assert.That(BattleDebugPaneLayout.ShowEntityPane(
                BattleDebugNavigationGroup.Runtime, true, false), Is.False);
            Assert.That(BattleDebugPaneLayout.ShowEntityPane(
                BattleDebugNavigationGroup.Session, true, false), Is.False);
            Assert.That(BattleDebugPaneLayout.ShowEntityPane(
                BattleDebugNavigationGroup.Investigation, false, true), Is.True);
            Assert.That(BattleDebugPaneLayout.ShowInspector(
                BattleDebugNavigationGroup.Actor, false, true), Is.False);
            Assert.That(BattleDebugPaneLayout.ShowInspector(
                BattleDebugNavigationGroup.FrameSync, false, true), Is.True);
        }

        [Test]
        public void EventsAndTrace_ExposeComposableWidgetsWithStableIds()
        {
            var events = (IBattleDebugWidgetProvider)new BattleDebugDiagnosticEventsPanel();
            Assert.That(events.Widgets.Count, Is.EqualTo(4));
            Assert.That(events.Widgets[0].StableId, Is.EqualTo(BattleDebugWidgetIds.EventsOverview));
            Assert.That(events.Widgets[1].StableId, Is.EqualTo(BattleDebugWidgetIds.EventsTriggerFlow));
            Assert.That(events.Widgets[2].StableId, Is.EqualTo(BattleDebugWidgetIds.EventsList));
            Assert.That(events.Widgets[3].StableId, Is.EqualTo(BattleDebugWidgetIds.EventsDetails));

            var trace = (IBattleDebugWidgetProvider)new BattleDebugDiagnosticTracePanel();
            Assert.That(trace.Widgets.Count, Is.EqualTo(3));
            Assert.That(trace.Widgets[0].StableId, Is.EqualTo(BattleDebugWidgetIds.TraceTree));
            Assert.That(trace.Widgets[1].StableId, Is.EqualTo(BattleDebugWidgetIds.TraceWaterfall));
            Assert.That(trace.Widgets[2].StableId, Is.EqualTo(BattleDebugWidgetIds.TraceDetails));
        }

        [Test]
        public void HistogramProjection_MapsInclusiveBoundaryFramesToValidBins()
        {
            var range = new BattleDiagnosticFrameRange(10, 19);

            Assert.That(BattleDebugHistogram.MapFrameToBin(10, range, 4), Is.EqualTo(0));
            Assert.That(BattleDebugHistogram.MapFrameToBin(13, range, 4), Is.EqualTo(1));
            Assert.That(BattleDebugHistogram.MapFrameToBin(19, range, 4), Is.EqualTo(3));
        }

        [Test]
        public void TimelineOverview_ProjectsPointsAndSpansIntoDensityBins()
        {
            var items = new[]
            {
                new BattleDebugTimelineOverviewItem(10, 10),
                new BattleDebugTimelineOverviewItem(12, 16),
                new BattleDebugTimelineOverviewItem(14, 15),
                new BattleDebugTimelineOverviewItem(19, 19)
            };
            var counts = new int[8];

            BattleDebugTimelineOverview.ProjectDensity(
                items,
                new BattleDiagnosticFrameRange(10, 19),
                counts,
                5,
                out var peak);

            Assert.That(counts[0], Is.EqualTo(1));
            Assert.That(counts[1], Is.EqualTo(1));
            Assert.That(counts[2], Is.EqualTo(2));
            Assert.That(counts[3], Is.EqualTo(1));
            Assert.That(counts[4], Is.EqualTo(1));
            Assert.That(peak, Is.EqualTo(2));
        }

        [Test]
        public void TimelineOverview_CalculatesLoadedRangeIntersectionPercent()
        {
            var loadedRange = new BattleDiagnosticFrameRange(10, 19);

            Assert.That(
                BattleDebugTimelineOverview.CalculateVisiblePercent(
                    loadedRange,
                    new BattleDiagnosticFrameRange(15, 24)),
                Is.EqualTo(50f));
            Assert.That(
                BattleDebugTimelineOverview.CalculateVisiblePercent(
                    loadedRange,
                    new BattleDiagnosticFrameRange(20, 30)),
                Is.Zero);
        }

        [Test]
        public void RuntimeObjectTimeline_UsesOnlyKnownFramesForPointOrSpan()
        {
            Assert.That(BattleDebugTraceObjectSpan.TryProject(0, 9, -1, out var start, out var end, out var point), Is.True);
            Assert.That((start, end, point), Is.EqualTo((0, 9, false)));
            Assert.That(BattleDebugTraceObjectSpan.TryProject(3, -1, -1, out start, out end, out point), Is.True);
            Assert.That((start, end, point), Is.EqualTo((3, 3, true)));
            Assert.That(BattleDebugTraceObjectSpan.TryProject(-1, 7, 3, out start, out end, out point), Is.True);
            Assert.That((start, end, point), Is.EqualTo((7, 7, true)));
            Assert.That(BattleDebugTraceObjectSpan.TryProject(-1, -1, 4, out start, out end, out point), Is.True);
            Assert.That((start, end, point), Is.EqualTo((4, 4, true)));
            Assert.That(BattleDebugTraceObjectSpan.TryProject(-1, -1, -1, out _, out _, out _), Is.False);
        }

        [Test]
        public void WaterfallProjection_ClipsPartialSpanAndHidesDisjointSpan()
        {
            var visibleRange = new BattleDiagnosticFrameRange(10, 20);

            Assert.That(
                BattleDebugWaterfall.TryClipSpan(5, 15, visibleRange, out var clipped),
                Is.True);
            Assert.That(clipped, Is.EqualTo(new BattleDiagnosticFrameRange(10, 15)));
            Assert.That(
                BattleDebugWaterfall.TryClipSpan(21, 30, visibleRange, out _),
                Is.False);
        }

        [Test]
        public void TimelineProjection_MapsNormalizedEdgesToInclusiveFrames()
        {
            var range = new BattleDiagnosticFrameRange(10, 19);

            Assert.That(
                BattleDebugTimelineInteraction.FrameAtNormalizedPosition(range, 0d),
                Is.EqualTo(10));
            Assert.That(
                BattleDebugTimelineInteraction.FrameAtNormalizedPosition(range, 0.1d),
                Is.EqualTo(11));
            Assert.That(
                BattleDebugTimelineInteraction.FrameAtNormalizedPosition(range, 1d),
                Is.EqualTo(19));
        }

        [Test]
        public void TimelineInteraction_Apply_UpdatesSharedRangeWithInteractionReason()
        {
            var workspace = new BattleDiagnosticWorkspaceState();
            workspace.AttachSession(
                new BattleDiagnosticSessionScope("session", "world", 1),
                100);
            var interaction = new BattleDebugTimelineInteractionResult(
                BattleDebugTimelineInteractionKind.SelectRange,
                BattleDiagnosticFrames.Invalid,
                new BattleDiagnosticFrameRange(20, 40));

            var changed = BattleDebugTimelineInteraction.Apply(workspace, interaction);

            Assert.That(changed, Is.True);
            Assert.That(workspace.TimeRange.Range, Is.EqualTo(new BattleDiagnosticFrameRange(20, 40)));
            Assert.That(
                workspace.TimeRange.ChangeReason,
                Is.EqualTo(BattleDiagnosticTimeRangeChangeReason.Brushed));
        }

        [BattleDebugModule(
            "tests.metadata",
            "Tests",
            RequiredCapabilities = BattleDiagnosticCapabilities.Events,
            Sources = BattleDebugModuleSourceSupport.Offline,
            Selections = BattleDebugModuleSelectionSupport.Event)]
        private sealed class MetadataPanel : IBattleDebugPanel
        {
            public string Name => "Metadata";
            public int Order => 1;

            public bool IsVisible(in BattleDebugContext context) => true;

            public void Draw(in BattleDebugContext context)
            {
            }
        }
    }
}
