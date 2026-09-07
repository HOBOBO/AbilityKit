using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using AbilityKit.HFSM;
using AbilityKit.HFSM.Graph;
using AbilityKit.HFSM.Graph.Compilation;
using AbilityKit.HFSM.Graph.Conditions;
using Object = UnityEngine.Object;

using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
namespace AbilityKit.Tests
{
    public sealed class GraphIntegrityTests
    {
        private enum TestStateId
        {
            Idle,
            Attack
        }

        private enum TestEventId
        {
            Go
        }

        private sealed class EvaluationContext : IEvaluationContext
        {
            public bool AllowTransition;
            public bool GetBool(string parameterName) => AllowTransition;
            public float GetFloat(string parameterName) => 0f;
            public int GetInt(string parameterName) => 0;
            public bool GetTrigger(string parameterName) => false;
            public bool HasAllActionsCompleted(string nodeId) => false;
            public float GetNodeElapsedTime(string nodeId) => 0f;
            public bool IsStateActive(string stateMachineId, string stateId) => false;
        }

        public sealed class ArbitraryConfigCondition : TransitionCondition
        {
            public string Alias;
            public int Count;
            public bool Enabled;
            public float Ratio;

            public override string TypeName => "Tests.ArbitraryConfig";
            public override string DisplayName => "Arbitrary Config";
            public override string GetDescription() => Alias;
            public override bool Evaluate(IEvaluationContext context) => Enabled;
            public override TransitionCondition Clone() => new ArbitraryConfigCondition
            {
                Alias = Alias,
                Count = Count,
                Enabled = Enabled,
                Ratio = Ratio
            };
            public override string[] GetRequiredParameters() => Array.Empty<string>();

            public override void SetFromConfig(Dictionary<string, object> config)
            {
                Alias = Convert.ToString(config["CustomAlias"]);
                Count = Convert.ToInt32(config["CustomCount"]);
                Enabled = Convert.ToBoolean(config["CustomEnabled"]);
                Ratio = Convert.ToSingle(config["CustomRatio"]);
            }

            public override Dictionary<string, object> ToConfig() => new Dictionary<string, object>
            {
                ["CustomAlias"] = Alias,
                ["CustomCount"] = Count,
                ["CustomEnabled"] = Enabled,
                ["CustomRatio"] = Ratio
            };
        }

        [Test]
        public void NodeHierarchyFieldsSurviveUnitySerialization()
        {
            var node = new StateNode("Idle")
            {
                ParentStateMachineId = "parent",
                isDefault = true
            };

            var restored = JsonUtility.FromJson<StateNode>(JsonUtility.ToJson(node));

            Assert.That(restored.ParentStateMachineId, Is.EqualTo("parent"));
            Assert.That(restored.isDefault, Is.True);
        }

        [Test]
        public void GraphCloneRemapsAllInternalReferencesAndEditorMetadata()
        {
            var graph = CreateNestedGraph(out var root, out var nested, out var idle, out var attack, out var edge);
            graph.EditorData.ToggleExpanded(nested.Id);
            graph.EditorData.GetOrCreateNodeEditorData(idle.Id).Position = new Vector2(12f, 34f);

            GraphAsset clone = null;
            try
            {
                clone = graph.Clone();
                var clonedRoot = clone.Nodes.OfType<StateMachineNode>().Single(node => node.DisplayName == root.DisplayName);
                var clonedNested = clone.Nodes.OfType<StateMachineNode>().Single(node => node.DisplayName == nested.DisplayName);
                var clonedIdle = clone.Nodes.OfType<StateNode>().Single(node => node.DisplayName == idle.DisplayName);
                var clonedAttack = clone.Nodes.OfType<StateNode>().Single(node => node.DisplayName == attack.DisplayName);
                var clonedEdge = clone.Edges.Single();

                Assert.That(clone.RootStateMachineId, Is.EqualTo(clonedRoot.Id));
                Assert.That(clonedRoot.ChildNodeIds, Is.EqualTo(new[] { clonedNested.Id }));
                Assert.That(clonedRoot.DefaultStateId, Is.EqualTo(clonedNested.Id));
                Assert.That(clonedNested.ParentStateMachineId, Is.EqualTo(clonedRoot.Id));
                Assert.That(clonedNested.ChildNodeIds, Is.EqualTo(new[] { clonedIdle.Id, clonedAttack.Id }));
                Assert.That(clonedNested.DefaultStateId, Is.EqualTo(clonedIdle.Id));
                Assert.That(clonedNested.TransitionIds, Is.EqualTo(new[] { clonedEdge.Id }));
                Assert.That(clonedIdle.ParentStateMachineId, Is.EqualTo(clonedNested.Id));
                Assert.That(clonedAttack.ParentStateMachineId, Is.EqualTo(clonedNested.Id));
                Assert.That(clonedEdge.SourceNodeId, Is.EqualTo(clonedIdle.Id));
                Assert.That(clonedEdge.TargetNodeId, Is.EqualTo(clonedAttack.Id));
                Assert.That(clone.EditorData.IsExpanded(clonedNested.Id), Is.True);
                Assert.That(clone.EditorData.GetNodeEditorData(clonedIdle.Id), Is.Not.Null);
                Assert.That(clone.Validate(), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(graph);
                if (clone != null)
                    Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void ReplacingConditionJsonInvalidatesPreviouslyLoadedConditions()
        {
            var edge = new TransitionEdge();
            edge.AddCondition(new ParameterCondition { ParameterName = "first" });
            _ = edge.Conditions;

            var replacement = new TransitionEdge();
            replacement.AddCondition(new ParameterCondition { ParameterName = "second" });
            edge.ConditionConfigJson = replacement.ConditionConfigJson;

            var condition = (ParameterCondition)edge.Conditions.Single();
            Assert.That(condition.ParameterName, Is.EqualTo("second"));
        }

        [Test]
        public void AddingConditionPreservesSerializedConditionsBeforeCacheIsLoaded()
        {
            var serialized = new TransitionEdge();
            serialized.AddCondition(new ParameterCondition { ParameterName = "existing" });

            var restored = new TransitionEdge
            {
                ConditionConfigJson = serialized.ConditionConfigJson
            };
            restored.AddCondition(new ParameterCondition { ParameterName = "added" });

            Assert.That(
                restored.Conditions.Cast<ParameterCondition>().Select(condition => condition.ParameterName),
                Is.EqualTo(new[] { "existing", "added" }));
        }

        [Test]
        public void ConditionProtocolRoundTripsBuiltInAndExtensionFields()
        {
            ConditionRegistry.Register<ArbitraryConfigCondition>();
            var edge = new TransitionEdge();
            edge.AddCondition(new TimeElapsedCondition
            {
                SourceNodeId = "source-node",
                Duration = 2.5f
            });
            edge.AddCondition(new ArbitraryConfigCondition
            {
                Alias = "extension",
                Count = 7,
                Enabled = true,
                Ratio = 0.75f
            });

            var restored = new TransitionEdge { ConditionConfigJson = edge.ConditionConfigJson };

            var elapsed = (TimeElapsedCondition)restored.Conditions[0];
            var extension = (ArbitraryConfigCondition)restored.Conditions[1];
            Assert.That(elapsed.SourceNodeId, Is.EqualTo("source-node"));
            Assert.That(elapsed.Duration, Is.EqualTo(2.5f));
            Assert.That(extension.Alias, Is.EqualTo("extension"));
            Assert.That(extension.Count, Is.EqualTo(7));
            Assert.That(extension.Enabled, Is.True);
            Assert.That(extension.Ratio, Is.EqualTo(0.75f));
        }

        [Test]
        public void InitializeFromGraphBuildsChildrenInsideTheirOwningMachine()
        {
            var graph = CreateNestedGraph(out _, out _, out _, out _, out _);
            try
            {
                var fsm = new ActionStateMachine();
                fsm.InitializeFromGraph(graph);

                Assert.That(fsm.GetAllStateNames(), Is.EqualTo(new[] { "Combat" }));
                var nested = (HybridStateMachine<string, string>)fsm.GetState("Combat");
                Assert.That(nested.GetAllStateNames(), Is.EqualTo(new[] { "Idle", "Attack" }));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void CompilerProducesStablePriorityOrderAndStructuredErrors()
        {
            var graph = CreateFlatGraph(out var root, out var idle, out var attack);
            try
            {
                var low = graph.CreateTransition(idle.Id, attack.Id);
                low.Priority = 1;
                root.AddTransition(low.Id);
                var high = graph.CreateTransition(idle.Id, attack.Id);
                high.Priority = 10;
                root.AddTransition(high.Id);
                var behavior = new BehaviorItem("Wait");
                idle.AddBehaviorItem(behavior);

                var program = new StateMachineGraphCompiler().Compile(graph);

                Assert.That(program.RootMachine.Transitions.Select(item => item.Priority), Is.EqualTo(new[] { 10, 1 }));
                Assert.That(((StateProgram)program.GetNode(idle.Id)).BehaviorIds, Is.EqualTo(new[] { behavior.id }));

                var orphan = graph.CreateTransition(idle.Id, attack.Id);
                var exception = Assert.Throws<GraphCompilationException>(() => new StateMachineGraphCompiler().Compile(graph));
                Assert.That(exception.Diagnostics.Any(item => item.Code == "EDGE_ORPHANED" && item.ElementId == orphan.Id), Is.True);

                graph.RemoveEdge(orphan);
                high.ConditionConfigJson = "{\"Version\":2,\"Conditions\":[{\"TypeName\":\"Missing.Condition\",\"Config\":{\"Entries\":[]}}]}";
                exception = Assert.Throws<GraphCompilationException>(() => new StateMachineGraphCompiler().Compile(graph));
                Assert.That(exception.Diagnostics.Any(item => item.Code == "CONDITION_CONFIG_INVALID" && item.ElementId == high.Id), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void CompiledConditionsGateNormalTransitions()
        {
            var graph = CreateFlatGraph(out var root, out var idle, out var attack);
            try
            {
                var edge = graph.CreateTransition(idle.Id, attack.Id);
                edge.AddCondition(new ParameterCondition
                {
                    ParameterName = "allow",
                    ParameterType = ParameterValueType.Bool,
                    BoolValue = true
                });
                root.AddTransition(edge.Id);
                var context = new EvaluationContext();
                var binding = StateMachineGraphBinding<string, string>.CreateNameBinding(context);
                var fsm = new ActionStateMachine { RegisterForInspection = false };
                fsm.InitializeFromGraph(graph, binding);
                fsm.Init();

                fsm.OnLogic();
                Assert.That(fsm.ActiveStateName, Is.EqualTo("Idle"));

                context.AllowTransition = true;
                fsm.OnLogic();
                Assert.That(fsm.ActiveStateName, Is.EqualTo("Attack"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void CompiledTriggerAndAnyStateTransitionsExecute()
        {
            var graph = CreateFlatGraph(out var root, out var idle, out var attack);
            try
            {
                var triggerEdge = graph.CreateTransition(idle.Id, attack.Id);
                triggerEdge.NextTriggerId = "go";
                root.AddTransition(triggerEdge.Id);
                var anyEdge = graph.CreateTransition(SpecialNodeIds.AnyState, idle.Id);
                anyEdge.NextTriggerId = "reset";
                root.AddAnyStateTransition(anyEdge.Id);

                var fsm = new ActionStateMachine { RegisterForInspection = false };
                fsm.InitializeFromGraph(graph);
                fsm.Init();

                fsm.Trigger("go");
                Assert.That(fsm.ActiveStateName, Is.EqualTo("Attack"));
                fsm.Trigger("reset");
                Assert.That(fsm.ActiveStateName, Is.EqualTo("Idle"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void ExplicitBindingSupportsNonStringStateAndEventIds()
        {
            var graph = CreateFlatGraph(out var root, out var idle, out var attack);
            try
            {
                var edge = graph.CreateTransition(idle.Id, attack.Id);
                edge.NextTriggerId = "Go";
                root.AddTransition(edge.Id);
                var binding = new StateMachineGraphBinding<TestStateId, TestEventId>(
                    node => (TestStateId)Enum.Parse(typeof(TestStateId), node.RuntimeName),
                    trigger => (TestEventId)Enum.Parse(typeof(TestEventId), trigger));
                var fsm = new ActionStateMachine<TestStateId, TestEventId> { RegisterForInspection = false };
                fsm.InitializeFromGraph(graph, binding);
                fsm.Init();

                fsm.Trigger(TestEventId.Go);

                Assert.That(fsm.ActiveStateName, Is.EqualTo(TestStateId.Attack));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void ParameterStoreLoadsDefaultsAndConsumesTriggers()
        {
            var store = new ParameterStore();
            store.LoadDefaults(new[]
            {
                new ParameterProgram("enabled", ParameterValueType.Bool, true),
                new ParameterProgram("speed", ParameterValueType.Float, 1.5f),
                new ParameterProgram("count", ParameterValueType.Int, 3),
                new ParameterProgram("pulse", ParameterValueType.Trigger, false)
            });

            Assert.That(store.GetBool("enabled"), Is.True);
            Assert.That(store.GetFloat("speed"), Is.EqualTo(1.5f));
            Assert.That(store.GetInt("count"), Is.EqualTo(3));
            store.SetTrigger("pulse");
            Assert.That(store.GetTrigger("pulse"), Is.True);
            Assert.That(store.GetTrigger("pulse"), Is.False);
        }

        private static GraphAsset CreateFlatGraph(
            out StateMachineNode root,
            out StateNode idle,
            out StateNode attack)
        {
            var graph = ScriptableObject.CreateInstance<GraphAsset>();
            root = graph.CreateStateMachine("Root", Vector2.zero);
            idle = graph.CreateState("Idle", Vector2.zero);
            attack = graph.CreateState("Attack", Vector2.one);
            idle.ParentStateMachineId = root.Id;
            attack.ParentStateMachineId = root.Id;
            root.AddChildNode(idle.Id);
            root.AddChildNode(attack.Id);
            root.DefaultStateId = idle.Id;
            return graph;
        }

        private static GraphAsset CreateNestedGraph(
            out StateMachineNode root,
            out StateMachineNode nested,
            out StateNode idle,
            out StateNode attack,
            out TransitionEdge edge)
        {
            var graph = ScriptableObject.CreateInstance<GraphAsset>();
            root = graph.CreateStateMachine("Root", Vector2.zero);
            nested = graph.CreateStateMachine("Combat", Vector2.one);
            nested.ParentStateMachineId = root.Id;
            root.AddChildNode(nested.Id);
            root.DefaultStateId = nested.Id;

            idle = graph.CreateState("Idle", Vector2.zero);
            idle.ParentStateMachineId = nested.Id;
            idle.isDefault = true;
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
