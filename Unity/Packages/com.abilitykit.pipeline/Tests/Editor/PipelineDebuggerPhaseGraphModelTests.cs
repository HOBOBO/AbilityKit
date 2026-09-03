#if UNITY_EDITOR

#nullable enable

using System;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Pipeline.Editor.Tests
{
    public sealed class PipelineDebuggerPhaseGraphModelTests
    {
        [Test]
        public void Rebuild_EmptyGraph_HasNoBounds()
        {
            var model = new PipelineDebuggerPhaseGraphModel();

            model.Rebuild(PipelineDebugGraphSnapshot.Empty, null, Array.Empty<PipelinePhaseDebugState>());

            Assert.That(model.NodeOrder, Is.Empty);
            Assert.That(model.TryGetBounds(out _), Is.False);
        }

        [Test]
        public void Rebuild_AutoLayout_IndexesHierarchyAndSeparatesNodes()
        {
            PipelinePhaseDebugNode left = Node("left");
            PipelinePhaseDebugNode right = Node("right");
            PipelinePhaseDebugNode root = Node("root", left, right);
            var model = Build(Graph(root));

            Assert.That(model.NodeOrder.Count, Is.EqualTo(3));
            Assert.That(model.NodeRects["root"].width, Is.EqualTo(PipelineDebuggerPhaseGraphModel.NodeWidth));
            Assert.That(model.NodeRects["left"].y, Is.GreaterThan(model.NodeRects["root"].y));
            Assert.That(model.NodeRects["right"].x, Is.GreaterThan(model.NodeRects["left"].x));
            Assert.That(model.TryGetBounds(out Rect bounds), Is.True);
            Assert.That(bounds.Contains(model.NodeRects["root"].center), Is.True);
        }

        [Test]
        public void Rebuild_CustomLayout_OverridesKnownNodeOnly()
        {
            PipelinePhaseDebugNode root = Node("root");
            var layout = new PipelineDebugGraphLayout(
                "graph",
                new[]
                {
                    new PipelineDebugNodeLayout("root", 320f, 180f),
                    new PipelineDebugNodeLayout("missing", 1f, 2f)
                });
            var model = new PipelineDebuggerPhaseGraphModel();

            model.Rebuild(Graph(root), layout, Array.Empty<PipelinePhaseDebugState>());

            Assert.That(model.NodeRects["root"].position, Is.EqualTo(new Vector2(320f, 180f)));
            Assert.That(model.NodeRects.ContainsKey("missing"), Is.False);
        }

        [Test]
        public void Selection_UsesTransformedRectAndCanBeCleared()
        {
            var model = Build(Graph(Node("root")));
            Vector2 point = model.Transform(model.NodeRects["root"]).center;

            Assert.That(model.TrySelect(point, out PipelinePhaseDebugNode? selected), Is.True);
            Assert.That(selected?.NodeKey, Is.EqualTo("root"));
            Assert.That(model.SelectedNodeKey, Is.EqualTo("root"));

            model.ClearSelection();
            Assert.That(model.SelectedNodeKey, Is.Null);
            Assert.That(model.TrySelect(new Vector2(-100f, -100f), out _), Is.False);
        }

        [Test]
        public void Rebuild_RemovesSelectionWhenNodeDisappears()
        {
            var model = Build(Graph(Node("root")));
            model.TrySelect(model.Transform(model.NodeRects["root"]).center, out _);

            model.Rebuild(PipelineDebugGraphSnapshot.Empty, null, Array.Empty<PipelinePhaseDebugState>());

            Assert.That(model.SelectedNodeKey, Is.Null);
        }

        [Test]
        public void ZoomAt_PreservesLogicalPointAndClampsZoom()
        {
            var model = Build(Graph(Node("root")));
            Vector2 pointer = new Vector2(240f, 160f);
            Vector2 logicalBefore = (pointer - model.Pan) / model.Zoom;

            model.ZoomAt(pointer, 1.5f);

            Vector2 logicalAfter = (pointer - model.Pan) / model.Zoom;
            Assert.That(logicalAfter.x, Is.EqualTo(logicalBefore.x).Within(0.001f));
            Assert.That(logicalAfter.y, Is.EqualTo(logicalBefore.y).Within(0.001f));
            model.ZoomAt(pointer, 100f);
            Assert.That(model.Zoom, Is.EqualTo(PipelineDebuggerPhaseGraphModel.MaxZoom));
        }

        [Test]
        public void PanFitAndFocus_UpdateViewportTransform()
        {
            var model = Build(Graph(Node("root")));
            Vector2 initialPan = model.Pan;

            model.PanBy(new Vector2(10f, -4f));
            Assert.That(model.Pan, Is.EqualTo(initialPan + new Vector2(10f, -4f)));
            Assert.That(model.Fit(new Vector2(800f, 600f)), Is.True);
            Assert.That(model.Focus("root", new Vector2(800f, 600f)), Is.True);
            Assert.That(model.SelectedNodeKey, Is.EqualTo("root"));
            Assert.That(model.Transform(model.NodeRects["root"]).center, Is.EqualTo(new Vector2(400f, 300f)));
            Assert.That(model.Focus("missing", new Vector2(800f, 600f)), Is.False);
        }

        [Test]
        public void TryFindFocusNode_PrefersFailedBeforeActiveInGraphOrder()
        {
            PipelinePhaseDebugNode root = Node("root", Node("active"), Node("failed"));
            var states = new[]
            {
                new PipelinePhaseDebugState("active", EPipelineDebugExecutionState.Active),
                new PipelinePhaseDebugState("failed", EPipelineDebugExecutionState.Failed)
            };
            var model = Build(Graph(root), states);

            Assert.That(model.TryFindFocusNode(out string? nodeKey), Is.True);
            Assert.That(nodeKey, Is.EqualTo("failed"));
        }

        [Test]
        public void ResolveState_UsesExplicitActiveAndTraceFallbacks()
        {
            PipelinePhaseDebugNode explicitNode = Node("explicit", "Explicit");
            PipelinePhaseDebugNode activeNode = Node("active", "Active");
            PipelinePhaseDebugNode traceNode = Node("trace", "Trace");
            var model = Build(
                Graph(Node("root", explicitNode, activeNode, traceNode)),
                new[] { new PipelinePhaseDebugState("explicit", EPipelineDebugExecutionState.Skipped) });
            var active = new[] { new AbilityPipelinePhaseId("Active") };
            var completedTrace = new[] { Trace(EPipelineTraceEventType.PhaseComplete, "Trace") };
            var failedTrace = new[]
            {
                Trace(EPipelineTraceEventType.PhaseComplete, "Trace"),
                Trace(EPipelineTraceEventType.PhaseError, "Trace")
            };

            Assert.That(model.ResolveState(explicitNode, active, failedTrace), Is.EqualTo(EPipelineDebugExecutionState.Skipped));
            Assert.That(model.ResolveState(activeNode, active, Array.Empty<PipelineTraceEvent>()), Is.EqualTo(EPipelineDebugExecutionState.Active));
            Assert.That(model.ResolveState(traceNode, Array.Empty<AbilityPipelinePhaseId>(), completedTrace), Is.EqualTo(EPipelineDebugExecutionState.Completed));
            Assert.That(model.ResolveState(traceNode, Array.Empty<AbilityPipelinePhaseId>(), failedTrace), Is.EqualTo(EPipelineDebugExecutionState.Failed));
        }

        [Test]
        public void ResolveEdgeState_ProjectsConditionAndTargetState()
        {
            var states = new[]
            {
                new PipelinePhaseDebugState(
                    "source",
                    EPipelineDebugExecutionState.Completed,
                    0,
                    new[] { EPipelineDebugConditionResult.Matched, EPipelineDebugConditionResult.Rejected }),
                new PipelinePhaseDebugState("active", EPipelineDebugExecutionState.Active)
            };
            var model = Build(Graph(Node("source"), Node("active"), Node("normal")), states);

            Assert.That(model.ResolveEdgeState(Edge("source", "normal", 0)), Is.EqualTo(PipelineDebuggerPhaseEdgeState.Selected));
            Assert.That(model.ResolveEdgeState(Edge("source", "normal", 1)), Is.EqualTo(PipelineDebuggerPhaseEdgeState.Rejected));
            Assert.That(model.ResolveEdgeState(new PipelinePhaseDebugEdge("normal", "active", EPipelineDebugEdgeKind.Flow)), Is.EqualTo(PipelineDebuggerPhaseEdgeState.Active));
            Assert.That(model.ResolveEdgeState(new PipelinePhaseDebugEdge("normal", "source", EPipelineDebugEdgeKind.Flow)), Is.EqualTo(PipelineDebuggerPhaseEdgeState.Normal));
        }

        private static PipelineDebuggerPhaseGraphModel Build(
            PipelineDebugGraphSnapshot graph,
            PipelinePhaseDebugState[]? states = null)
        {
            var model = new PipelineDebuggerPhaseGraphModel();
            model.Rebuild(graph, null, states ?? Array.Empty<PipelinePhaseDebugState>());
            return model;
        }

        private static PipelineDebugGraphSnapshot Graph(params PipelinePhaseDebugNode[] roots)
        {
            return new PipelineDebugGraphSnapshot(roots, Array.Empty<PipelinePhaseDebugEdge>(), "graph");
        }

        private static PipelinePhaseDebugNode Node(string key, params PipelinePhaseDebugNode[] children)
        {
            return Node(key, key, children);
        }

        private static PipelinePhaseDebugNode Node(
            string key,
            string phaseId,
            params PipelinePhaseDebugNode[] children)
        {
            return new PipelinePhaseDebugNode(
                key,
                new AbilityPipelinePhaseId(phaseId),
                "TestPhase",
                children.Length == 0 ? EPipelineDebugNodeKind.Phase : EPipelineDebugNodeKind.Composite,
                string.Empty,
                children);
        }

        private static PipelinePhaseDebugEdge Edge(string source, string target, int childIndex)
        {
            return new PipelinePhaseDebugEdge(source, target, EPipelineDebugEdgeKind.Condition, childIndex: childIndex);
        }

        private static PipelineTraceEvent Trace(EPipelineTraceEventType type, string phaseId)
        {
            return new PipelineTraceEvent(
                1,
                type,
                new AbilityPipelinePhaseId(phaseId),
                type == EPipelineTraceEventType.PhaseError
                    ? EAbilityPipelineState.Failed
                    : EAbilityPipelineState.Completed,
                string.Empty,
                DateTime.UtcNow);
        }
    }
}

#endif
