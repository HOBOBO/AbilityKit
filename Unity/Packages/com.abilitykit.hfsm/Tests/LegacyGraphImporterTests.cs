using System.Linq;
using AbilityKit.HFSM.Migration;
using NUnit.Framework;
using UnityEngine;
using AbilityKit.HFSM.Graph;

using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
namespace AbilityKit.Tests
{
    public sealed class LegacyGraphImporterTests
    {
        [Test]
        public void ImportsProvablyEquivalentGraphSubset()
        {
            var graph = ScriptableObject.CreateInstance<GraphAsset>();
            try
            {
                graph.GraphName = "combat";
                var root = graph.CreateStateMachine("Root", Vector2.zero);
                var idle = graph.CreateState("Idle", Vector2.zero);
                var attack = graph.CreateState("Attack", Vector2.zero);
                root.AddChildNode(idle.Id);
                root.AddChildNode(attack.Id);
                root.DefaultStateId = idle.Id;
                root.RememberLastState = true;

                idle.AddLogicAction("LegacyTick");
                var direct = graph.CreateTransition(idle.Id, attack.Id);
                direct.Priority = 9;
                direct.ForceInstantly = true;
                direct.ConditionConfigJson = "{\"Conditions\":[{}]}";
                root.AddTransition(direct.Id);

                var fromAny = graph.CreateTransition(SpecialNodeIds.AnyState, idle.Id);
                root.AddAnyStateTransition(fromAny.Id);

                var bindings = new LegacyImportBindings()
                    .RegisterState(idle.Id, "combat.idle")
                    .RegisterCondition(direct.Id, "combat.canAttack");
                var result = LegacyGraphImporter.Import(graph, bindings);

                Assert.That(result.IsSuccess, Is.True, string.Join("\n", result.Issues));
                Assert.That(result.Definition, Is.Not.Null);
                var machine = result.Definition.Machines.Single();
                Assert.That(machine.Id, Is.EqualTo(root.Id));
                Assert.That(machine.InitialStateId, Is.EqualTo(idle.Id));
                Assert.That(machine.RememberLastState, Is.True);
                Assert.That(machine.States.Single(state => state.Id == idle.Id).BehaviorKey,
                    Is.EqualTo("combat.idle"));
                var importedDirect = machine.Transitions.Single(transition => transition.Id == direct.Id);
                Assert.That(importedDirect.ConditionKey, Is.EqualTo("combat.canAttack"));
                Assert.That(importedDirect.Priority, Is.EqualTo(9));
                Assert.That(importedDirect.ForceImmediate, Is.True);
                Assert.That(machine.Transitions.Single(transition => transition.Id == fromAny.Id).FromAnyState,
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void RejectsUnsupportedSemanticsWithoutMappings()
        {
            var graph = ScriptableObject.CreateInstance<GraphAsset>();
            try
            {
                var root = graph.CreateStateMachine("Root", Vector2.zero);
                var source = graph.CreateState("Source", Vector2.zero);
                var target = graph.CreateState("Target", Vector2.zero);
                root.AddChildNode(source.Id);
                root.AddChildNode(target.Id);
                root.DefaultStateId = source.Id;

                source.IsGhostState = true;
                source.NeedsExitTime = true;
                source.AddLogicAction("LegacyTick");
                var edge = graph.CreateTransition(source.Id, target.Id);
                edge.IsExitTransition = true;
                edge.ConditionConfigJson = "{\"Conditions\":[{}]}";
                root.AddTransition(edge.Id);

                var result = LegacyGraphImporter.Import(graph);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Definition, Is.Null);
                Assert.That(result.Issues.Any(issue => issue.Code == "HFSMLEG012"), Is.True);
                Assert.That(result.Issues.Any(issue => issue.Code == "HFSMLEG013"), Is.True);
                Assert.That(result.Issues.Any(issue => issue.Code == "HFSMLEG022"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }
    }
}
