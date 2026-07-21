using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class BattleDebugDiagnosticViewModelTests
    {
        [Test]
        public void EventsCacheKey_IncludesRevisionAndEveryFilterOrSelectionInput()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticEventsViewModel();

            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.EventQueryCount, Is.EqualTo(1));

            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.EventQueryCount, Is.EqualTo(1));

            session.EventStoreRevision++;
            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.EventQueryCount, Is.EqualTo(2));

            viewModel.FilterBySelectedActor = false;
            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.EventQueryCount, Is.EqualTo(3));

            viewModel.FailuresOnly = true;
            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.EventQueryCount, Is.EqualTo(4));

            viewModel.SearchText = "damage";
            viewModel.RefreshIfNeeded(session, 10, true);
            Assert.That(session.EventQueryCount, Is.EqualTo(5));

            viewModel.RefreshIfNeeded(session, 11, true);
            Assert.That(session.EventQueryCount, Is.EqualTo(6));

            viewModel.RefreshIfNeeded(session, 11, false);
            Assert.That(session.EventQueryCount, Is.EqualTo(7));
        }

        [Test]
        public void EventsCache_IgnoresStateRevision()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticEventsViewModel();
            viewModel.RefreshIfNeeded(session, 0, false);

            session.StateStoreRevision++;
            viewModel.RefreshIfNeeded(session, 0, false);

            Assert.That(session.EventQueryCount, Is.EqualTo(1));
        }

        [Test]
        public void StateCacheKey_IncludesStateRevisionAndFrameInput()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticStateViewModel();

            viewModel.RefreshIfNeeded(session);
            Assert.That(session.WorldQueryCount, Is.EqualTo(1));
            Assert.That(session.ActorQueryCount, Is.EqualTo(1));

            viewModel.RefreshIfNeeded(session);
            Assert.That(session.WorldQueryCount, Is.EqualTo(1));

            session.StateStoreRevision++;
            viewModel.RefreshIfNeeded(session);
            Assert.That(session.WorldQueryCount, Is.EqualTo(2));

            viewModel.FrameInput = 5;
            viewModel.RefreshIfNeeded(session);
            Assert.That(session.WorldQueryCount, Is.EqualTo(3));
            Assert.That(session.LastWorldFrame, Is.EqualTo(5));
            Assert.That(session.LastActorFrame, Is.EqualTo(5));
        }

        [Test]
        public void StateCache_IgnoresEventRevisionAndCachesUnavailableResult()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticStateViewModel();
            viewModel.RefreshIfNeeded(session);

            session.EventStoreRevision++;
            viewModel.RefreshIfNeeded(session);

            Assert.That(session.WorldQueryCount, Is.EqualTo(1));
            Assert.That(session.ActorQueryCount, Is.EqualTo(1));
        }

        [Test]
        public void AttributeCacheKey_IncludesAttributeRevisionActorAndFrame()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticAttributesViewModel();

            viewModel.RefreshIfNeeded(session, 11);
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.AttributeQueryCount, Is.EqualTo(1));
            Assert.That(session.ModifierQueryCount, Is.EqualTo(1));

            session.StateStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.AttributeQueryCount, Is.EqualTo(1));

            session.ActorAttributeStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            viewModel.RefreshIfNeeded(session, 12);
            viewModel.RefreshIfNeeded(session, 12, 5);

            Assert.That(session.AttributeQueryCount, Is.EqualTo(4));
            Assert.That(session.ModifierQueryCount, Is.EqualTo(4));
            Assert.That(session.LastAttributeActorId, Is.EqualTo(12));
            Assert.That(session.LastAttributeFrame, Is.EqualTo(5));
        }

        [Test]
        public void BuffCacheKey_IncludesBuffRevisionActorAndFrame()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticBuffsViewModel();

            viewModel.RefreshIfNeeded(session, 11);
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.BuffQueryCount, Is.EqualTo(1));

            session.StateStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.BuffQueryCount, Is.EqualTo(1));

            session.ActorBuffStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            viewModel.RefreshIfNeeded(session, 12);
            viewModel.RefreshIfNeeded(session, 12, 5);

            Assert.That(session.BuffQueryCount, Is.EqualTo(4));
            Assert.That(session.LastBuffActorId, Is.EqualTo(12));
            Assert.That(session.LastBuffFrame, Is.EqualTo(5));
        }

        [Test]
        public void TagCacheKey_IncludesTagRevisionActorAndFrame()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticTagsViewModel();

            viewModel.RefreshIfNeeded(session, 11);
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.TagQueryCount, Is.EqualTo(1));

            session.StateStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.TagQueryCount, Is.EqualTo(1));

            session.ActorTagStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            viewModel.RefreshIfNeeded(session, 12);
            viewModel.RefreshIfNeeded(session, 12, 5);

            Assert.That(session.TagQueryCount, Is.EqualTo(4));
            Assert.That(session.LastTagActorId, Is.EqualTo(12));
            Assert.That(session.LastTagFrame, Is.EqualTo(5));
        }

        [Test]
        public void EffectCacheKey_IncludesEffectRevisionActorAndFrame()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticEffectsViewModel();

            viewModel.RefreshIfNeeded(session, 11);
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.EffectQueryCount, Is.EqualTo(1));

            session.StateStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.EffectQueryCount, Is.EqualTo(1));

            session.ActorEffectStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            viewModel.RefreshIfNeeded(session, 12);
            viewModel.RefreshIfNeeded(session, 12, 5);

            Assert.That(session.EffectQueryCount, Is.EqualTo(4));
            Assert.That(session.LastEffectActorId, Is.EqualTo(12));
            Assert.That(session.LastEffectFrame, Is.EqualTo(5));
        }

        [Test]
        public void OverviewCacheKey_IncludesAllRevisionsActorAndFrame()
        {
            var session = new RecordingSession();
            var viewModel = new BattleDebugDiagnosticOverviewViewModel();

            viewModel.RefreshIfNeeded(session, 11);
            viewModel.RefreshIfNeeded(session, 11);
            Assert.That(session.ActorQueryCount, Is.EqualTo(1));
            Assert.That(session.TagQueryCount, Is.EqualTo(1));
            Assert.That(session.EffectQueryCount, Is.EqualTo(1));

            session.StateStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            session.ActorTagStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            session.ActorEffectStoreRevision++;
            viewModel.RefreshIfNeeded(session, 11);
            viewModel.RefreshIfNeeded(session, 12);
            viewModel.RefreshIfNeeded(session, 12, 5);

            Assert.That(session.ActorQueryCount, Is.EqualTo(6));
            Assert.That(session.TagQueryCount, Is.EqualTo(6));
            Assert.That(session.EffectQueryCount, Is.EqualTo(6));
            Assert.That(session.LastActorFrame, Is.EqualTo(5));
            Assert.That(session.LastTagActorId, Is.EqualTo(12));
            Assert.That(session.LastEffectActorId, Is.EqualTo(12));
        }

        [Test]
        public void Overview_ProjectsSelectedActorCountsAndTagClipboardText()
        {
            var session = new RecordingSession
            {
                Actors = new[]
                {
                    new BattleDiagnosticActorSummary(
                        RecordingSession.Scope, 7, 10, BattleDiagnosticActorKind.Minion,
                        100, 2, 0, 0, 0, 20, 20, true, "Other"),
                    new BattleDiagnosticActorSummary(
                        RecordingSession.Scope, 7, 11, BattleDiagnosticActorKind.Hero,
                        200, 1, 1, 2, 3, 80, 100, true, "Selected")
                },
                Tags = new[]
                {
                    new BattleDiagnosticActorTag(RecordingSession.Scope, 7, 11, 1001, "State.Stunned"),
                    new BattleDiagnosticActorTag(RecordingSession.Scope, 7, 11, 1002)
                },
                Effects = new[]
                {
                    new BattleDiagnosticActorEffect(
                        RecordingSession.Scope, 7, 11, 1,
                        BattleDiagnosticEffectDurationPolicy.Infinite, 1,
                        0, 0, false, 0, false, 0, 0, 0, false)
                }
            };
            var viewModel = new BattleDebugDiagnosticOverviewViewModel();

            viewModel.RefreshIfNeeded(session, 11, 7);

            Assert.That(viewModel.Actor.HasValue, Is.True);
            Assert.That(viewModel.Actor.Value.DisplayName, Is.EqualTo("Selected"));
            Assert.That(viewModel.TagCount, Is.EqualTo(2));
            Assert.That(viewModel.EffectCount, Is.EqualTo(1));
            Assert.That(viewModel.BuildTagList(), Is.EqualTo("State.Stunned\n1002"));
            Assert.That(viewModel.StatusMessage, Is.Empty);
        }

        [Test]
        public void TraceCacheKey_IncludesScopeRevisionAndRootButIgnoresOtherRevisions()
        {
            var session = new RecordingSession { TraceNodes = new[] { TraceNode(100, 100, 0, "Root") } };
            var viewModel = new BattleDebugDiagnosticTraceViewModel();

            viewModel.RefreshIfNeeded(session, 100);
            viewModel.RefreshIfNeeded(session, 100);
            Assert.That(session.TraceQueryCount, Is.EqualTo(1));

            session.EventStoreRevision++;
            session.StateStoreRevision++;
            viewModel.RefreshIfNeeded(session, 100);
            Assert.That(session.TraceQueryCount, Is.EqualTo(1));

            session.TraceStoreRevision++;
            viewModel.RefreshIfNeeded(session, 100);
            viewModel.RefreshIfNeeded(session, 200);

            Assert.That(session.TraceQueryCount, Is.EqualTo(3));
            Assert.That(session.LastTraceRootContextId, Is.EqualTo(200));

            var otherSession = new RecordingSession(2) { TraceNodes = session.TraceNodes };
            viewModel.RefreshIfNeeded(otherSession, 200);
            Assert.That(otherSession.TraceQueryCount, Is.EqualTo(1));
        }

        [Test]
        public void Trace_ProjectsDepthOrphansAndSelectedParentPath()
        {
            var session = new RecordingSession
            {
                TraceNodes = new[]
                {
                    TraceNode(100, 100, 0, "Root"),
                    TraceNode(100, 110, 100, "Skill"),
                    TraceNode(100, 111, 110, "Effect"),
                    TraceNode(100, 120, 999, "Orphan")
                }
            };
            var viewModel = new BattleDebugDiagnosticTraceViewModel();

            viewModel.RefreshIfNeeded(session, 100);

            Assert.That(viewModel.Rows.Count, Is.EqualTo(4));
            Assert.That(viewModel.Rows[0].Depth, Is.EqualTo(0));
            Assert.That(viewModel.Rows[1].Depth, Is.EqualTo(1));
            Assert.That(viewModel.Rows[2].Depth, Is.EqualTo(2));
            Assert.That(viewModel.Rows[3].Depth, Is.EqualTo(0));
            Assert.That(viewModel.Rows[3].IsOrphan, Is.True);
            Assert.That(viewModel.SelectedContextId, Is.EqualTo(100));

            Assert.That(viewModel.SelectContext(111), Is.True);
            Assert.That(viewModel.SelectedPath.Count, Is.EqualTo(3));
            Assert.That(viewModel.SelectedPath[0].ContextId, Is.EqualTo(100));
            Assert.That(viewModel.SelectedPath[1].ContextId, Is.EqualTo(110));
            Assert.That(viewModel.SelectedPath[2].ContextId, Is.EqualTo(111));
            Assert.That(viewModel.SelectContext(999), Is.False);
        }

        [Test]
        public void TraceRevision_WhenSelectedNodeDisappears_FallsBackToRoot()
        {
            var session = new RecordingSession
            {
                TraceNodes = new[]
                {
                    TraceNode(100, 100, 0, "Root"),
                    TraceNode(100, 110, 100, "Child")
                }
            };
            var viewModel = new BattleDebugDiagnosticTraceViewModel();
            viewModel.RefreshIfNeeded(session, 100);
            viewModel.SelectContext(110);

            session.TraceStoreRevision++;
            session.TraceNodes = new[] { TraceNode(100, 100, 0, "Root") };
            viewModel.RefreshIfNeeded(session, 100);

            Assert.That(viewModel.SelectedContextId, Is.EqualTo(100));
            Assert.That(viewModel.SelectedPath.Count, Is.EqualTo(1));
            Assert.That(viewModel.SelectedPath[0].ContextId, Is.EqualTo(100));
        }

        [Test]
        public void TraceUnavailable_ClearsPreviousRowsSelectionAndPath()
        {
            var session = new RecordingSession { TraceNodes = new[] { TraceNode(100, 100, 0, "Root") } };
            var viewModel = new BattleDebugDiagnosticTraceViewModel();
            viewModel.RefreshIfNeeded(session, 100);
            Assert.That(viewModel.Rows.Count, Is.EqualTo(1));

            session.TraceStoreRevision++;
            session.TraceNodes = null;
            session.TraceAvailability = BattleDiagnosticDataAvailability.Evicted;
            viewModel.RefreshIfNeeded(session, 100);

            Assert.That(viewModel.Rows, Is.Empty);
            Assert.That(viewModel.SelectedPath, Is.Empty);
            Assert.That(viewModel.SelectedContextId, Is.Zero);
            Assert.That(viewModel.StatusMessage, Does.Contain("Evicted"));
        }

        [Test]
        public void Trace_CyclicParents_DoNotRecurseForever()
        {
            var session = new RecordingSession
            {
                TraceNodes = new[]
                {
                    TraceNode(100, 100, 110, "A"),
                    TraceNode(100, 110, 100, "B")
                }
            };
            var viewModel = new BattleDebugDiagnosticTraceViewModel();

            Assert.DoesNotThrow(() => viewModel.RefreshIfNeeded(session, 100));
            Assert.That(viewModel.Rows.Count, Is.EqualTo(2));
            Assert.That(viewModel.SelectContext(110), Is.True);
            Assert.That(viewModel.SelectedPath.Count, Is.EqualTo(2));
        }

        [Test]
        public void TraceSearch_IncludesMatchesAndAncestors_AndIgnoresCollapse()
        {
            var session = new RecordingSession
            {
                TraceNodes = new[]
                {
                    TraceNode(100, 100, 0, "Root"),
                    TraceNode(100, 110, 100, "Skill"),
                    TraceNode(100, 111, 110, "DamageEffect"),
                    TraceNode(100, 120, 100, "Unrelated")
                }
            };
            var viewModel = new BattleDebugDiagnosticTraceViewModel();
            viewModel.RefreshIfNeeded(session, 100);
            viewModel.ToggleCollapsed(100);

            Assert.That(viewModel.VisibleRows.Count, Is.EqualTo(1));

            viewModel.SetSearchText("damage");

            Assert.That(viewModel.SearchMatchCount, Is.EqualTo(1));
            Assert.That(viewModel.VisibleRows.Count, Is.EqualTo(3));
            Assert.That(viewModel.VisibleRows[0].Node.ContextId, Is.EqualTo(100));
            Assert.That(viewModel.VisibleRows[1].Node.ContextId, Is.EqualTo(110));
            Assert.That(viewModel.VisibleRows[2].Node.ContextId, Is.EqualTo(111));
            Assert.That(viewModel.IsSearchMatch(111), Is.True);
            Assert.That(viewModel.IsSearchMatch(110), Is.False);

            viewModel.SetSearchText(string.Empty);
            Assert.That(viewModel.VisibleRows.Count, Is.EqualTo(1));
        }

        [Test]
        public void TraceSearchNavigation_SelectsDirectMatchesAndWraps()
        {
            var session = new RecordingSession
            {
                TraceNodes = new[]
                {
                    TraceNode(100, 100, 0, "Root"),
                    TraceNode(100, 110, 100, "DamageSkill"),
                    TraceNode(100, 111, 110, "DamageEffect"),
                    TraceNode(100, 120, 100, "Unrelated")
                }
            };
            var viewModel = new BattleDebugDiagnosticTraceViewModel();
            viewModel.RefreshIfNeeded(session, 100);
            viewModel.SetSearchText("damage");

            Assert.That(viewModel.SelectSearchMatch(1), Is.True);
            Assert.That(viewModel.SelectedContextId, Is.EqualTo(110));
            Assert.That(viewModel.SelectSearchMatch(1), Is.True);
            Assert.That(viewModel.SelectedContextId, Is.EqualTo(111));
            Assert.That(viewModel.SelectSearchMatch(1), Is.True);
            Assert.That(viewModel.SelectedContextId, Is.EqualTo(110));
            Assert.That(viewModel.SelectSearchMatch(-1), Is.True);
            Assert.That(viewModel.SelectedContextId, Is.EqualTo(111));
            Assert.That(viewModel.GetVisibleRowIndex(111), Is.EqualTo(2));
        }

        [Test]
        public void TraceCollapseAll_PreservesSelectedPath_AndExpandAllRestoresRows()
        {
            var session = new RecordingSession
            {
                TraceNodes = new[]
                {
                    TraceNode(100, 100, 0, "Root"),
                    TraceNode(100, 110, 100, "Skill"),
                    TraceNode(100, 111, 110, "SelectedEffect"),
                    TraceNode(100, 120, 100, "OtherBranch"),
                    TraceNode(100, 121, 120, "OtherChild")
                }
            };
            var viewModel = new BattleDebugDiagnosticTraceViewModel();
            viewModel.RefreshIfNeeded(session, 100);
            viewModel.SelectContext(111);

            viewModel.CollapseAllPreservingSelection();

            Assert.That(viewModel.VisibleRows.Count, Is.EqualTo(4));
            Assert.That(viewModel.GetVisibleRowIndex(111), Is.EqualTo(2));
            Assert.That(viewModel.IsCollapsed(100), Is.False);
            Assert.That(viewModel.IsCollapsed(110), Is.False);
            Assert.That(viewModel.IsCollapsed(120), Is.True);
            Assert.That(viewModel.CollapsedBranchCount, Is.EqualTo(1));

            viewModel.ExpandAll();
            Assert.That(viewModel.VisibleRows.Count, Is.EqualTo(5));
            Assert.That(viewModel.CollapsedBranchCount, Is.Zero);
        }

        [Test]
        public void TracePin_ReturnsToPinnedNode_AndReportsEvictedNodeUnavailable()
        {
            var session = new RecordingSession
            {
                TraceNodes = new[]
                {
                    TraceNode(100, 100, 0, "Root"),
                    TraceNode(100, 110, 100, "Skill")
                }
            };
            var viewModel = new BattleDebugDiagnosticTraceViewModel();
            viewModel.RefreshIfNeeded(session, 100);
            viewModel.SelectContext(110);
            viewModel.PinSelection();
            viewModel.SelectContext(100);

            Assert.That(viewModel.PinnedContextId, Is.EqualTo(110));
            Assert.That(viewModel.SelectPinned(), Is.True);
            Assert.That(viewModel.SelectedContextId, Is.EqualTo(110));

            session.TraceStoreRevision++;
            session.TraceNodes = new[] { TraceNode(100, 100, 0, "Root") };
            viewModel.RefreshIfNeeded(session, 100);

            Assert.That(viewModel.PinnedContextId, Is.EqualTo(110));
            Assert.That(viewModel.IsPinnedContextAvailable, Is.False);
            Assert.That(viewModel.SelectPinned(), Is.False);
        }

        private static BattleDiagnosticTraceNodeSummary TraceNode(
            long rootContextId,
            long contextId,
            long parentContextId,
            string kind)
        {
            return new BattleDiagnosticTraceNodeSummary(
                RecordingSession.Scope,
                rootContextId,
                contextId,
                parentContextId,
                1,
                -1,
                BattleDiagnosticTraceNodeState.Active,
                kind: kind);
        }

        private sealed class RecordingSession : IBattleDiagnosticReadOnlySession
        {
            internal static readonly BattleDiagnosticSessionScope Scope =
                new BattleDiagnosticSessionScope("test", "world", 1);

            public RecordingSession(int worldInstanceId = 1)
            {
                SessionInfo = new BattleDiagnosticSessionInfo(
                    new BattleDiagnosticSessionScope("test", "world", worldInstanceId),
                    "test",
                    string.Empty,
                    1,
                    1,
                    BattleDiagnosticCapabilities.WorldState |
                    BattleDiagnosticCapabilities.ActorState |
                    BattleDiagnosticCapabilities.Events |
                    BattleDiagnosticCapabilities.Trace,
                    BattleDiagnosticConnectionState.Connected,
                    BattleDiagnosticCaptureState.Capturing);
            }

            public BattleDiagnosticSessionInfo SessionInfo { get; }

            public long EventStoreRevision { get; set; }
            public long StateStoreRevision { get; set; }
            public long TraceStoreRevision { get; set; }
            public long ActorAttributeStoreRevision { get; set; }
            public long ActorBuffStoreRevision { get; set; }
            public long ActorTagStoreRevision { get; set; }
            public long ActorEffectStoreRevision { get; set; }
            public IReadOnlyList<BattleDiagnosticActorSummary> Actors { get; set; }
            public IReadOnlyList<BattleDiagnosticActorTag> Tags { get; set; }
            public IReadOnlyList<BattleDiagnosticActorEffect> Effects { get; set; }
            public IReadOnlyList<BattleDiagnosticTraceNodeSummary> TraceNodes { get; set; }
            public BattleDiagnosticDataAvailability TraceAvailability { get; set; } =
                BattleDiagnosticDataAvailability.NotProduced;
            public bool TraceTruncated { get; set; }
            public long StoreRevision => EventStoreRevision;
            public int EventQueryCount { get; private set; }
            public int WorldQueryCount { get; private set; }
            public int ActorQueryCount { get; private set; }
            public int AttributeQueryCount { get; private set; }
            public int ModifierQueryCount { get; private set; }
            public int BuffQueryCount { get; private set; }
            public int TagQueryCount { get; private set; }
            public int EffectQueryCount { get; private set; }
            public int TraceQueryCount { get; private set; }
            public long LastTraceRootContextId { get; private set; }
            public int LastWorldFrame { get; private set; }
            public int LastActorFrame { get; private set; }
            public int LastAttributeFrame { get; private set; }
            public long LastAttributeActorId { get; private set; }
            public int LastBuffFrame { get; private set; }
            public long LastBuffActorId { get; private set; }
            public int LastTagFrame { get; private set; }
            public long LastTagActorId { get; private set; }
            public int LastEffectFrame { get; private set; }
            public long LastEffectActorId { get; private set; }

            public BattleDiagnosticQueryResult<BattleDiagnosticWorldSummary> QueryWorld(
                long requestId,
                int frame)
            {
                WorldQueryCount++;
                LastWorldFrame = frame;
                return BattleDiagnosticQueryResult<BattleDiagnosticWorldSummary>.Unavailable(
                    requestId,
                    StateStoreRevision,
                    BattleDiagnosticDataAvailability.NotProduced);
            }

            public BattleDiagnosticQueryResult<BattleDiagnosticActorSummary> QueryActors(
                long requestId,
                int frame)
            {
                ActorQueryCount++;
                LastActorFrame = frame;
                if (Actors != null)
                {
                    return BattleDiagnosticQueryResult<BattleDiagnosticActorSummary>.FromItems(
                        requestId, StateStoreRevision, new List<BattleDiagnosticActorSummary>(Actors), false);
                }

                return BattleDiagnosticQueryResult<BattleDiagnosticActorSummary>.Unavailable(
                    requestId,
                    StateStoreRevision,
                    BattleDiagnosticDataAvailability.NotProduced);
            }

            public BattleDiagnosticQueryResult<BattleDiagnosticEvent> QueryEvents(
                BattleDiagnosticEventQuery query)
            {
                EventQueryCount++;
                return BattleDiagnosticQueryResult<BattleDiagnosticEvent>.FromItems(
                    query.RequestId,
                    EventStoreRevision,
                    new List<BattleDiagnosticEvent>(),
                    false);
            }

            public BattleDiagnosticQueryResult<BattleDiagnosticTraceNodeSummary> QueryTrace(
                long requestId,
                long rootContextId)
            {
                TraceQueryCount++;
                LastTraceRootContextId = rootContextId;
                if (TraceNodes != null)
                {
                    return BattleDiagnosticQueryResult<BattleDiagnosticTraceNodeSummary>.FromItems(
                        requestId,
                        TraceStoreRevision,
                        new List<BattleDiagnosticTraceNodeSummary>(TraceNodes),
                        TraceTruncated);
                }

                return BattleDiagnosticQueryResult<BattleDiagnosticTraceNodeSummary>.Unavailable(
                    requestId,
                    TraceStoreRevision,
                    TraceAvailability);
            }

            public BattleDiagnosticQueryResult<BattleDiagnosticActorAttribute> QueryActorAttributes(
                long requestId,
                int frame,
                long actorId)
            {
                AttributeQueryCount++;
                LastAttributeFrame = frame;
                LastAttributeActorId = actorId;
                return BattleDiagnosticQueryResult<BattleDiagnosticActorAttribute>.Unavailable(
                    requestId,
                    ActorAttributeStoreRevision,
                    BattleDiagnosticDataAvailability.NotProduced);
            }

            public BattleDiagnosticQueryResult<BattleDiagnosticActorAttributeModifier> QueryActorAttributeModifiers(
                long requestId,
                int frame,
                long actorId)
            {
                ModifierQueryCount++;
                return BattleDiagnosticQueryResult<BattleDiagnosticActorAttributeModifier>.Unavailable(
                    requestId,
                    ActorAttributeStoreRevision,
                    BattleDiagnosticDataAvailability.NotProduced);
            }

            public BattleDiagnosticQueryResult<BattleDiagnosticActorBuff> QueryActorBuffs(
                long requestId,
                int frame,
                long actorId)
            {
                BuffQueryCount++;
                LastBuffFrame = frame;
                LastBuffActorId = actorId;
                return BattleDiagnosticQueryResult<BattleDiagnosticActorBuff>.Unavailable(
                    requestId,
                    ActorBuffStoreRevision,
                    BattleDiagnosticDataAvailability.NotProduced);
            }

            public BattleDiagnosticQueryResult<BattleDiagnosticActorTag> QueryActorTags(
                long requestId,
                int frame,
                long actorId)
            {
                TagQueryCount++;
                LastTagFrame = frame;
                LastTagActorId = actorId;
                if (Tags != null)
                {
                    return BattleDiagnosticQueryResult<BattleDiagnosticActorTag>.FromItems(
                        requestId, ActorTagStoreRevision, new List<BattleDiagnosticActorTag>(Tags), false);
                }

                return BattleDiagnosticQueryResult<BattleDiagnosticActorTag>.Unavailable(
                    requestId,
                    ActorTagStoreRevision,
                    BattleDiagnosticDataAvailability.NotProduced);
            }

            public BattleDiagnosticQueryResult<BattleDiagnosticActorEffect> QueryActorEffects(
                long requestId,
                int frame,
                long actorId)
            {
                EffectQueryCount++;
                LastEffectFrame = frame;
                LastEffectActorId = actorId;
                if (Effects != null)
                {
                    return BattleDiagnosticQueryResult<BattleDiagnosticActorEffect>.FromItems(
                        requestId, ActorEffectStoreRevision, new List<BattleDiagnosticActorEffect>(Effects), false);
                }

                return BattleDiagnosticQueryResult<BattleDiagnosticActorEffect>.Unavailable(
                    requestId,
                    ActorEffectStoreRevision,
                    BattleDiagnosticDataAvailability.NotProduced);
            }
        }
    }
}
