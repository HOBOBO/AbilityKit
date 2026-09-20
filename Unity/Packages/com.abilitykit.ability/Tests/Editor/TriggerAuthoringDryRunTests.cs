#if UNITY_EDITOR
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using NUnit.Framework;

namespace AbilityKit.Ability.Editor.Tests
{
    public sealed class TriggerAuthoringDryRunTests
    {
        [Test]
        public void Run_ShortCircuitsFailedAllAndSkipsActions()
        {
            var second = Condition("always_true");
            var module = Module(
                new TriggerNodeData
                {
                    Kind = TriggerNodeKind.Condition,
                    Type = "all",
                    Children =
                    {
                        Compare("arg_gt", Payload("damage"), Number(10d)),
                        second
                    }
                },
                Sequence(Action("debug_log")));
            var input = Parse("{\"payload\":{\"damage\":5}}");
            TriggerAuthoringNodeIdentity.EnsureModule(module);

            var result = TriggerAuthoringDryRun.Run(module, module.Triggers[0], 0, null, input);

            Assert.That(result.EntryState, Is.EqualTo(TriggerAuthoringDryRunState.Failed));
            Assert.That(Find(result, second.NodeId).State, Is.EqualTo(TriggerAuthoringDryRunState.Skipped));
            Assert.That(Find(result, module.Triggers[0].Actions.NodeId).State,
                Is.EqualTo(TriggerAuthoringDryRunState.Skipped));
            Assert.That(result.Count(TriggerAuthoringDryRunState.WouldExecute), Is.EqualTo(0));
        }

        [Test]
        public void Run_PropagatesUnknownRuntimeConditionToPotentialActions()
        {
            var action = Action("give_damage");
            var module = Module(Condition("has_buff"), action);
            TriggerAuthoringNodeIdentity.EnsureModule(module);

            var result = TriggerAuthoringDryRun.Run(
                module,
                module.Triggers[0],
                0,
                null,
                new TriggerAuthoringDryRunInput());

            Assert.That(result.EntryState, Is.EqualTo(TriggerAuthoringDryRunState.Unknown));
            Assert.That(Find(result, action.NodeId).State, Is.EqualTo(TriggerAuthoringDryRunState.Potential));
        }

        [Test]
        public void Run_ConditionalActionSelectsElseBranchWithoutExecutingActions()
        {
            var thenAction = Action("heal");
            var elseAction = Action("debug_log");
            var conditional = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                Type = "conditional",
                Condition = Condition("always_false"),
                Children = { thenAction },
                ElseChildren = { elseAction }
            };
            var module = Module(null, Sequence(conditional));
            TriggerAuthoringNodeIdentity.EnsureModule(module);

            var result = TriggerAuthoringDryRun.Run(
                module,
                module.Triggers[0],
                0,
                null,
                new TriggerAuthoringDryRunInput());

            Assert.That(result.EntryState, Is.EqualTo(TriggerAuthoringDryRunState.Passed));
            Assert.That(Find(result, thenAction.NodeId).State, Is.EqualTo(TriggerAuthoringDryRunState.Skipped));
            Assert.That(Find(result, elseAction.NodeId).State,
                Is.EqualTo(TriggerAuthoringDryRunState.WouldExecute));
        }

        [Test]
        public void Run_UsesLocalDefaultAndTracesReusableGroupPath()
        {
            var groupRoot = Compare("num_var_gt", Local("trigger:stacks"), Number(1d));
            var reference = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Condition,
                GroupReference = "shared.condition"
            };
            var module = Module(reference, Action("debug_log"));
            module.ConditionGroups.Add(new TriggerNodeGroupData
            {
                Id = "shared.condition",
                Root = groupRoot
            });
            module.Triggers[0].Blackboard.Add(new TriggerBlackboardVariableData
            {
                Key = "stacks",
                Type = TriggerValueType.Number,
                DefaultValue = Number(2d)
            });
            TriggerAuthoringNodeIdentity.EnsureModule(module);

            var result = TriggerAuthoringDryRun.Run(
                module,
                module.Triggers[0],
                0,
                null,
                new TriggerAuthoringDryRunInput());

            Assert.That(result.EntryState, Is.EqualTo(TriggerAuthoringDryRunState.Passed));
            Assert.That(Find(result, reference.NodeId).Path, Is.EqualTo("module.triggers[0].condition"));
            Assert.That(Find(result, groupRoot.NodeId).Path, Is.EqualTo("module.triggers[0].condition.resolved"));
        }

        [Test]
        public void Run_AdvancedControlFlow_UsesConservativeChildStatesAndTracesUntilCondition()
        {
            var parallelChild = Action("debug_log");
            var selectorChild = Action("debug_log");
            var repeatChild = Action("debug_log");
            var untilChild = Action("debug_log");
            var untilCondition = Condition("always_false");
            var until = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                Type = "until",
                Condition = untilCondition,
                Children = { untilChild }
            };
            var module = Module(
                null,
                Sequence(
                    Flow("parallel", parallelChild),
                    Flow("selector", selectorChild),
                    Flow("repeat", repeatChild),
                    until));
            TriggerAuthoringNodeIdentity.EnsureModule(module);

            var result = TriggerAuthoringDryRun.Run(
                module,
                module.Triggers[0],
                0,
                null,
                new TriggerAuthoringDryRunInput());

            Assert.That(Find(result, parallelChild.NodeId).State,
                Is.EqualTo(TriggerAuthoringDryRunState.WouldExecute));
            Assert.That(Find(result, selectorChild.NodeId).State,
                Is.EqualTo(TriggerAuthoringDryRunState.Potential));
            Assert.That(Find(result, repeatChild.NodeId).State,
                Is.EqualTo(TriggerAuthoringDryRunState.Potential));
            Assert.That(Find(result, untilChild.NodeId).State,
                Is.EqualTo(TriggerAuthoringDryRunState.Potential));
            var conditionEntry = Find(result, untilCondition.NodeId);
            Assert.That(conditionEntry.State, Is.EqualTo(TriggerAuthoringDryRunState.Failed));
            Assert.That(conditionEntry.Path, Is.EqualTo("module.triggers[0].actions.children[3].condition"));
        }

        [Test]
        public void Input_RejectsNonObjectDomains()
        {
            var success = TriggerAuthoringDryRunInput.TryParse(
                "{\"payload\": 1}",
                out _,
                out var error);

            Assert.That(success, Is.False);
            Assert.That(error, Does.Contain("payload"));
        }

        private static TriggerAuthoringModuleData Module(TriggerNodeData condition, TriggerNodeData actions)
        {
            var module = new TriggerAuthoringModuleData { ModuleId = "dry-run.tests" };
            module.Triggers.Add(new TriggerDefinitionData
            {
                Id = 1,
                Name = "Dry Run",
                Event = "test.event",
                Condition = condition,
                Actions = actions
            });
            return module;
        }

        private static TriggerNodeData Sequence(params TriggerNodeData[] children)
        {
            var node = new TriggerNodeData { Kind = TriggerNodeKind.Action, Type = "seq" };
            node.Children.AddRange(children);
            return node;
        }

        private static TriggerNodeData Condition(string type)
        {
            return new TriggerNodeData { Kind = TriggerNodeKind.Condition, Type = type };
        }

        private static TriggerNodeData Action(string type)
        {
            return new TriggerNodeData { Kind = TriggerNodeKind.Action, Type = type };
        }

        private static TriggerNodeData Flow(string type, params TriggerNodeData[] children)
        {
            var node = Action(type);
            node.Children.AddRange(children);
            return node;
        }

        private static TriggerNodeData Compare(
            string type,
            TriggerValueRefData left,
            TriggerValueRefData right)
        {
            var leftName = type.StartsWith("num_var_", System.StringComparison.Ordinal) ? "variable" : "left";
            var rightName = type.StartsWith("num_var_", System.StringComparison.Ordinal) ? "value" : "right";
            return new TriggerNodeData
            {
                Kind = TriggerNodeKind.Condition,
                Type = type,
                Arguments =
                {
                    new TriggerArgumentData { Name = leftName, Value = left },
                    new TriggerArgumentData { Name = rightName, Value = right }
                }
            };
        }

        private static TriggerValueRefData Payload(string path)
        {
            return new TriggerValueRefData
            {
                Source = TriggerValueSource.Payload,
                Type = TriggerValueType.Number,
                Path = path
            };
        }

        private static TriggerValueRefData Local(string path)
        {
            return new TriggerValueRefData
            {
                Source = TriggerValueSource.LocalBlackboard,
                Type = TriggerValueType.Number,
                Path = path
            };
        }

        private static TriggerValueRefData Number(double value)
        {
            return new TriggerValueRefData
            {
                Source = TriggerValueSource.Constant,
                Type = TriggerValueType.Number,
                NumberValue = value
            };
        }

        private static TriggerAuthoringDryRunInput Parse(string json)
        {
            Assert.That(TriggerAuthoringDryRunInput.TryParse(json, out var input, out var error), Is.True, error);
            return input;
        }

        private static TriggerAuthoringDryRunEntry Find(TriggerAuthoringDryRunResult result, string nodeId)
        {
            return result.Entries.Find(entry => entry.NodeId == nodeId);
        }
    }
}
#endif
