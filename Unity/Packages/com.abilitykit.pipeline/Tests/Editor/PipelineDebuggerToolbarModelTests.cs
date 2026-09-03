#if UNITY_EDITOR

using NUnit.Framework;

namespace AbilityKit.Pipeline.Editor.Tests
{
    public sealed class PipelineDebuggerToolbarModelTests
    {
        [Test]
        public void Rebuild_ProjectsStatsAndToolbarState()
        {
            var model = new PipelineDebuggerToolbarModel();
            var stats = new EditorPipelineRegistry.DebugStats(
                total: 9,
                active: 2,
                history: 7,
                failed: 3,
                pinned: 1);

            model.Rebuild(
                stats,
                captureEnabled: false,
                followLatest: false,
                relativeTraceTime: false,
                confirmInterrupt: false,
                historyCapacity: 512,
                traceCapacity: 8192,
                refreshIntervalSeconds: 0.25f);

            Assert.That(model.CaptureEnabled, Is.False);
            Assert.That(model.FollowLatest, Is.False);
            Assert.That(model.RelativeTraceTime, Is.False);
            Assert.That(model.ConfirmInterrupt, Is.False);
            Assert.That(model.HistoryCapacity, Is.EqualTo(512));
            Assert.That(model.TraceCapacity, Is.EqualTo(8192));
            Assert.That(model.RefreshIntervalSeconds, Is.EqualTo(0.25f));
            Assert.That(
                model.StatsText,
                Is.EqualTo("Runs 9  |  Active 2  |  Failed 3  |  Pinned 1"));
            Assert.That(model.CanClearHistory, Is.True);
        }

        [Test]
        public void Rebuild_DisablesClearWhenHistoryIsEmpty()
        {
            var model = new PipelineDebuggerToolbarModel();

            model.Rebuild(
                new EditorPipelineRegistry.DebugStats(2, 2, 0, 0, 0),
                true,
                true,
                true,
                true,
                128,
                2048,
                0.1f);

            Assert.That(model.CanClearHistory, Is.False);
        }

        [Test]
        public void ToggleSetters_ReportChangesAndUpdateState()
        {
            var model = CreateDefaultModel();

            Assert.That(model.SetCaptureEnabled(true), Is.False);
            Assert.That(model.SetCaptureEnabled(false), Is.True);
            Assert.That(model.CaptureEnabled, Is.False);
            Assert.That(model.SetFollowLatest(true), Is.False);
            Assert.That(model.SetFollowLatest(false), Is.True);
            Assert.That(model.FollowLatest, Is.False);
            Assert.That(model.ToggleRelativeTraceTime(), Is.False);
            Assert.That(model.ToggleConfirmInterrupt(), Is.False);
        }

        [Test]
        public void CapacityAndRefreshOptions_PreserveSupportedValues()
        {
            var model = CreateDefaultModel();

            CollectionAssert.AreEqual(
                new[] { 32, 128, 512, 2048 },
                model.HistoryCapacities);
            CollectionAssert.AreEqual(
                new[] { 512, 2048, 8192, 32768 },
                model.TraceCapacities);
            CollectionAssert.AreEqual(
                new[] { 0.05f, 0.1f, 0.25f },
                model.RefreshIntervals);

            model.SetHistoryCapacity(2048);
            model.SetTraceCapacity(32768);
            model.SetRefreshInterval(0.05f);

            Assert.That(model.HistoryCapacity, Is.EqualTo(2048));
            Assert.That(model.TraceCapacity, Is.EqualTo(32768));
            Assert.That(model.IsRefreshIntervalSelected(0.05f), Is.True);
            Assert.That(model.IsRefreshIntervalSelected(0.1f), Is.False);
        }

        [Test]
        public void UnsupportedCapacityAndRefreshValues_AreRejected()
        {
            var model = CreateDefaultModel();

            Assert.That(
                () => model.SetHistoryCapacity(64),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(
                () => model.SetTraceCapacity(1024),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(
                () => model.SetRefreshInterval(0.2f),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void ResetOptions_RestoresExistingWindowDefaults()
        {
            var model = new PipelineDebuggerToolbarModel();
            model.Rebuild(
                new EditorPipelineRegistry.DebugStats(0, 0, 0, 0, 0),
                false,
                false,
                false,
                false,
                2048,
                32768,
                0.25f);

            model.ResetOptions();

            Assert.That(model.CaptureEnabled, Is.True);
            Assert.That(model.FollowLatest, Is.True);
            Assert.That(model.RelativeTraceTime, Is.True);
            Assert.That(model.ConfirmInterrupt, Is.True);
            Assert.That(
                model.HistoryCapacity,
                Is.EqualTo(PipelineDebuggerToolbarModel.DefaultHistoryCapacity));
            Assert.That(
                model.TraceCapacity,
                Is.EqualTo(PipelineDebuggerToolbarModel.DefaultTraceCapacity));
            Assert.That(
                model.RefreshIntervalSeconds,
                Is.EqualTo(PipelineDebuggerWorkspaceState.DefaultRefreshIntervalSeconds));
        }

        [Test]
        public void Rebuild_ClampsRefreshIntervalToWorkspaceBounds()
        {
            var model = CreateDefaultModel();
            var stats = new EditorPipelineRegistry.DebugStats(0, 0, 0, 0, 0);

            model.Rebuild(stats, true, true, true, true, 128, 2048, -1f);
            Assert.That(
                model.RefreshIntervalSeconds,
                Is.EqualTo(PipelineDebuggerWorkspaceState.MinRefreshIntervalSeconds));

            model.Rebuild(stats, true, true, true, true, 128, 2048, 2f);
            Assert.That(
                model.RefreshIntervalSeconds,
                Is.EqualTo(PipelineDebuggerWorkspaceState.MaxRefreshIntervalSeconds));
        }

        private static PipelineDebuggerToolbarModel CreateDefaultModel()
        {
            var model = new PipelineDebuggerToolbarModel();
            model.Rebuild(
                new EditorPipelineRegistry.DebugStats(0, 0, 0, 0, 0),
                true,
                true,
                true,
                true,
                128,
                2048,
                0.1f);
            return model;
        }
    }
}

#endif
