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
                attack.NextParallelBehaviorKeysInternal.Add("combat.move");
                attack.NextParallelBehaviorKeysInternal.Add("combat.aim");
                attack.NextParallelExitPolicy = ParallelExitPolicy.All;

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
                var importedAttack = machine.States.Single(state => state.Id == attack.Id);
                Assert.That(importedAttack.ParallelBehaviorKeys,
                    Is.EqualTo(new[] { "combat.move", "combat.aim" }));
                Assert.That(importedAttack.ParallelExitPolicy, Is.EqualTo(ParallelExitPolicy.All));
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
        public void ImportsGhostNestedMachineAndVerticalExitSemantics()
        {
            var graph = ScriptableObject.CreateInstance<GraphAsset>();
            try
            {
                var root = graph.CreateStateMachine("Root", Vector2.zero);
                var nested = graph.CreateStateMachine("Nested", Vector2.zero);
                var done = graph.CreateState("Done", Vector2.one);
                var source = graph.CreateState("Source", Vector2.zero);
                root.AddChildNode(nested.Id);
                root.AddChildNode(done.Id);
                root.DefaultStateId = nested.Id;
                nested.ParentStateMachineId = root.Id;
                nested.NeedsExitTime = true;
                nested.IsGhostState = true;
                nested.AddChildNode(source.Id);
                nested.DefaultStateId = source.Id;
                source.ParentStateMachineId = nested.Id;
                source.IsGhostState = true;

                var edge = graph.CreateTransition(source.Id, string.Empty);
                edge.IsExitTransition = true;
                nested.AddTransition(edge.Id);

                var result = LegacyGraphImporter.Import(graph);

                Assert.That(result.IsSuccess, Is.True, string.Join("\n", result.Issues));
                var rootDefinition = result.Definition.Machines.Single(machine => machine.Id == root.Id);
                var nestedState = rootDefinition.States.Single(state => state.Id == nested.Id);
                Assert.That(nestedState.RequiresExitApproval, Is.True);
                Assert.That(nestedState.IsGhostState, Is.True);
                var nestedDefinition = result.Definition.Machines.Single(machine => machine.Id == nested.Id);
                Assert.That(nestedDefinition.States.Single().IsGhostState, Is.True);
                Assert.That(nestedDefinition.Transitions.Single().ExitMachine, Is.True);
                Assert.That(nestedDefinition.Transitions.Single().ToStateId, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }
    }
}
