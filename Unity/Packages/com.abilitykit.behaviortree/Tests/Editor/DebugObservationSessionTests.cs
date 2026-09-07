#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Editor;
using AbilityKit.BehaviorTree.Editor.Debugging.Contributors;
using AbilityKit.BehaviorTree.Editor.Debugging.Observation;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
namespace AbilityKit.BehaviorTree.Editor.Tests
{
    public sealed class DebugObservationSessionTests
    {
        [Test]
        public void Capture_RecordsNodeAndBlackboardChanges()
        {
            var view = new FakeDebugView();
            var session = new DebugObservationSession();

            view.SetSample(1, NodeState.Running, 10);
            session.Capture(view);
            Assert.That(session.Events, Is.Empty);

            view.SetSample(2, NodeState.Success, 11);
            session.Capture(view);

            Assert.That(session.LastFrame, Is.EqualTo(2));
            Assert.That(session.Nodes[0].State, Is.EqualTo(NodeState.Success));
            Assert.That(session.HasBlackboardChanged("score"), Is.True);
            Assert.That(session.Events, Has.Count.EqualTo(2));
            Assert.That(session.Events[0].Source, Is.EqualTo("root"));
            Assert.That(session.Events[1].Source, Is.EqualTo("score"));
        }

        [Test]
        public void Capture_BoundsHistoryAndResetClearsSession()
        {
            var view = new FakeDebugView();
            var session = new DebugObservationSession(historyLimit: 2);

            for (var frame = 0; frame < 4; frame++)
            {
                view.SetSample(frame, frame % 2 == 0 ? NodeState.Running : NodeState.Success, frame);
                session.Capture(view);
            }

            Assert.That(session.Events, Has.Count.EqualTo(2));
            Assert.That(session.Events[0].Frame, Is.EqualTo(3));
            Assert.That(session.Events[1].Frame, Is.EqualTo(3));

            session.Reset();

            Assert.That(session.HasSample, Is.False);
            Assert.That(session.Nodes, Is.Empty);
            Assert.That(session.Events, Is.Empty);
            Assert.That(session.Blackboard, Is.Null);
        }

        private sealed class FakeDebugView : TreeDebugView
        {
            private List<NodeDebugInfo> _nodes = new();
            private BlackboardValueSnapshot _blackboard = new();

            public string TreeId => "session-test";
            public string DisplayName => TreeId;
            public string OwnerLabel => "test";
            public int NodeCount => 1;
            public int LastFrame { get; private set; }
            public TreeDefinition TreeDefinition { get; } = BuildDefinition();
            public IReadOnlyDictionary<string, string> NodeSourceTree => null;
            public IReadOnlyDictionary<string, string> NodeSourceNode => null;
            public IReadOnlyList<SubtreeInstance> SubtreeInstances => new List<SubtreeInstance>();

            public List<NodeDebugInfo> GetNodeStates() => new(_nodes);
            public BlackboardValueSnapshot GetBlackboard() => _blackboard;
            public TreeRuntimeSnapshot CaptureState() => new();

            public void SetSample(int frame, NodeState state, long score)
            {
                LastFrame = frame;
                _nodes = new List<NodeDebugInfo>
                {
                    new("root", "Root", BuiltInNodeTypes.Succeed, NodeKind.Action, state, 0, 1, -1),
                };
                _blackboard = new BlackboardValueSnapshot
                {
                    KeyNames = new List<string> { "score" },
                    KeyTypes = new List<ValueType> { ValueType.Int64 },
                    BoolValues = new List<bool> { false },
                    Int64Values = new List<long> { score },
                    Fixed64RawValues = new List<long> { 0 },
                    StringValues = new List<string> { "" },
                };
            }

            private static TreeDefinition BuildDefinition()
            {
                var definition = new TreeDefinition { TreeId = "session-test", RootNodeId = "root" };
                definition.Nodes.Add(new NodeDefinition { Id = "root", Type = BuiltInNodeTypes.Succeed });
                return definition;
            }
        }
    }

    // ======================================================================
    // P3 调试核心：snapshot / timeline / controller / contributor registry
    // ======================================================================

    public sealed class ObservationSnapshotTests
    {
        [Test]
        public void Capture_DeepCopiesSoLaterMutationDoesNotAffectSnapshot()
        {
            var view = new MutableDebugView("snap-tree", "Snap Tree");
            view.SetSample(1, NodeState.Running, onStack: 1, score: 10);

            var snapshot = ObservationSnapshot.Capture(7, 0, view);

            view.SetSample(2, NodeState.Success, onStack: 0, score: 99);

            Assert.That(snapshot.Frame, Is.EqualTo(1));
            Assert.That(snapshot.InstanceId, Is.EqualTo(7));
            Assert.That(snapshot.Nodes[0].State, Is.EqualTo(NodeState.Running));
            Assert.That(snapshot.Blackboard.GetDisplayValue("score"), Is.EqualTo("10"));
        }

        [Test]
        public void Capture_ComputesActiveNodesAndStateLookup()
        {
            var view = new MutableDebugView("snap-tree", "Snap Tree");
            view.SetNodes(1,
                ("root", NodeState.Running, 1),
                ("child", NodeState.Running, 2),
                ("idle", NodeState.Success, 0));

            var snapshot = ObservationSnapshot.Capture(1, 0, view);

            Assert.That(snapshot.ActiveNodeIds, Is.EquivalentTo(new[] { "root", "child" }));
            Assert.That(snapshot.StateOf("root"), Is.EqualTo(NodeState.Running));
            Assert.That(snapshot.StateOf("missing"), Is.EqualTo(NodeState.Inactive));
            Assert.That(snapshot.IsActive("child"), Is.True);
            Assert.That(snapshot.IsActive("idle"), Is.False);
        }
    }

    public sealed class ObservationTimelineTests
    {
        private static ObservationSnapshot Snapshot(int frame, long sequence, NodeState state, long score)
        {
            var view = new MutableDebugView("tl-tree", "TL Tree");
            view.SetSample(frame, state, state == NodeState.Running ? 1 : 0, score);
            return ObservationSnapshot.Capture(1, sequence, view);
        }

        [Test]
        public void Append_ComputesStructuredDiffAgainstPrevious()
        {
            var timeline = new ObservationTimeline();
            var first = Snapshot(1, 0, NodeState.Running, 10);
            var second = Snapshot(2, 1, NodeState.Success, 11);

            var diff0 = timeline.Append(first);
            var diff1 = timeline.Append(second);

            Assert.That(diff0.HasChanges, Is.False);
            Assert.That(diff1.HasChanges, Is.True);
            Assert.That(diff1.NodeChanges.Count, Is.EqualTo(1));
            Assert.That(diff1.NodeChanges[0].From, Is.EqualTo(NodeState.Running));
            Assert.That(diff1.NodeChanges[0].To, Is.EqualTo(NodeState.Success));
            Assert.That(diff1.ChangedBlackboardKeys, Is.EquivalentTo(new[] { "score" }));
            Assert.That(timeline.Count, Is.EqualTo(2));
        }

        [Test]
        public void Append_BoundsHistoryToLimit()
        {
            var timeline = new ObservationTimeline(sampleLimit: 2);
            for (var i = 0; i < 4; i++) timeline.Append(Snapshot(i, i, NodeState.Success, i));

            Assert.That(timeline.Count, Is.EqualTo(2));
            Assert.That(timeline.Samples[0].Frame, Is.EqualTo(2));
            Assert.That(timeline.Latest.Frame, Is.EqualTo(3));
        }

        [Test]
        public void FindFrame_NavigatesToMostRecentSampleAtOrBeforeFrame()
        {
            var timeline = new ObservationTimeline();
            timeline.Append(Snapshot(1, 0, NodeState.Running, 1));
            timeline.Append(Snapshot(5, 1, NodeState.Running, 2));

            Assert.That(timeline.FindFrame(5).Frame, Is.EqualTo(5));
            Assert.That(timeline.FindFrame(4).Frame, Is.EqualTo(1));
            Assert.That(timeline.FindFrame(0).Frame, Is.EqualTo(1));
        }

        [Test]
        public void Compare_ComputesAbDiffBetweenTwoHistoricalSamples()
        {
            var timeline = new ObservationTimeline();
            timeline.Append(Snapshot(1, 0, NodeState.Running, 10));
            timeline.Append(Snapshot(2, 1, NodeState.Success, 11));

            var diff = timeline.Compare(0, 1);
            Assert.That(diff.NodeChanges.Count, Is.EqualTo(1));
            Assert.That(diff.ChangedBlackboardKeys, Is.EquivalentTo(new[] { "score" }));
        }

        [Test]
        public void EnumerateChanges_FlattensNodeAndBlackboardEvents()
        {
            var timeline = new ObservationTimeline();
            timeline.Append(Snapshot(1, 0, NodeState.Running, 10));
            timeline.Append(Snapshot(2, 1, NodeState.Success, 11));

            var changes = new List<ObservationChange>(timeline.EnumerateChanges());
            Assert.That(changes, Has.Count.EqualTo(2));
            Assert.That(changes[0].Kind, Is.EqualTo(ObservationChangeKind.NodeState));
            Assert.That(changes[0].From, Is.EqualTo("Running"));
            Assert.That(changes[0].To, Is.EqualTo("Success"));
            Assert.That(changes[1].Kind, Is.EqualTo(ObservationChangeKind.BlackboardValue));
            Assert.That(changes[1].Target, Is.EqualTo("score"));
            Assert.That(changes[1].From, Is.EqualTo("10"));
            Assert.That(changes[1].To, Is.EqualTo("11"));
        }
    }

    public sealed class ObservationControllerTests
    {
        [TearDown]
        public void TearDown() => DebugRegistry.ClearForTests();

        [Test]
        public void State_TransitionsNoSampleLiveFrozenDisconnected()
        {
            var view = new MutableDebugView("ctrl-tree", "Ctrl Tree");
            var handle = DebugRegistry.Register(view);
            view.SetSample(0, NodeState.Running, onStack: 1, score: 0);

            var controller = new ObservationController(sampleIntervalSeconds: 0.1d);
            Assert.That(controller.State, Is.EqualTo(ObservationSessionState.NoSample));

            controller.Poll(0.0d);
            Assert.That(controller.SelectedInstanceId, Is.Not.EqualTo(0));
            Assert.That(controller.State, Is.EqualTo(ObservationSessionState.Live));

            controller.Pause();
            Assert.That(controller.State, Is.EqualTo(ObservationSessionState.Frozen));

            controller.Resume();
            Assert.That(controller.State, Is.EqualTo(ObservationSessionState.Live));

            DebugRegistry.Unregister(handle);
            controller.Poll(0.3d);
            Assert.That(controller.State, Is.EqualTo(ObservationSessionState.Disconnected));
        }

        [Test]
        public void Disconnected_RetainsLastImmutableSnapshot()
        {
            var view = new MutableDebugView("ctrl-tree", "Ctrl Tree");
            var handle = DebugRegistry.Register(view);
            view.SetSample(1, NodeState.Running, onStack: 1, score: 10);

            var controller = new ObservationController(sampleIntervalSeconds: 0.1d);
            controller.Poll(0.0d);

            Assert.That(controller.Latest.Frame, Is.EqualTo(1));
            Assert.That(controller.Latest.Blackboard.GetDisplayValue("score"), Is.EqualTo("10"));

            DebugRegistry.Unregister(handle);
            view.SetSample(2, NodeState.Success, onStack: 0, score: 99);
            controller.Poll(0.2d);

            Assert.That(controller.State, Is.EqualTo(ObservationSessionState.Disconnected));
            Assert.That(controller.Latest.Frame, Is.EqualTo(1));
            Assert.That(controller.Latest.Blackboard.GetDisplayValue("score"), Is.EqualTo("10"));
        }

        [Test]
        public void Poll_SamplesOnlyAfterIntervalElapses()
        {
            var view = new MutableDebugView("ctrl-tree", "Ctrl Tree");
            DebugRegistry.Register(view);
            view.SetSample(0, NodeState.Running, onStack: 1, score: 0);

            var controller = new ObservationController(sampleIntervalSeconds: 0.5d);
            controller.Poll(0.0d);
            Assert.That(controller.Timeline.Count, Is.EqualTo(1));

            controller.Poll(0.2d);
            Assert.That(controller.Timeline.Count, Is.EqualTo(1));

            controller.Poll(0.5d);
            Assert.That(controller.Timeline.Count, Is.EqualTo(2));
        }

        [Test]
        public void Pause_StopsAutoSamplingButAllowsExplicitStep()
        {
            var view = new MutableDebugView("ctrl-tree", "Ctrl Tree");
            DebugRegistry.Register(view);
            view.SetSample(0, NodeState.Running, onStack: 1, score: 0);

            var controller = new ObservationController(sampleIntervalSeconds: 0.05d);
            controller.Poll(0.0d);
            controller.Pause();

            controller.Poll(1.0d);
            Assert.That(controller.Timeline.Count, Is.EqualTo(1));

            var step = controller.Sample();
            Assert.That(step, Is.Not.Null);
            Assert.That(controller.Timeline.Count, Is.EqualTo(2));
        }

        [Test]
        public void SelectInstance_SwitchesAndClearsHistory()
        {
            var viewA = new MutableDebugView("tree-a", "A");
            var viewB = new MutableDebugView("tree-b", "B");
            DebugRegistry.Register(viewA);
            DebugRegistry.Register(viewB);
            viewA.SetSample(0, NodeState.Running, onStack: 1, score: 0);
            viewB.SetSample(0, NodeState.Success, onStack: 1, score: 0);

            var controller = new ObservationController(sampleIntervalSeconds: 0.05d);
            controller.Poll(0.0d);
            var idA = controller.Entries[0].Id;
            var idB = controller.Entries[1].Id;
            Assert.That(controller.SelectedInstanceId, Is.EqualTo(idA));

            controller.Poll(0.1d);
            Assert.That(controller.Timeline.Count, Is.EqualTo(2));

            Assert.That(controller.SelectInstance(idB), Is.True);
            Assert.That(controller.Timeline.Count, Is.EqualTo(0));

            controller.Poll(0.2d);
            Assert.That(controller.Timeline.Count, Is.EqualTo(1));
            Assert.That(controller.Latest.TreeId, Is.EqualTo("tree-b"));
        }
    }

    public sealed class ObservationContributorRegistryTests
    {
        private sealed class FakeDetail : IObservationDetailContributor
        {
            private readonly Func<IReadOnlyList<ObservationDetailSection>> _produce;

            public string Id { get; }
            public int Priority { get; }

            public FakeDetail(string id, int priority, Func<IReadOnlyList<ObservationDetailSection>> produce)
            {
                Id = id;
                Priority = priority;
                _produce = produce;
            }

            public IReadOnlyList<ObservationDetailSection> GetSections(ObservationDetailContext context) => _produce();
        }

        private static readonly Func<IReadOnlyList<ObservationDetailSection>> Empty =
            () => Array.Empty<ObservationDetailSection>();

        [Test]
        public void Register_OrdersDetailsByPriorityWithLaterRegistrationFirstOnTie()
        {
            var registry = new ObservationContributorRegistry();
            registry.Register(new FakeDetail("low", 10, Empty));
            registry.Register(new FakeDetail("high", 0, Empty));
            registry.Register(new FakeDetail("tie-first", 5, Empty));
            registry.Register(new FakeDetail("tie-second", 5, Empty));

            Assert.That(registry.Details[0].Id, Is.EqualTo("high"));
            Assert.That(registry.Details[1].Id, Is.EqualTo("tie-second"));
            Assert.That(registry.Details[2].Id, Is.EqualTo("tie-first"));
            Assert.That(registry.Details[3].Id, Is.EqualTo("low"));
        }

        [Test]
        public void Register_RejectsDuplicateId()
        {
            var registry = new ObservationContributorRegistry();
            registry.Register(new FakeDetail("dup", 0, Empty));
            Assert.Throws<ArgumentException>(() => registry.Register(new FakeDetail("dup", 1, Empty)));
        }

        [Test]
        public void Dispose_UnregistersContributorAndIsIdempotent()
        {
            var registry = new ObservationContributorRegistry();
            var handle = registry.Register(new FakeDetail("x", 0, Empty));
            Assert.That(registry.Details, Has.Count.EqualTo(1));

            handle.Dispose();
            Assert.That(registry.Details, Is.Empty);

            handle.Dispose();
            Assert.That(registry.Details, Is.Empty);
        }

        [Test]
        public void CollectSections_IsolatesThrowingContributor()
        {
            var registry = new ObservationContributorRegistry();
            registry.Register(new FakeDetail("good", 0, () => new[]
            {
                new ObservationDetailSection("S", new[] { new ObservationDetailRow("k", "v") }),
            }));
            registry.Register(new FakeDetail("bad", 1, () => throw new InvalidOperationException("boom")));

            var sections = registry.CollectSections(new ObservationDetailContext(1, null, null));

            Assert.That(sections, Has.Count.EqualTo(1));
            Assert.That(registry.Errors, Has.Count.EqualTo(1));
            Assert.That(registry.Errors[0].ContributorId, Is.EqualTo("bad"));
        }

        [Test]
        public void AnyFilterMatches_IsolatesThrowingFilter()
        {
            var registry = new ObservationContributorRegistry();
            registry.Register(new ObservationFilter("bad", "Bad", _ => throw new InvalidOperationException("boom")));

            var view = new MutableDebugView("f-tree", "F");
            view.SetNodes(1, ("root", NodeState.Running, 1));
            var snap = ObservationSnapshot.Capture(1, 0, view);

            Assert.That(registry.AnyFilterMatches(ObservationFilterContext.ForNode(snap, snap.Nodes[0], null)), Is.False);
            Assert.That(registry.Errors, Has.Count.EqualTo(1));
        }

        [Test]
        public void ScopedFilter_DoesNotHideCandidatesFromOtherScopes()
        {
            var registry = new ObservationContributorRegistry();
            registry.Register(ObservationFilters.RunningPath());
            var view = new MutableDebugView("scope-tree", "Scope Tree");
            var handle = DebugRegistry.Register(view);

            try
            {
                var entry = DebugRegistry.GetEntries()
                    .Single(candidate => candidate.View.TreeId == view.TreeId);
                Assert.That(
                    registry.AnyFilterMatches(ObservationFilterContext.ForInstance(entry)),
                    Is.True);
            }
            finally
            {
                DebugRegistry.Unregister(handle);
            }
        }

        [Test]
        public void DebugRegistry_Register_DoesNotAdvertiseDeltaForPlainView()
        {
            var view = new MutableDebugView("plain-tree", "Plain Tree");
            var handle = DebugRegistry.Register(view);

            try
            {
                var registered = DebugRegistry.GetEntries()
                    .Single(candidate => candidate.View.TreeId == view.TreeId)
                    .View;

                Assert.That(registered, Is.Not.InstanceOf<TreeDebugDeltaView>());
            }
            finally
            {
                DebugRegistry.Unregister(handle);
            }
        }

        [Test]
        public void DebugRegistry_Register_PreservesDeltaViewCapability()
        {
            var view = new DeltaDebugView();
            var handle = DebugRegistry.Register(view);

            try
            {
                var registered = DebugRegistry.GetEntries()
                    .Single(candidate => candidate.View.TreeId == view.TreeId)
                    .View;
                Assert.That(registered, Is.InstanceOf<TreeDebugDeltaView>());

                var deltaView = (TreeDebugDeltaView)registered;
                Assert.That(deltaView.DebugSequence, Is.EqualTo(17));
                var delta = deltaView.CaptureDebugDelta(16, includeBlackboard: true);
                Assert.That(delta.Sequence, Is.EqualTo(17));
                Assert.That(delta.IsFull, Is.False);
                Assert.That(delta.LastFrame, Is.EqualTo(23));
                Assert.That(delta.Nodes, Has.Count.EqualTo(1));
                Assert.That(delta.Nodes[0].NodeId, Is.EqualTo("root"));
                Assert.That(delta.Blackboard, Is.Not.Null);
                Assert.That(delta.Blackboard.KeyNames, Is.EqualTo(new[] { "score" }));
            }
            finally
            {
                DebugRegistry.Unregister(handle);
            }
        }

        private sealed class DeltaDebugView : TreeDebugView, TreeDebugDeltaView
        {
            public string TreeId => "delta-tree";
            public string DisplayName => "Delta Tree";
            public string OwnerLabel => "test";
            public int NodeCount => 1;
            public int LastFrame => 23;
            public long DebugSequence => 17;
            public TreeDefinition TreeDefinition { get; } = new()
            {
                TreeId = "delta-tree",
                RootNodeId = "root",
                Nodes =
                {
                    new NodeDefinition { Id = "root", Type = BuiltInNodeTypes.Succeed },
                },
            };
            public IReadOnlyDictionary<string, string> NodeSourceTree => null;
            public IReadOnlyDictionary<string, string> NodeSourceNode => null;
            public IReadOnlyList<SubtreeInstance> SubtreeInstances => Array.Empty<SubtreeInstance>();

            public List<NodeDebugInfo> GetNodeStates() => new()
            {
                new NodeDebugInfo(
                    "root", "Root", BuiltInNodeTypes.Succeed, NodeKind.Action,
                    NodeState.Running, 0, 1, -1),
            };

            public BlackboardValueSnapshot GetBlackboard() => CreateBlackboard();
            public TreeRuntimeSnapshot CaptureState() => new();

            public TreeDebugDelta CaptureDebugDelta(long knownSequence, bool includeBlackboard) => new()
            {
                Sequence = DebugSequence,
                IsFull = knownSequence == 0,
                LastFrame = LastFrame,
                Nodes = GetNodeStates(),
                Blackboard = includeBlackboard ? CreateBlackboard() : null,
            };

            private static BlackboardValueSnapshot CreateBlackboard() => new()
            {
                KeyNames = new List<string> { "score" },
                KeyTypes = new List<ValueType> { ValueType.Int64 },
                Int64Values = new List<long> { 42L },
            };
        }

        [Test]
        public void Filters_ComposeAndMatchExpectedCandidates()
        {
            var view = new MutableDebugView("f-tree", "F Tree");
            view.SetNodes(1,
                ("root", NodeState.Running, 1),
                ("child", NodeState.Running, 2),
                ("idle", NodeState.Success, 0));
            var snap = ObservationSnapshot.Capture(1, 0, view);
            var rootNode = snap.Nodes[0];
            var childNode = snap.Nodes[1];
            var idleNode = snap.Nodes[2];

            var runningPath = ObservationFilters.RunningPath();
            Assert.That(runningPath.Matches(ObservationFilterContext.ForNode(snap, rootNode, null)), Is.True);
            Assert.That(runningPath.Matches(ObservationFilterContext.ForNode(snap, idleNode, null)), Is.False);

            var successState = ObservationFilters.NodeState(NodeState.Success);
            Assert.That(successState.Matches(ObservationFilterContext.ForNode(snap, idleNode, null)), Is.True);
            Assert.That(successState.Matches(ObservationFilterContext.ForNode(snap, rootNode, null)), Is.False);

            var text = ObservationFilters.Text("child");
            Assert.That(text.Matches(ObservationFilterContext.ForNode(snap, childNode, null)), Is.True);
            Assert.That(text.Matches(ObservationFilterContext.ForNode(snap, idleNode, null)), Is.False);

            var notRunningPath = ObservationFilters.Not(runningPath);
            Assert.That(notRunningPath.Matches(ObservationFilterContext.ForNode(snap, idleNode, null)), Is.True);

            var and = ObservationFilters.And(ObservationFilters.Text("child"), runningPath);
            Assert.That(and.Matches(ObservationFilterContext.ForNode(snap, childNode, null)), Is.True);
            Assert.That(and.Matches(ObservationFilterContext.ForNode(snap, idleNode, null)), Is.False);
        }

        [Test]
        public void ChangedOnlyFilter_MatchesNodesAndKeysInDiff()
        {
            var view = new MutableDebugView("f-tree", "F Tree");
            view.SetSample(1, NodeState.Running, onStack: 1, score: 10);
            var a = ObservationSnapshot.Capture(1, 0, view);
            view.SetSample(2, NodeState.Success, onStack: 1, score: 11);
            var b = ObservationSnapshot.Capture(1, 1, view);
            var diff = ObservationDiff.Compare(a, b);

            var changedOnly = ObservationFilters.ChangedOnly();
            Assert.That(changedOnly.Matches(ObservationFilterContext.ForNode(b, b.Nodes[0], diff)), Is.True);
            Assert.That(changedOnly.Matches(ObservationFilterContext.ForBlackboardKey(b, "score", "11", diff)), Is.True);
            Assert.That(changedOnly.Matches(ObservationFilterContext.ForBlackboardKey(b, "other", "", diff)), Is.False);
        }

        [Test]
        public void NodeStateOverlay_ProducesKindByState()
        {
            var contributor = ObservationOverlayContributors.NodeState();
            var view = new MutableDebugView("o-tree", "O Tree");
            view.SetNodes(1,
                ("run", NodeState.Running, 1),
                ("ok", NodeState.Success, 0),
                ("fail", NodeState.Failure, 0),
                ("idle", NodeState.Inactive, 0));
            var snap = ObservationSnapshot.Capture(1, 0, view);

            var running = contributor.GetOverlays(new ObservationOverlayContext(1, snap, snap.Nodes[0]));
            Assert.That(running[0].Kind, Is.EqualTo(ObservationOverlayKind.Border));
            Assert.That(running[0].Text, Is.EqualTo("Running"));

            var success = contributor.GetOverlays(new ObservationOverlayContext(1, snap, snap.Nodes[1]));
            Assert.That(success[0].Kind, Is.EqualTo(ObservationOverlayKind.Badge));

            var failure = contributor.GetOverlays(new ObservationOverlayContext(1, snap, snap.Nodes[2]));
            Assert.That(failure[0].Kind, Is.EqualTo(ObservationOverlayKind.Badge));

            var inactive = contributor.GetOverlays(new ObservationOverlayContext(1, snap, snap.Nodes[3]));
            Assert.That(inactive[0].Kind, Is.EqualTo(ObservationOverlayKind.Marker));
        }
    }

    public sealed class ObservationBlackboardViewTests
    {
        [Test]
        public void Create_ExposesCurrentPreviousChangedOnlyAndSearch()
        {
            var view = new MutableDebugView("bb-tree", "BB Tree");
            view.SetSample(1, NodeState.Running, onStack: 1, score: 10);
            var previous = ObservationSnapshot.Capture(1, 0, view);
            view.SetBlackboardValues(("score", 11), ("hp", 20));
            var current = ObservationSnapshot.Capture(1, 1, view);
            var diff = ObservationDiff.Compare(previous, current);

            var blackboard = ObservationBlackboardView.Create(current, previous, diff);

            Assert.That(blackboard.TryGetRow("score", out var score), Is.True);
            Assert.That(score.CurrentValue, Is.EqualTo("11"));
            Assert.That(score.PreviousValue, Is.EqualTo("10"));
            Assert.That(score.IsChanged, Is.True);
            Assert.That(blackboard.Search("sco", changedOnly: true).Select(row => row.Key), Is.EqualTo(new[] { "score" }));
            Assert.That(blackboard.Search("20").Select(row => row.Key), Does.Contain("hp"));
        }
    }

    public sealed class AuthoringGraphViewObservationProjectionTests
    {
        [Test]
        public void ApplyObservationProjection_UpdatesChangedNodesWithoutReapplyingUnchangedNodes()
        {
            var document = BuildGraphDocument();
            var graph = BuildGraph(document);
            var view = new MutableDebugView("graph-tree", "Graph Tree");

            view.SetNodes(1,
                ("root", NodeState.Running, 1, 0),
                ("childA", NodeState.Running, 1, -1),
                ("childB", NodeState.Inactive, 0, -1));
            var first = ObservationSnapshot.Capture(7, 0, view);
            graph.ApplyObservationProjection(first, null, new ObservationContributorRegistry());

            var root = graph.GetNodeViewForTests("root");
            var childA = graph.GetNodeViewForTests("childA");
            var childB = graph.GetNodeViewForTests("childB");
            Assert.That(root, Is.Not.Null);
            Assert.That(childA, Is.Not.Null);
            Assert.That(childB, Is.Not.Null);
            var rootCount = root.ObservationApplyCount;
            var childACount = childA.ObservationApplyCount;
            var childBCount = childB.ObservationApplyCount;

            view.SetNodes(2,
                ("root", NodeState.Running, 1, 0),
                ("childA", NodeState.Success, 0, -1),
                ("childB", NodeState.Inactive, 0, -1));
            var second = ObservationSnapshot.Capture(7, 1, view);
            graph.ApplyObservationProjection(second, ObservationDiff.Compare(first, second), new ObservationContributorRegistry());

            Assert.That(root.ObservationApplyCount, Is.GreaterThanOrEqualTo(rootCount));
            Assert.That(childA.ObservationApplyCount, Is.EqualTo(childACount + 1));
            Assert.That(childB.ObservationApplyCount, Is.EqualTo(childBCount));
        }

        [Test]
        public void ApplyObservationProjection_ProjectsOverlayKindsAndActiveEdges()
        {
            var document = BuildGraphDocument();
            var graph = BuildGraph(document);
            var registry = new ObservationContributorRegistry();
            registry.Register(new StaticOverlayContributor());
            var view = new MutableDebugView("graph-tree", "Graph Tree");

            view.SetNodes(1,
                ("root", NodeState.Running, 1, 0),
                ("childA", NodeState.Running, 1, -1),
                ("childB", NodeState.Inactive, 0, -1));
            var first = ObservationSnapshot.Capture(7, 0, view);
            graph.ApplyObservationProjection(first, null, registry);

            var childA = graph.GetNodeViewForTests("childA");
            Assert.That(childA.tooltip, Does.Contain("tip-childA"));
            var childAText = string.Join("\n", childA.Query<Label>().ToList().Select(label => label.text));
            Assert.That(childAText, Does.Contain("badge-childA"));
            Assert.That(childAText, Does.Contain("marker-childA"));
            Assert.That(graph.GetEdgeForTests("childA", "root").edgeControl.edgeWidth, Is.EqualTo(4f));

            view.SetNodes(2,
                ("root", NodeState.Running, 1, 1),
                ("childA", NodeState.Success, 0, -1),
                ("childB", NodeState.Running, 1, -1));
            var second = ObservationSnapshot.Capture(7, 1, view);
            graph.ApplyObservationProjection(second, ObservationDiff.Compare(first, second), registry);

            Assert.That(graph.GetEdgeForTests("childA", "root").edgeControl.edgeWidth, Is.EqualTo(2f));
            Assert.That(graph.GetEdgeForTests("childB", "root").edgeControl.edgeWidth, Is.EqualTo(4f));
        }

        private static AuthoringSourceDocument BuildGraphDocument()
        {
            var document = new AuthoringSourceDocument();
            document.Tree.TreeId = "graph-tree";
            document.Tree.RootNodeId = "root";
            document.Tree.Nodes.Add(new NodeDefinition
            {
                Id = "root",
                Type = BuiltInNodeTypes.Sequence,
                ChildIds = { "childA", "childB" },
            });
            document.Tree.Nodes.Add(new NodeDefinition { Id = "childA", Type = BuiltInNodeTypes.Succeed });
            document.Tree.Nodes.Add(new NodeDefinition { Id = "childB", Type = BuiltInNodeTypes.Succeed });
            document.Layout.Add(new NodeLayoutData { NodeId = "root", X = 0, Y = 0 });
            document.Layout.Add(new NodeLayoutData { NodeId = "childA", X = 0, Y = 160 });
            document.Layout.Add(new NodeLayoutData { NodeId = "childB", X = 220, Y = 160 });
            return document;
        }

        private static AuthoringGraphView BuildGraph(AuthoringSourceDocument document)
        {
            var graph = new AuthoringGraphView(new GraphHost(document));
            foreach (var node in document.Tree.Nodes) graph.AddNodeView(node);
            foreach (var node in document.Tree.Nodes)
            {
                foreach (var childId in node.ChildIds) graph.Connect(childId, node.Id);
            }
            return graph;
        }

        private sealed class StaticOverlayContributor : IObservationOverlayContributor
        {
            public string Id => "test.overlay";
            public int Priority => 0;

            public IReadOnlyList<ObservationOverlay> GetOverlays(ObservationOverlayContext context) =>
                new[]
                {
                    new ObservationOverlay(context.Node.NodeId, ObservationOverlayKind.Badge, "badge-" + context.Node.NodeId),
                    new ObservationOverlay(context.Node.NodeId, ObservationOverlayKind.Marker, "marker-" + context.Node.NodeId),
                    new ObservationOverlay(context.Node.NodeId, ObservationOverlayKind.Tooltip, "tip-" + context.Node.NodeId),
                    new ObservationOverlay(context.Node.NodeId, ObservationOverlayKind.Border, "border-" + context.Node.NodeId),
                };
        }

        private sealed class GraphHost : IAuthoringGraphHost
        {
            public GraphHost(AuthoringSourceDocument document) => Document = document;

            public AuthoringSourceDocument Document { get; }
            public bool IsReadOnly => true;
            public void OnGraphSelectionChanged(NodeDefinition? node) { }
            public void RecordChange() { }
            public void RecordChange(string beforeChangeSnapshot) { }
            public bool CanConnect(string childId, string parentId, out string error)
            {
                error = "";
                return true;
            }
            public void SetConnected(string childId, string parentId, bool connected) { }
            public string ResolveNodeDisplayName(NodeDefinition node) => node.Id;
            public int ResolveChildOrder(string nodeId) => 0;
            public Vector2 ScreenToGraphPosition(Vector2 screenPosition) => screenPosition;
            public void AddNode(NodeDescriptor descriptor, Vector2 graphPosition) { }
        }
    }

    public sealed class AuthoringInspectorRendererObservationTests
    {
        [Test]
        public void RuntimeDetails_ReadNodeAndBlackboardFromDisplayedSnapshot()
        {
            var document = BuildDocument(out var node);
            var view = new MutableDebugView("inspector-tree", "Inspector Tree");
            view.SetSample(1, NodeState.Running, onStack: 1, score: 10);
            var displayed = ObservationSnapshot.Capture(42, 0, view);

            view.SetSample(2, NodeState.Success, onStack: 0, score: 99);

            var root = new ScrollView();
            var renderer = new AuthoringInspectorRenderer(
                root,
                new SnapshotInspectorHost(document, displayed));

            renderer.Render(node);
            renderer.RefreshRuntimeDetails();

            var text = string.Join("\n", root.Query<Label>().ToList().Select(label => label.text));
            Assert.That(text, Does.Contain("Running"));
            Assert.That(text, Does.Contain("score = 10"));
            Assert.That(text, Does.Not.Contain("score = 99"));
        }

        private static AuthoringSourceDocument BuildDocument(out NodeDefinition node)
        {
            var document = new AuthoringSourceDocument();
            document.Tree.TreeId = "inspector-tree";
            document.Tree.RootNodeId = "root";
            document.Tree.Blackboard.Keys.Add(new BlackboardKeyDefinition
            {
                Name = "score",
                Type = ValueType.Int64,
            });
            node = new NodeDefinition
            {
                Id = "root",
                Type = BuiltInNodeTypes.BlackboardCompare,
            };
            node.Properties.Set(BlackboardCompareNode.LeftKeyProperty, PropertyValue.Of("score"));
            document.Tree.Nodes.Add(node);
            return document;
        }

        private sealed class SnapshotInspectorHost : IAuthoringInspectorHost
        {
            public SnapshotInspectorHost(AuthoringSourceDocument document, ObservationSnapshot snapshot)
            {
                Document = document;
                DisplayedObservationSnapshot = snapshot;
            }

            public AuthoringSourceDocument Document { get; }
            public bool IsReadOnly => true;
            public ObservationSnapshot? DisplayedObservationSnapshot { get; }
            public ObservationSnapshot? PreviousObservationSnapshot => null;
            public ObservationDiff? DisplayedObservationDiff => null;

            public string ResolveNodeDisplayName(NodeDefinition node) => node.Id;
            public void RecordChange() { }
            public void RecordChange(string beforeChangeSnapshot) { }
            public void RefreshNodeTitles() { }
            public void RebuildGraph() { }
            public void RefreshChrome() { }
            public void FocusNode(string nodeId) { }
        }
    }

    public sealed class ObservationP3RecordingTests
    {
        [Test]
        public void Recording_RoundTripsTimelineThroughStableJson()
        {
            var view = new MutableDebugView("record-tree", "Record Tree");
            var timeline = new ObservationTimeline(sampleLimit: 8);

            view.SetSample(1, NodeState.Running, onStack: 1, score: 10);
            timeline.Append(ObservationSnapshot.Capture(3, 0, view));
            view.SetSample(2, NodeState.Success, onStack: 0, score: 11);
            timeline.Append(ObservationSnapshot.Capture(3, 1, view));

            var json = ObservationRecording.ToJson(timeline, prettyPrint: false);
            Assert.That(json, Does.Contain("FormatVersion"));
            Assert.That(json, Does.Contain("Samples"));

            var loaded = ObservationRecording.TimelineFromJson(json);
            Assert.That(loaded.Count, Is.EqualTo(2));
            Assert.That(loaded.Latest.TreeId, Is.EqualTo("record-tree"));
            Assert.That(loaded.Latest.Frame, Is.EqualTo(2));
            Assert.That(loaded.Latest.Blackboard.GetDisplayValue("score"), Is.EqualTo("11"));
            Assert.That(loaded.LatestDiff.ChangedBlackboardKeys, Is.EquivalentTo(new[] { "score" }));
        }

        [Test]
        public void Recording_ExportsImportsFileAndReplayNavigates()
        {
            var view = new MutableDebugView("replay-tree", "Replay Tree");
            var timeline = new ObservationTimeline(sampleLimit: 4);
            view.SetSample(10, NodeState.Running, onStack: 1, score: 1);
            timeline.Append(ObservationSnapshot.Capture(9, 0, view));
            view.SetSample(11, NodeState.Failure, onStack: 0, score: 2);
            timeline.Append(ObservationSnapshot.Capture(9, 1, view));

            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "bt-observation-recording-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                ObservationRecording.ExportToFile(path, timeline);
                var replay = ObservationRecording.ImportReplayFromFile(path);

                Assert.That(replay.Count, Is.EqualTo(2));
                Assert.That(replay.Current.Frame, Is.EqualTo(11));
                Assert.That(replay.StepPrevious(), Is.True);
                Assert.That(replay.Current.Frame, Is.EqualTo(10));
                Assert.That(replay.StepPrevious(), Is.False);
                Assert.That(replay.StepNext(), Is.True);
                Assert.That(replay.CurrentDiff.NodeChanges[0].To, Is.EqualTo(NodeState.Failure));
            }
            finally
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        [Test]
        public void OfflineReplay_ScrubsPlaysSpeedAndCompares()
        {
            var view = new MutableDebugView("replay-tree", "Replay Tree");
            var timeline = new ObservationTimeline(sampleLimit: 4);
            for (var i = 0; i < 4; i++)
            {
                view.SetSample(10 + i, i % 2 == 0 ? NodeState.Running : NodeState.Success, 1, i);
                timeline.Append(ObservationSnapshot.Capture(9, i, view));
            }

            var replay = new ObservationOfflineReplay(timeline);

            Assert.That(replay.SeekNormalized(0f), Is.True);
            Assert.That(replay.Current.Frame, Is.EqualTo(10));
            replay.MarkCompareA();
            Assert.That(replay.SeekNormalized(1f), Is.True);
            replay.MarkCompareB();
            Assert.That(replay.CompareDiff.HasChanges, Is.True);

            replay.Seek(0);
            replay.PlaybackSpeed = 2d;
            replay.Play();
            replay.Tick(0.49d);
            Assert.That(replay.CurrentIndex, Is.EqualTo(0));
            replay.Tick(0.01d);
            Assert.That(replay.CurrentIndex, Is.EqualTo(1));
            replay.Tick(10d);
            Assert.That(replay.IsPlaying, Is.False);
            Assert.That(replay.CurrentIndex, Is.EqualTo(3));
        }

        [Test]
        public void EditorTransport_AppliesResetAndSnapshotFramesToTimelineSink()
        {
            var view = new MutableDebugView("transport-tree", "Transport Tree");
            var timeline = new ObservationTimeline(sampleLimit: 4);
            var sink = new ObservationTimelineSnapshotSink(timeline);

            view.SetSample(1, NodeState.Running, 1, 10);
            var snapshot = ObservationSnapshot.Capture(5, 0, view);

            Assert.That(ObservationEditorTransport.TryApply(
                ObservationEditorTransport.FullSnapshot(snapshot),
                sink), Is.True);
            Assert.That(timeline.Count, Is.EqualTo(1));
            Assert.That(timeline.Latest.Frame, Is.EqualTo(1));

            Assert.That(ObservationEditorTransport.TryApply(
                ObservationEditorTransport.Reset(),
                sink), Is.True);
            Assert.That(timeline.Count, Is.EqualTo(0));
        }

        [Test]
        public void EditorTransport_RuntimeCopyPayloadUsesImmutableObservationSnapshot()
        {
            var view = new MutableDebugView("copy-tree", "Copy Tree");
            view.SetSample(1, NodeState.Running, 1, 10);
            var snapshot = ObservationSnapshot.Capture(5, 0, view);

            view.SetSample(2, NodeState.Success, 0, 99);
            var json = ObservationEditorTransport.CreateRuntimeSnapshotJson(snapshot);
            var runtime = TreeJson.LoadSnapshot(json);

            Assert.That(view.CaptureStateCalls, Is.EqualTo(0));
            Assert.That(runtime.Nodes[0].State, Is.EqualTo(NodeState.Running));
            Assert.That(runtime.Blackboard.Int64Values[0], Is.EqualTo(10));
        }
    }

    public sealed class ObservationP3SettingsAndDiagnosticsTests
    {
        [TearDown]
        public void TearDown() => DebugRegistry.ClearForTests();

        [Test]
        public void Settings_ClampUpperBoundsAndRejectInvalidValues()
        {
            var controller = new ObservationController(
                historyLimit: ObservationSettings.MaxTimelineCapacity + 500,
                sampleIntervalSeconds: ObservationSettings.MaxSampleIntervalSeconds + 1d);

            Assert.That(controller.TimelineCapacity, Is.EqualTo(ObservationSettings.MaxTimelineCapacity));
            Assert.That(controller.SampleIntervalSeconds, Is.EqualTo(ObservationSettings.MaxSampleIntervalSeconds));
            Assert.Throws<ArgumentOutOfRangeException>(() => controller.SampleIntervalSeconds = 0d);
            Assert.Throws<ArgumentOutOfRangeException>(() => controller.TimelineCapacity = 0);
        }

        [Test]
        public void Controller_UpdatedSamplingIntervalControlsPollingCadence()
        {
            var view = new MutableDebugView("cadence-tree", "Cadence Tree");
            DebugRegistry.Register(view);
            view.SetSample(0, NodeState.Running, onStack: 1, score: 0);

            var controller = new ObservationController(sampleIntervalSeconds: 1d);
            controller.Poll(0d);
            Assert.That(controller.Timeline.Count, Is.EqualTo(1));

            controller.SampleIntervalSeconds = 0.1d;
            controller.Poll(0.05d);
            Assert.That(controller.Timeline.Count, Is.EqualTo(2));
            controller.Poll(0.10d);
            Assert.That(controller.Timeline.Count, Is.EqualTo(2));
            controller.Poll(0.15d);
            Assert.That(controller.Timeline.Count, Is.EqualTo(3));
        }

        [Test]
        public void Diagnostics_ReportObservationSettingsAndDisconnectedReplayRisk()
        {
            var view = new MutableDebugView("diag-tree", "Diag Tree");
            var handle = DebugRegistry.Register(view);
            view.SetSample(0, NodeState.Running, onStack: 1, score: 0);

            var controller = new ObservationController(
                historyLimit: ObservationSettings.MaxTimelineCapacity,
                sampleIntervalSeconds: 0.2d);
            controller.Poll(0d);
            DebugRegistry.Unregister(handle);
            controller.Poll(1d);

            var diagnostics = EditorDiagnostics.AnalyzeObservation(controller);
            Assert.That(diagnostics.Items.Any(item => item.Code == EditorDiagnostics.ObservationInfoCode), Is.True);
            Assert.That(diagnostics.Items.Any(item =>
                item.Severity == EditorDiagnosticSeverity.Warning
                && item.Path == "observation/connection"), Is.True);
            Assert.That(diagnostics.Items.Any(item =>
                item.Severity == EditorDiagnosticSeverity.Warning
                && item.Path == "observation/settings/timelineCapacity"), Is.True);
        }
    }

    public sealed class ObservationP3PerformanceBudgetTests
    {
        [TestCase(100, 400, 16, 2500)]
        [TestCase(500, 1200, 16, 5000)]
        [TestCase(1000, 2400, 16, 9000)]
        public void CaptureDiffAndRecording_ScaleWithinBroadBudget(
            int nodeCount,
            int blackboardKeyCount,
            int sampleCount,
            int maxElapsedMilliseconds)
        {
            var view = new LargeDebugView(nodeCount, blackboardKeyCount);
            var timeline = new ObservationTimeline(sampleLimit: sampleCount);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (var frame = 0; frame < sampleCount; frame++)
            {
                view.Advance(frame);
                timeline.Append(ObservationSnapshot.Capture(100, frame, view));
            }

            var json = ObservationRecording.ToJson(timeline, prettyPrint: false);
            var replay = ObservationRecording.ReplayFromJson(json);

            stopwatch.Stop();
            TestContext.WriteLine(
                $"BT observation budget nodes={nodeCount} keys={blackboardKeyCount} samples={sampleCount} "
                + $"elapsedMs={stopwatch.ElapsedMilliseconds} jsonChars={json.Length}");

            Assert.That(timeline.Count, Is.EqualTo(sampleCount));
            Assert.That(replay.Count, Is.EqualTo(sampleCount));
            Assert.That(replay.Current.NodeCount, Is.EqualTo(nodeCount));
            Assert.That(replay.Current.Blackboard.Count, Is.EqualTo(blackboardKeyCount));
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(maxElapsedMilliseconds));
        }
    }

    internal sealed class MutableDebugView : TreeDebugView
    {
        private List<NodeDebugInfo> _nodes = new();
        private BlackboardValueSnapshot _blackboard = new();

        public string TreeId { get; }
        public string DisplayName { get; }
        public string OwnerLabel => "";
        public int NodeCount => _nodes.Count;
        public int LastFrame { get; private set; }
        public int CaptureStateCalls { get; private set; }
        public TreeDefinition TreeDefinition { get; }
        public IReadOnlyDictionary<string, string> NodeSourceTree { get; set; }
        public IReadOnlyDictionary<string, string> NodeSourceNode { get; set; }
        public IReadOnlyList<SubtreeInstance> SubtreeInstances => new List<SubtreeInstance>();

        public MutableDebugView(string treeId, string displayName)
        {
            TreeId = treeId;
            DisplayName = displayName;
            TreeDefinition = new TreeDefinition { TreeId = treeId, RootNodeId = "root" };
        }

        public List<NodeDebugInfo> GetNodeStates() => new(_nodes);

        public BlackboardValueSnapshot GetBlackboard() => _blackboard;

        public TreeRuntimeSnapshot CaptureState()
        {
            CaptureStateCalls++;
            var snapshot = new TreeRuntimeSnapshot
            {
                SnapshotVersion = 1,
                Enabled = true,
                TreeState = _nodes.Count > 0 ? _nodes[0].State : NodeState.Inactive,
                Blackboard = _blackboard,
            };
            foreach (var node in _nodes)
            {
                snapshot.Nodes.Add(new NodeRuntimeSnapshot
                {
                    NodeId = node.NodeId,
                    State = node.State,
                    RunningChildIndex = node.RunningChildIndex,
                });
            }
            return snapshot;
        }

        public void SetSample(int frame, NodeState state, int onStack, long score)
        {
            LastFrame = frame;
            _nodes = new List<NodeDebugInfo>
            {
                new("root", "Root", "Test", NodeKind.Action, state, 0, onStack, -1),
            };
            _blackboard = new BlackboardValueSnapshot
            {
                KeyNames = new List<string> { "score" },
                KeyTypes = new List<ValueType> { ValueType.Int64 },
                BoolValues = new List<bool> { false },
                Int64Values = new List<long> { score },
                Fixed64RawValues = new List<long> { 0 },
                StringValues = new List<string> { "" },
            };
        }

        public void SetNodes(int frame, params (string id, NodeState state, int onStack)[] nodes)
        {
            LastFrame = frame;
            _nodes = new List<NodeDebugInfo>();
            foreach (var node in nodes)
            {
                _nodes.Add(new NodeDebugInfo(node.id, node.id, "Test", NodeKind.Action, node.state, 0, node.onStack, -1));
            }
            _blackboard = new BlackboardValueSnapshot();
        }

        public void SetNodes(int frame, params (string id, NodeState state, int onStack, int runningChildIndex)[] nodes)
        {
            LastFrame = frame;
            _nodes = new List<NodeDebugInfo>();
            foreach (var node in nodes)
            {
                _nodes.Add(new NodeDebugInfo(
                    node.id,
                    node.id,
                    "Test",
                    NodeKind.Action,
                    node.state,
                    0,
                    node.onStack,
                    node.runningChildIndex));
            }
            _blackboard = new BlackboardValueSnapshot();
        }

        public void SetBlackboardValues(params (string key, long value)[] values)
        {
            _blackboard = new BlackboardValueSnapshot
            {
                KeyNames = new List<string>(),
                KeyTypes = new List<ValueType>(),
                BoolValues = new List<bool>(),
                Int64Values = new List<long>(),
                Fixed64RawValues = new List<long>(),
                StringValues = new List<string>(),
            };
            foreach (var value in values)
            {
                _blackboard.KeyNames.Add(value.key);
                _blackboard.KeyTypes.Add(ValueType.Int64);
                _blackboard.BoolValues.Add(false);
                _blackboard.Int64Values.Add(value.value);
                _blackboard.Fixed64RawValues.Add(0L);
                _blackboard.StringValues.Add("");
            }
        }
    }

    internal sealed class LargeDebugView : TreeDebugView
    {
        private readonly List<NodeDebugInfo> _nodes;
        private readonly BlackboardValueSnapshot _blackboard;

        public string TreeId => "large-tree";
        public string DisplayName => "Large Tree";
        public string OwnerLabel => "budget";
        public int NodeCount => _nodes.Count;
        public int LastFrame { get; private set; }
        public TreeDefinition TreeDefinition { get; }
        public IReadOnlyDictionary<string, string> NodeSourceTree => null;
        public IReadOnlyDictionary<string, string> NodeSourceNode => null;
        public IReadOnlyList<SubtreeInstance> SubtreeInstances => Array.Empty<SubtreeInstance>();

        public LargeDebugView(int nodeCount, int blackboardKeyCount)
        {
            _nodes = new List<NodeDebugInfo>(nodeCount);
            _blackboard = new BlackboardValueSnapshot
            {
                KeyNames = new List<string>(blackboardKeyCount),
                KeyTypes = new List<ValueType>(blackboardKeyCount),
                BoolValues = new List<bool>(blackboardKeyCount),
                Int64Values = new List<long>(blackboardKeyCount),
                Fixed64RawValues = new List<long>(blackboardKeyCount),
                StringValues = new List<string>(blackboardKeyCount),
            };
            TreeDefinition = new TreeDefinition { TreeId = TreeId, RootNodeId = "n0" };

            for (var i = 0; i < nodeCount; i++)
            {
                var id = "n" + i;
                TreeDefinition.Nodes.Add(new NodeDefinition { Id = id, Type = BuiltInNodeTypes.Succeed });
                _nodes.Add(new NodeDebugInfo(id, id, "Test", NodeKind.Action, NodeState.Inactive, i, 0, -1));
            }

            for (var i = 0; i < blackboardKeyCount; i++)
            {
                _blackboard.KeyNames.Add("k" + i);
                _blackboard.KeyTypes.Add(ValueType.Int64);
                _blackboard.BoolValues.Add(false);
                _blackboard.Int64Values.Add(0L);
                _blackboard.Fixed64RawValues.Add(0L);
                _blackboard.StringValues.Add("");
            }
        }

        public List<NodeDebugInfo> GetNodeStates() => _nodes;

        public BlackboardValueSnapshot GetBlackboard() => _blackboard;

        public TreeRuntimeSnapshot CaptureState() => new();

        public void Advance(int frame)
        {
            LastFrame = frame;
            if (_nodes.Count > 0)
            {
                var running = frame % _nodes.Count;
                var previous = (frame + _nodes.Count - 1) % _nodes.Count;
                _nodes[previous] = NewNode(previous, NodeState.Success, 0);
                _nodes[running] = NewNode(running, NodeState.Running, 1);
            }

            if (_blackboard.Int64Values.Count > 0)
            {
                var keyIndex = frame % _blackboard.Int64Values.Count;
                _blackboard.Int64Values[keyIndex] = frame;
            }
        }

        private static NodeDebugInfo NewNode(int index, NodeState state, int onStack)
        {
            var id = "n" + index;
            return new NodeDebugInfo(id, id, "Test", NodeKind.Action, state, index, onStack, -1);
        }
    }

}
#endif
