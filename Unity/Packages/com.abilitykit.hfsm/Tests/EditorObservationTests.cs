using AbilityKit.HFSM.Editor;
using AbilityKit.HFSM.Graph;
using AbilityKit.HFSM.Visualization;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AbilityKit.Tests
{
    /// <summary>
    /// Covers the editor-canvas runtime observation binding. Live state paths are
    /// "{machineId}/{stateId}", so the context must prefix node ids with whichever machine the
    /// canvas currently shows — and must unbind when the graph is swapped.
    /// </summary>
    public sealed class EditorObservationTests
    {
        [Test]
        public void LiveSnapshotMarksActiveEnteringAndExitingNodes()
        {
            var graph = CreateGraph(out var root, out var idle, out var attack);
            try
            {
                var context = new EditorContext { GraphAsset = graph };
                context.BeginObserve("instance");
                Assert.That(context.CurrentMachineId, Is.EqualTo(root.Id));

                var snapshot = new FsmSnapshot();
                snapshot.activeStatePaths.Add(root.Id + "/" + idle.Id);
                snapshot.pendingStatePaths.Add(root.Id + "/" + attack.Id);
                snapshot.exitingStatePaths.Add(root.Id + "/" + idle.Id);
                context.SetLiveSnapshot(snapshot);

                Assert.That(context.IsNodeActive(idle.Id), Is.True);
                Assert.That(context.IsNodeActive(attack.Id), Is.False);
                Assert.That(context.IsNodeEntering(attack.Id), Is.True);
                Assert.That(context.IsNodeEntering(idle.Id), Is.False);
                Assert.That(context.IsNodeExiting(idle.Id), Is.True);
                Assert.That(context.IsNodeExiting(attack.Id), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void ObservationFollowsNestedMachinePrefixAndUnbindsOnReset()
        {
            var graph = CreateGraph(out var root, out _, out _);
            try
            {
                var nested = graph.CreateStateMachine("Combat", Vector2.one);
                nested.ParentStateMachineId = root.Id;
                root.AddChildNode(nested.Id);
                var nestedIdle = graph.CreateState("CombatIdle", Vector2.zero);
                nestedIdle.ParentStateMachineId = nested.Id;
                nested.AddChildNode(nestedIdle.Id);

                var context = new EditorContext { GraphAsset = graph };
                context.BeginObserve("bot");
                Assert.That(context.IsObserving, Is.True);
                Assert.That(context.ObservedInstanceName, Is.EqualTo("bot"));

                var snapshot = new FsmSnapshot();
                snapshot.activeStatePaths.Add(nested.Id + "/" + nestedIdle.Id);
                context.SetLiveSnapshot(snapshot);

                // At the root view the nested path must not match...
                Assert.That(context.IsNodeActive(nestedIdle.Id), Is.False);

                // ...and it matches once the canvas shows the nested machine.
                context.NavigateInto(nested);
                Assert.That(context.CurrentMachineId, Is.EqualTo(nested.Id));
                Assert.That(context.IsNodeActive(nestedIdle.Id), Is.True);

                // Swapping the graph unbinds observation entirely.
                context.Reset();
                Assert.That(context.IsObserving, Is.False);
                Assert.That(context.LiveSnapshot, Is.Null);
                Assert.That(context.IsNodeActive(nestedIdle.Id), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void UnobservedContextNeverMatches()
        {
            var graph = CreateGraph(out var root, out var idle, out _);
            try
            {
                var context = new EditorContext { GraphAsset = graph };
                var snapshot = new FsmSnapshot();
                snapshot.activeStatePaths.Add(root.Id + "/" + idle.Id);
                context.SetLiveSnapshot(snapshot);

                Assert.That(context.IsObserving, Is.False);
                Assert.That(context.IsNodeActive(idle.Id), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        private static GraphAsset CreateGraph(
            out StateMachineNode root,
            out StateNode idle,
            out StateNode attack)
        {
            var graph = ScriptableObject.CreateInstance<GraphAsset>();
            graph.GraphName = "observation";
            root = graph.CreateStateMachine("Root", Vector2.zero);
            graph.SetRootStateMachine(root);

            idle = graph.CreateState("Idle", Vector2.zero);
            idle.ParentStateMachineId = root.Id;
            root.AddChildNode(idle.Id);

            attack = graph.CreateState("Attack", Vector2.one);
            attack.ParentStateMachineId = root.Id;
            root.AddChildNode(attack.Id);
            return graph;
        }
    }
}
