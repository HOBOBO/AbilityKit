using System;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class BattleDiagnosticCoreTests
    {
        [Test]
        public void Selection_BelongsTo_RequiresMatchingWorldEpoch()
        {
            var firstEpoch = new BattleDiagnosticSessionScope("session", "world", 1);
            var nextEpoch = new BattleDiagnosticSessionScope("session", "world", 2);
            var selection = Actor(firstEpoch, 1001, 25);

            Assert.That(selection.BelongsTo(firstEpoch), Is.True);
            Assert.That(selection.BelongsTo(nextEpoch), Is.False);
        }

        [Test]
        public void FrameCursor_UserSelection_DisablesFollowLive()
        {
            var cursor = BattleDiagnosticFrameCursor.CreateFollowingLive(100);

            cursor = cursor.SelectFrame(80);
            cursor = cursor.AdvanceLive(101);

            Assert.That(cursor.Frame, Is.EqualTo(80));
            Assert.That(cursor.FollowsLive, Is.False);
            Assert.That(cursor.ChangeReason, Is.EqualTo(BattleDiagnosticFrameCursorChangeReason.UserSelectedFrame));
        }

        [Test]
        public void FrameCursor_RestoreFollowLive_JumpsToLatestCompleteFrame()
        {
            var cursor = BattleDiagnosticFrameCursor.CreateFollowingLive(100)
                .SelectFrame(80);

            cursor = cursor.SetFollowLive(true, 125);

            Assert.That(cursor.Frame, Is.EqualTo(125));
            Assert.That(cursor.FollowsLive, Is.True);
            Assert.That(cursor.ChangeReason, Is.EqualTo(BattleDiagnosticFrameCursorChangeReason.FollowLiveAdvanced));
        }

        [Test]
        public void FrameCursor_AdvanceLive_OnlyMovesFollowingCursor()
        {
            var following = BattleDiagnosticFrameCursor.CreateFollowingLive(100)
                .AdvanceLive(101);
            var fixedFrame = BattleDiagnosticFrameCursor.CreateFollowingLive(100)
                .SelectFrame(80)
                .AdvanceLive(101);

            Assert.That(following.Frame, Is.EqualTo(101));
            Assert.That(following.ChangeReason, Is.EqualTo(BattleDiagnosticFrameCursorChangeReason.FollowLiveAdvanced));
            Assert.That(fixedFrame.Frame, Is.EqualTo(80));
            Assert.That(fixedFrame.FollowsLive, Is.False);
        }

        [Test]
        public void FrameCursor_ConstrainToRetainedRange_MovesEvictedFrameToFirstAvailable()
        {
            var cursor = BattleDiagnosticFrameCursor.CreateFollowingLive(100).SelectFrame(20);

            cursor = cursor.ConstrainTo(new BattleDiagnosticFrameRange(40, 100));

            Assert.That(cursor.Frame, Is.EqualTo(40));
            Assert.That(cursor.FollowsLive, Is.False);
            Assert.That(cursor.ChangeReason, Is.EqualTo(BattleDiagnosticFrameCursorChangeReason.RetainedRangeClamped));
        }

        [Test]
        public void NavigationHistory_NewSelectionAfterBack_RemovesForwardBranch()
        {
            var scope = new BattleDiagnosticSessionScope("session", "world", 1);
            var history = new BattleDiagnosticNavigationHistory();
            var first = Actor(scope, 1, 10);
            var second = Actor(scope, 2, 20);
            var branch = Actor(scope, 3, 30);

            history.NavigateTo(first);
            history.NavigateTo(second);
            Assert.That(history.TryGoBack(out var back), Is.True);
            Assert.That(back, Is.EqualTo(first));

            history.NavigateTo(branch);

            Assert.That(history.Count, Is.EqualTo(2));
            Assert.That(history.Current, Is.EqualTo(branch));
            Assert.That(history.CanGoForward, Is.False);
        }

        [Test]
        public void NavigationHistory_DifferentScope_ClearsPreviousEntries()
        {
            var firstScope = new BattleDiagnosticSessionScope("session", "world", 1);
            var nextScope = new BattleDiagnosticSessionScope("session", "world", 2);
            var history = new BattleDiagnosticNavigationHistory();

            history.NavigateTo(Actor(firstScope, 1, 10));
            history.NavigateTo(Actor(nextScope, 1, 11));

            Assert.That(history.Count, Is.EqualTo(1));
            Assert.That(history.Scope, Is.EqualTo(nextScope));
            Assert.That(history.Current.Scope, Is.EqualTo(nextScope));
            Assert.That(history.CanGoBack, Is.False);
        }

        [Test]
        public void NavigationHistory_ExceedingCapacity_DropsOldestEntry()
        {
            var scope = new BattleDiagnosticSessionScope("session", "world", 1);
            var history = new BattleDiagnosticNavigationHistory(2);

            history.NavigateTo(Actor(scope, 1, 10));
            history.NavigateTo(Actor(scope, 2, 20));
            history.NavigateTo(Actor(scope, 3, 30));

            Assert.That(history.Count, Is.EqualTo(2));
            Assert.That(history.TryGetEntry(0, out var firstRetained), Is.True);
            Assert.That(firstRetained.Id, Is.EqualTo(2));
            Assert.That(history.Current.Id, Is.EqualTo(3));
        }

        [Test]
        public void Workspace_RejectsSelectionFromDifferentScope()
        {
            var currentScope = new BattleDiagnosticSessionScope("session", "world", 1);
            var otherScope = new BattleDiagnosticSessionScope("session", "world", 2);
            var workspace = new BattleDiagnosticWorkspaceState();
            workspace.AttachSession(currentScope, 50);

            var changed = workspace.Select(Actor(otherScope, 7, 30));

            Assert.That(changed, Is.False);
            Assert.That(workspace.Selection.IsValid, Is.False);
            Assert.That(workspace.FrameCursor.Frame, Is.EqualTo(50));
        }

        [Test]
        public void Workspace_SelectingEvent_MovesCursorAndCreatesNavigationEntry()
        {
            var scope = new BattleDiagnosticSessionScope("session", "world", 1);
            var workspace = new BattleDiagnosticWorkspaceState();
            workspace.AttachSession(scope, 50);
            var selection = new BattleDiagnosticSelection(
                scope,
                BattleDiagnosticSelectionKind.Event,
                9001,
                35);

            var changed = workspace.Select(selection);

            Assert.That(changed, Is.True);
            Assert.That(workspace.Selection, Is.EqualTo(selection));
            Assert.That(workspace.FrameCursor.Frame, Is.EqualTo(35));
            Assert.That(workspace.FrameCursor.FollowsLive, Is.False);
            Assert.That(workspace.Navigation.Count, Is.EqualTo(1));
        }

        [Test]
        public void Workspace_HistoryBackAndForward_RestoreSelectionFrame()
        {
            var scope = new BattleDiagnosticSessionScope("session", "world", 1);
            var workspace = new BattleDiagnosticWorkspaceState();
            workspace.AttachSession(scope, 50);
            var first = new BattleDiagnosticSelection(
                scope,
                BattleDiagnosticSelectionKind.Event,
                1,
                20);
            var second = new BattleDiagnosticSelection(
                scope,
                BattleDiagnosticSelectionKind.TraceNode,
                2,
                35,
                900);

            workspace.Select(first);
            workspace.Select(second);
            Assert.That(workspace.GoBack(), Is.True);
            Assert.That(workspace.Selection, Is.EqualTo(first));
            Assert.That(workspace.FrameCursor.Frame, Is.EqualTo(20));
            Assert.That(workspace.FrameCursor.ChangeReason, Is.EqualTo(BattleDiagnosticFrameCursorChangeReason.SelectionNavigation));

            Assert.That(workspace.GoForward(), Is.True);
            Assert.That(workspace.Selection, Is.EqualTo(second));
            Assert.That(workspace.FrameCursor.Frame, Is.EqualTo(35));
            Assert.That(workspace.FrameCursor.ChangeReason, Is.EqualTo(BattleDiagnosticFrameCursorChangeReason.SelectionNavigation));
        }

        [Test]
        public void TimeRange_Fixed_NormalizesBoundsAndResolvesIndependentlyOfAutomaticRange()
        {
            var timeRange = BattleDiagnosticTimeRange.Fixed(80, 20);

            Assert.That(timeRange.IsFixed, Is.True);
            Assert.That(timeRange.FirstFrame, Is.EqualTo(20));
            Assert.That(timeRange.LastFrame, Is.EqualTo(80));
            Assert.That(
                timeRange.Resolve(new BattleDiagnosticFrameRange(40, 60)),
                Is.EqualTo(new BattleDiagnosticFrameRange(20, 80)));
        }

        [Test]
        public void FrameRange_Intersect_ClipsOverlapAndRejectsDisjointRanges()
        {
            var range = new BattleDiagnosticFrameRange(20, 80);

            Assert.That(
                range.Intersect(new BattleDiagnosticFrameRange(60, 100)),
                Is.EqualTo(new BattleDiagnosticFrameRange(60, 80)));
            Assert.That(
                range.Intersect(new BattleDiagnosticFrameRange(90, 100)).IsValid,
                Is.False);
        }

        [Test]
        public void Workspace_FixedTimeRange_SurvivesCursorMovementAndResetsForNewSession()
        {
            var firstScope = new BattleDiagnosticSessionScope("session-a", "world", 1);
            var secondScope = new BattleDiagnosticSessionScope("session-b", "world", 2);
            var workspace = new BattleDiagnosticWorkspaceState();
            workspace.AttachSession(firstScope, 100);
            workspace.SetTimeRange(20, 80);

            workspace.SetFrame(50);

            Assert.That(workspace.TimeRange.Range, Is.EqualTo(new BattleDiagnosticFrameRange(20, 80)));
            workspace.AttachSession(secondScope, 120);
            Assert.That(workspace.TimeRange.IsAuto, Is.True);
            Assert.That(
                workspace.TimeRange.ChangeReason,
                Is.EqualTo(BattleDiagnosticTimeRangeChangeReason.SessionChanged));
        }

        [Test]
        public void Workspace_ConstrainToRetainedRange_ClampsCursorAndFixedTimeRangeTogether()
        {
            var workspace = new BattleDiagnosticWorkspaceState();
            workspace.AttachSession(
                new BattleDiagnosticSessionScope("session", "world", 1),
                100);
            workspace.SetTimeRange(10, 90);
            workspace.SetFrame(15);

            workspace.ConstrainToRetainedRange(new BattleDiagnosticFrameRange(40, 75));

            Assert.That(workspace.FrameCursor.Frame, Is.EqualTo(40));
            Assert.That(workspace.TimeRange.Range, Is.EqualTo(new BattleDiagnosticFrameRange(40, 75)));
            Assert.That(
                workspace.TimeRange.ChangeReason,
                Is.EqualTo(BattleDiagnosticTimeRangeChangeReason.RetainedRangeClamped));
        }

        [Test]
        public void TimeRange_Zoom_PreservesAnchorAndUsesInclusiveFrameCount()
        {
            var range = BattleDiagnosticTimeRange.Fixed(100, 199);

            var zoomedIn = range.Zoom(150, 0.5d);
            var zoomedOut = range.Zoom(150, 2d);

            Assert.That(zoomedIn.Range, Is.EqualTo(new BattleDiagnosticFrameRange(125, 174)));
            Assert.That(zoomedIn.ChangeReason, Is.EqualTo(BattleDiagnosticTimeRangeChangeReason.Zoomed));
            Assert.That(zoomedOut.Range, Is.EqualTo(new BattleDiagnosticFrameRange(49, 248)));
        }

        [Test]
        public void TimeRange_Pan_PreservesWidthAndClampsToNonNegativeFrames()
        {
            var range = BattleDiagnosticTimeRange.Fixed(100, 199);

            var panned = range.Pan(-150);

            Assert.That(panned.Range, Is.EqualTo(new BattleDiagnosticFrameRange(0, 99)));
            Assert.That(panned.ChangeReason, Is.EqualTo(BattleDiagnosticTimeRangeChangeReason.Panned));
        }

        [Test]
        public void Workspace_TimeRangeHistory_BackForwardAndNewBranchAreBoundedToSession()
        {
            var workspace = new BattleDiagnosticWorkspaceState();
            workspace.AttachSession(
                new BattleDiagnosticSessionScope("session", "world", 1),
                100);
            workspace.SetTimeRange(10, 20);
            workspace.SetTimeRange(30, 40);

            Assert.That(workspace.GoBackTimeRange(), Is.True);
            Assert.That(workspace.TimeRange.Range, Is.EqualTo(new BattleDiagnosticFrameRange(10, 20)));
            Assert.That(
                workspace.TimeRange.ChangeReason,
                Is.EqualTo(BattleDiagnosticTimeRangeChangeReason.HistoryNavigation));
            Assert.That(workspace.GoBackTimeRange(), Is.True);
            Assert.That(workspace.TimeRange.IsAuto, Is.True);
            Assert.That(workspace.GoForwardTimeRange(), Is.True);

            workspace.SetTimeRange(50, 60);

            Assert.That(workspace.CanGoForwardTimeRange, Is.False);
            Assert.That(workspace.TimeRangeHistoryCount, Is.EqualTo(3));
            Assert.That(workspace.TimeRange.Range, Is.EqualTo(new BattleDiagnosticFrameRange(50, 60)));
        }

        [Test]
        public void Workspace_TimeRangeHistory_DoesNotRecordSameBoundsTwice()
        {
            var workspace = new BattleDiagnosticWorkspaceState();
            workspace.AttachSession(
                new BattleDiagnosticSessionScope("session", "world", 1),
                100);
            workspace.SetTimeRange(10, 20);

            var changed = workspace.SetTimeRange(
                10,
                20,
                BattleDiagnosticTimeRangeChangeReason.Zoomed);

            Assert.That(changed, Is.False);
            Assert.That(workspace.TimeRangeHistoryCount, Is.EqualTo(2));
        }

        [Test]
        public void Filter_Default_IsUnboundedAndHasNoActiveDimensions()
        {
            var filter = BattleDiagnosticFilter.Default;

            Assert.That(filter.Frames.IsBounded, Is.False);
            Assert.That(filter.ActiveFilterCount, Is.EqualTo(0));
        }

        [Test]
        public void Filter_NormalizesSearchText_AndCountsActiveDimensions()
        {
            var filter = BattleDiagnosticFilter.Default
                .WithActor(42, BattleDiagnosticActorRelation.Either)
                .WithSearchText("  Fire Ball  ");

            Assert.That(filter.SearchText, Is.EqualTo("Fire Ball"));
            Assert.That(filter.ActiveFilterCount, Is.EqualTo(2));
        }

        [Test]
        public void QueryStatus_Partial_RejectsAmbiguousAvailability()
        {
            Assert.Throws<ArgumentException>(() =>
                BattleDiagnosticQueryStatus.Partial(
                    1,
                    2,
                    10,
                    BattleDiagnosticDataAvailability.Available));
        }

        [Test]
        public void PageRequest_NextPage_KeepsStoreRevision()
        {
            var first = new BattleDiagnosticPageRequest(123, 0, 100);

            var next = first.NextPage();

            Assert.That(next.StoreRevision, Is.EqualTo(123));
            Assert.That(next.Offset, Is.EqualTo(100));
            Assert.That(next.Limit, Is.EqualTo(100));
        }

        [Test]
        public void HealthSnapshot_ValidProducedTracksAndErrors_AreDerivedFromCapturedValues()
        {
            var health = Health(
                eventRevision: 3,
                stateRevision: 2,
                lastStateFrame: 120,
                lastEventSequence: 99,
                stateError: "sample failed");

            Assert.That(health.IsValid, Is.True);
            Assert.That(health.HasProducedState, Is.True);
            Assert.That(health.HasProducedEvents, Is.True);
            Assert.That(health.HasErrors, Is.True);
        }

        [Test]
        public void HealthSnapshot_RequiresRevisionAndIdentityToReportProducedTracks()
        {
            var health = Health(
                eventRevision: 0,
                stateRevision: 1,
                lastStateFrame: BattleDiagnosticFrames.Invalid,
                lastEventSequence: 99);

            Assert.That(health.HasProducedState, Is.False);
            Assert.That(health.HasProducedEvents, Is.False);
            Assert.That(health.HasErrors, Is.False);
        }

        [Test]
        public void HealthSnapshot_TruncatesErrorsAndPreservesValueEquality()
        {
            var longError = new string('x', 600);
            var first = Health(stateError: longError, eventError: longError);
            var second = Health(stateError: longError, eventError: longError);

            Assert.That(first.LastStateSampleError.Length, Is.EqualTo(512));
            Assert.That(first.LastEventCollectError.Length, Is.EqualTo(512));
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        private static BattleDiagnosticHealthSnapshot Health(
            long eventRevision = 0,
            long stateRevision = 0,
            int lastStateFrame = BattleDiagnosticFrames.Invalid,
            long lastEventSequence = 0,
            string stateError = "",
            string eventError = "")
        {
            var sessionInfo = new BattleDiagnosticSessionInfo(
                new BattleDiagnosticSessionScope("session", "world", 1),
                "test",
                "build",
                1,
                1000,
                BattleDiagnosticCapabilities.AllLocal,
                BattleDiagnosticConnectionState.Connected,
                BattleDiagnosticCaptureState.Capturing);
            var metrics = new BattleDiagnosticStoreMetrics(
                100,
                3,
                eventRevision,
                3,
                0,
                0,
                false);
            return new BattleDiagnosticHealthSnapshot(
                sessionInfo,
                eventRevision,
                stateRevision,
                4,
                lastStateFrame,
                lastEventSequence,
                BattleDiagnosticEventChannel.All,
                false,
                metrics,
                string.IsNullOrEmpty(stateError) ? 0 : 1,
                string.IsNullOrEmpty(eventError) ? 0 : 1,
                stateError,
                eventError);
        }

        private static BattleDiagnosticSelection Actor(
            BattleDiagnosticSessionScope scope,
            long actorId,
            int frame)
        {
            return new BattleDiagnosticSelection(
                scope,
                BattleDiagnosticSelectionKind.Actor,
                actorId,
                frame);
        }
    }
}
