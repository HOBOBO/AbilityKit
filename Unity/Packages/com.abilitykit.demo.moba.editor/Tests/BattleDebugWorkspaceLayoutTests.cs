using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class BattleDebugWorkspaceLayoutTests
    {
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
                "frame-sync",
                out var frameSync), Is.True);
            Assert.That(frameSync.PrimaryModuleId, Is.EqualTo(BattleDebugModuleIds.FrameSyncPrediction));
            Assert.That(frameSync.SecondaryModuleId, Is.EqualTo(BattleDebugModuleIds.FrameSyncNetwork));
        }

        [Test]
        public void EventsAndTrace_ExposeComposableWidgetsWithStableIds()
        {
            var events = (IBattleDebugWidgetProvider)new BattleDebugDiagnosticEventsPanel();
            Assert.That(events.Widgets.Count, Is.EqualTo(3));
            Assert.That(events.Widgets[0].StableId, Is.EqualTo(BattleDebugWidgetIds.EventsOverview));
            Assert.That(events.Widgets[1].StableId, Is.EqualTo(BattleDebugWidgetIds.EventsList));
            Assert.That(events.Widgets[2].StableId, Is.EqualTo(BattleDebugWidgetIds.EventsDetails));

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
