using System.Linq;
using AbilityKit.HFSM;
using NUnit.Framework;
using UnityEngine;
using UnityHFSM.Editor;
using UnityHFSM.Editor.Diagnostics;
using UnityHFSM.Graph;

namespace AbilityKit.Tests
{
    public sealed class HfsmNextDiagnosticsTests
    {
        [TestCase("$.nodes['idle'].behaviorKey", HfsmDiagnosticTargetKind.Node, "idle")]
        [TestCase("$.edges['edge-1'].conditionKey", HfsmDiagnosticTargetKind.Transition, "edge-1")]
        [TestCase("$.machines['root'].states['attack'].behaviorKey", HfsmDiagnosticTargetKind.Node, "attack")]
        [TestCase("$.machines['root'].transitions['attack-edge'].actionKey", HfsmDiagnosticTargetKind.Transition, "attack-edge")]
        [TestCase("$.machines['root'].initialStateId", HfsmDiagnosticTargetKind.Node, "root")]
        public void ResolvesStructuredDiagnosticPaths(
            string path,
            HfsmDiagnosticTargetKind expectedKind,
            string expectedId)
        {
            var target = HfsmNextDiagnostics.ResolveTarget(path);

            Assert.That(target.Kind, Is.EqualTo(expectedKind));
            Assert.That(target.Id, Is.EqualTo(expectedId));
        }

        [Test]
        public void SnapshotSummarizesExportReadinessAndCatalogSource()
        {
            var graph = CreateNestedGraph(out _, out var idle, out _, out _);
            try
            {
                idle.NextBehaviorKey = "missing.state";
                var snapshot = HfsmNextDiagnostics.Analyze(graph);

                Assert.That(snapshot.IsExportReady, Is.False);
                Assert.That(snapshot.ErrorCount, Is.GreaterThan(0));
                Assert.That(snapshot.CatalogSource, Is.Not.Empty);
                Assert.That(snapshot.Issues.Any(issue => issue.Code == "HFSMNEXT001"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void ContextFocusNavigatesToNestedNodeAndOwnedTransition()
        {
            var graph = CreateNestedGraph(out var nested, out var idle, out _, out var edge);
            try
            {
                var context = new HfsmEditorContext { GraphAsset = graph };

                Assert.That(context.FocusNode(idle.Id), Is.True);
                Assert.That(context.CurrentStateMachine, Is.SameAs(nested));
                Assert.That(context.FirstSelectedNode, Is.SameAs(idle));

                Assert.That(context.FocusTransition(edge.Id), Is.True);
                Assert.That(context.CurrentStateMachine, Is.SameAs(nested));
                Assert.That(context.SelectedEdge, Is.SameAs(edge));

                var anyEdge = graph.CreateTransition(HfsmSpecialNodeIds.AnyState, idle.Id);
                nested.AddAnyStateTransition(anyEdge.Id);
                Assert.That(context.FocusTransition(anyEdge.Id), Is.True);
                Assert.That(context.CurrentStateMachine, Is.SameAs(nested));
                Assert.That(context.SelectedEdge, Is.SameAs(anyEdge));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        private static HfsmGraphAsset CreateNestedGraph(
            out HfsmStateMachineNode nested,
            out HfsmStateNode idle,
            out HfsmStateNode attack,
            out HfsmTransitionEdge edge)
        {
            var graph = ScriptableObject.CreateInstance<HfsmGraphAsset>();
            graph.GraphName = "diagnostics";
            var root = graph.CreateStateMachine("Root", Vector2.zero);
            nested = graph.CreateStateMachine("Combat", Vector2.one);
            nested.ParentStateMachineId = root.Id;
            root.AddChildNode(nested.Id);
            root.DefaultStateId = nested.Id;

            idle = graph.CreateState("Idle", Vector2.zero);
            idle.ParentStateMachineId = nested.Id;
            attack = graph.CreateState("Attack", Vector2.one);
            attack.ParentStateMachineId = nested.Id;
            nested.AddChildNode(idle.Id);
            nested.AddChildNode(attack.Id);
            nested.DefaultStateId = idle.Id;
            edge = graph.CreateTransition(idle.Id, attack.Id);
            nested.AddTransition(edge.Id);
            return graph;
        }
    }
}
