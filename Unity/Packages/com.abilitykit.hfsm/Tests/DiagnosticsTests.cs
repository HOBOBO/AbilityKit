using System.Linq;
using AbilityKit.HFSM;
using NUnit.Framework;
using UnityEngine;
using AbilityKit.HFSM.Editor;
using AbilityKit.HFSM.Editor.Diagnostics;
using AbilityKit.HFSM.Graph;

using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
namespace AbilityKit.Tests
{
    public sealed class DiagnosticsTests
    {
        [TestCase("$.nodes['idle'].behaviorKey", DiagnosticTargetKind.Node, "idle")]
        [TestCase("$.edges['edge-1'].conditionKey", DiagnosticTargetKind.Transition, "edge-1")]
        [TestCase("$.machines['root'].states['attack'].behaviorKey", DiagnosticTargetKind.Node, "attack")]
        [TestCase("$.machines['root'].transitions['attack-edge'].actionKey", DiagnosticTargetKind.Transition, "attack-edge")]
        [TestCase("$.machines['root'].initialStateId", DiagnosticTargetKind.Node, "root")]
        public void ResolvesStructuredDiagnosticPaths(
            string path,
            DiagnosticTargetKind expectedKind,
            string expectedId)
        {
            var target = Diagnostics.ResolveTarget(path);

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
                var snapshot = Diagnostics.Analyze(graph);

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
        public void PlatformDiagnosticsPreserveMetadataAndLocateAction()
        {
            var graph = CreateNestedGraph(out _, out var idle, out _, out _);
            try
            {
                idle.NextBehaviorKey = "missing.state";
                var snapshot = Diagnostics.Analyze(graph);
                DiagnosticTarget located = default;

                var diagnostics = snapshot.ToPlatformDiagnostics(target => located = target);
                var diagnostic = diagnostics.Items.Single(item => item.Code == "HFSMNEXT001");

                Assert.That(diagnostic.Severity,
                    Is.EqualTo(AbilityKit.Editor.Platform.Diagnostics.EditorDiagnosticSeverity.Error));
                Assert.That(diagnostic.Path, Does.Contain(idle.Id));
                Assert.That(diagnostic.Message, Is.Not.Empty);
                Assert.That(diagnostic.CanLocate, Is.True);

                diagnostic.Locate.Invoke();
                Assert.That(located.Kind, Is.EqualTo(DiagnosticTargetKind.Node));
                Assert.That(located.Id, Is.EqualTo(idle.Id));
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
                var context = new EditorContext { GraphAsset = graph };

                Assert.That(context.FocusNode(idle.Id), Is.True);
                Assert.That(context.CurrentStateMachine, Is.SameAs(nested));
                Assert.That(context.FirstSelectedNode, Is.SameAs(idle));

                Assert.That(context.FocusTransition(edge.Id), Is.True);
                Assert.That(context.CurrentStateMachine, Is.SameAs(nested));
                Assert.That(context.SelectedEdge, Is.SameAs(edge));

                var anyEdge = graph.CreateTransition(SpecialNodeIds.AnyState, idle.Id);
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

        private static GraphAsset CreateNestedGraph(
            out StateMachineNode nested,
            out StateNode idle,
            out StateNode attack,
            out TransitionEdge edge)
        {
            var graph = ScriptableObject.CreateInstance<GraphAsset>();
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
