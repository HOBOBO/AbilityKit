#if UNITY_EDITOR

using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Pipeline.Editor.Tests
{
    public sealed class PipelineDebuggerWorkspaceStateTests
    {
        private PipelineDebuggerUserState _userState = null!;

        [SetUp]
        public void SetUp()
        {
            _userState = ScriptableObject.CreateInstance<PipelineDebuggerUserState>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_userState);
        }

        [Test]
        public void Constructor_UsesWorkspaceDefaults()
        {
            var state = new PipelineDebuggerWorkspaceState();

            AssertDefaults(state);
        }

        [Test]
        public void Reset_RestoresDefaultsAndRefreshGate()
        {
            var state = CreateCustomizedState();
            Assert.That(state.TryBeginRefresh(10d), Is.True);
            state.MarkRegistryChanged();

            state.Reset();

            AssertDefaults(state);
            Assert.That(state.TryBeginRefresh(0d), Is.True);
        }

        [Test]
        public void LayoutAndRefreshInterval_AreClamped()
        {
            var state = new PipelineDebuggerWorkspaceState
            {
                RunPaneWidth = float.MinValue,
                RefreshIntervalSeconds = float.MinValue
            };

            Assert.That(
                state.RunPaneWidth,
                Is.EqualTo(PipelineDebuggerWorkspaceState.MinRunPaneWidth));
            Assert.That(
                state.RefreshIntervalSeconds,
                Is.EqualTo(PipelineDebuggerWorkspaceState.MinRefreshIntervalSeconds));

            state.RunPaneWidth = float.MaxValue;
            state.RefreshIntervalSeconds = float.MaxValue;

            Assert.That(
                state.RunPaneWidth,
                Is.EqualTo(PipelineDebuggerWorkspaceState.MaxRunPaneWidth));
            Assert.That(
                state.RefreshIntervalSeconds,
                Is.EqualTo(PipelineDebuggerWorkspaceState.MaxRefreshIntervalSeconds));
        }

        [Test]
        public void Restore_InvalidEnumValues_FallBackToDefaults()
        {
            _userState.RunFilter = int.MaxValue;
            _userState.DetailTab = int.MaxValue;
            _userState.TraceFilter = int.MaxValue;

            var state = new PipelineDebuggerWorkspaceState();
            state.Restore(_userState);

            Assert.That(state.RunFilter, Is.EqualTo(PipelineDebuggerRunFilter.All));
            Assert.That(state.DetailTab, Is.EqualTo(PipelineDebuggerDetailTab.Overview));
            Assert.That(state.TraceFilter, Is.EqualTo(PipelineDebuggerTraceFilter.All));
        }

        [Test]
        public void PersistAndRestore_RoundTripsWorkspaceValues()
        {
            var source = CreateCustomizedState();

            source.Persist(_userState);
            var restored = new PipelineDebuggerWorkspaceState();
            restored.Restore(_userState);

            Assert.That(restored.FollowLatest, Is.False);
            Assert.That(restored.RelativeTraceTime, Is.False);
            Assert.That(restored.ConfirmInterrupt, Is.False);
            Assert.That(restored.ShowOnlyChangedContext, Is.True);
            Assert.That(restored.ShowPhaseGraph, Is.False);
            Assert.That(restored.RunFilter, Is.EqualTo(PipelineDebuggerRunFilter.Failed));
            Assert.That(restored.DetailTab, Is.EqualTo(PipelineDebuggerDetailTab.Context));
            Assert.That(restored.TraceFilter, Is.EqualTo(PipelineDebuggerTraceFilter.Errors));
            Assert.That(restored.RunSearch, Is.EqualTo("run"));
            Assert.That(restored.TraceSearch, Is.EqualTo("trace"));
            Assert.That(restored.ContextSearch, Is.EqualTo("context"));
            Assert.That(restored.RunPaneWidth, Is.EqualTo(420f));
            Assert.That(restored.RefreshIntervalSeconds, Is.EqualTo(0.5f));
        }

        [Test]
        public void TryBeginRefresh_ThrottlesUntilIntervalExpires()
        {
            var state = new PipelineDebuggerWorkspaceState
            {
                RefreshIntervalSeconds = 0.5f
            };

            Assert.That(state.TryBeginRefresh(10d), Is.True);
            Assert.That(state.TryBeginRefresh(10.49d), Is.False);
            Assert.That(state.TryBeginRefresh(10.5d), Is.True);
        }

        [Test]
        public void MarkRegistryChanged_BypassesRefreshThrottleOnce()
        {
            var state = new PipelineDebuggerWorkspaceState
            {
                RefreshIntervalSeconds = 1f
            };

            Assert.That(state.TryBeginRefresh(10d), Is.True);
            state.MarkRegistryChanged();

            Assert.That(state.TryBeginRefresh(10.1d), Is.True);
            Assert.That(state.TryBeginRefresh(10.2d), Is.False);
        }

        [Test]
        public void ResetRefreshGate_AllowsImmediateRefresh()
        {
            var state = new PipelineDebuggerWorkspaceState
            {
                RefreshIntervalSeconds = 1f
            };

            Assert.That(state.TryBeginRefresh(10d), Is.True);
            Assert.That(state.TryBeginRefresh(10.1d), Is.False);

            state.ResetRefreshGate();

            Assert.That(state.TryBeginRefresh(10.1d), Is.True);
        }

        [Test]
        public void RestoreAndPersist_RejectNullUserState()
        {
            var state = new PipelineDebuggerWorkspaceState();

            Assert.That(
                () => state.Restore(null!),
                Throws.ArgumentNullException);
            Assert.That(
                () => state.Persist(null!),
                Throws.ArgumentNullException);
        }

        private static PipelineDebuggerWorkspaceState CreateCustomizedState()
        {
            return new PipelineDebuggerWorkspaceState
            {
                FollowLatest = false,
                RelativeTraceTime = false,
                ConfirmInterrupt = false,
                ShowOnlyChangedContext = true,
                ShowPhaseGraph = false,
                RunFilter = PipelineDebuggerRunFilter.Failed,
                DetailTab = PipelineDebuggerDetailTab.Context,
                TraceFilter = PipelineDebuggerTraceFilter.Errors,
                RunSearch = "run",
                TraceSearch = "trace",
                ContextSearch = "context",
                RunPaneWidth = 420f,
                RefreshIntervalSeconds = 0.5f
            };
        }

        private static void AssertDefaults(PipelineDebuggerWorkspaceState state)
        {
            Assert.That(state.RunSearch, Is.Empty);
            Assert.That(state.TraceSearch, Is.Empty);
            Assert.That(state.ContextSearch, Is.Empty);
            Assert.That(state.RunFilter, Is.EqualTo(PipelineDebuggerRunFilter.All));
            Assert.That(state.DetailTab, Is.EqualTo(PipelineDebuggerDetailTab.Overview));
            Assert.That(state.TraceFilter, Is.EqualTo(PipelineDebuggerTraceFilter.All));
            Assert.That(state.FollowLatest, Is.True);
            Assert.That(state.RelativeTraceTime, Is.True);
            Assert.That(state.ConfirmInterrupt, Is.True);
            Assert.That(state.ShowOnlyChangedContext, Is.False);
            Assert.That(state.ShowPhaseGraph, Is.True);
            Assert.That(
                state.RunPaneWidth,
                Is.EqualTo(PipelineDebuggerWorkspaceState.DefaultRunPaneWidth));
            Assert.That(
                state.RefreshIntervalSeconds,
                Is.EqualTo(PipelineDebuggerWorkspaceState.DefaultRefreshIntervalSeconds));
        }
    }
}

#endif
