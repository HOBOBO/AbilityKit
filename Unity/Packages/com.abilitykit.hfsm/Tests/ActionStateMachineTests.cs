using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AbilityKit.HFSM;
using AbilityKit.HFSM.Actions;
using AbilityKit.HFSM.Graph;

using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
namespace AbilityKit.Tests
{
    /// <summary>
    /// Integration tests for ActionStateMachine and behavior execution
    /// </summary>
    public class ActionStateMachineTests
    {
        private sealed class CustomCompositeAction : ActionBase, ICompositeAction
        {
            public readonly List<IAction> Children = new List<IAction>();

            public CustomCompositeAction()
            {
            }

            public void AddChild(IAction child) => Children.Add(child);

            public override BehaviorStatus Execute(BehaviorContext context) => BehaviorStatus.Success;
        }

        /// <summary>
        /// Test that ActionStateMachine can be created and initialized
        /// </summary>
        [Test]
        public void ActionStateMachine_CanBeCreated()
        {
            var fsm = new ActionStateMachine();
            Assert.IsNotNull(fsm);
        }

        /// <summary>
        /// Test that states can be added to ActionStateMachine
        /// </summary>
        [Test]
        public void ActionStateMachine_CanAddStates()
        {
            var fsm = new ActionStateMachine();
            fsm.AddState("idle", new ActionState(false));

            Assert.AreEqual(1, fsm.GetAllStateNames().Count);
        }

        /// <summary>
        /// Test that behavior actions can be created from BehaviorItem
        /// </summary>
        [Test]
        public void BehaviorItem_CanCreateWaitAction()
        {
            var item = new BehaviorItem("Wait");
            item.SetParameter("duration", 1f);

            Assert.AreEqual("Wait", item.TypeName);
            Assert.AreEqual(1f, item.GetParamValue<float>("duration"));
        }

        /// <summary>
        /// Test that BehaviorItem Clone works correctly
        /// </summary>
        [Test]
        public void BehaviorItem_CloneCreatesNewId()
        {
            var original = new BehaviorItem("Wait");
            original.SetParameter("duration", 2f);

            var clone = original.Clone();

            Assert.AreNotEqual(original.id, clone.id);
            Assert.AreEqual(original.TypeName, clone.TypeName);
            Assert.AreEqual(original.GetParamValue<float>("duration"), clone.GetParamValue<float>("duration"));
        }

        [Test]
        public void BehaviorItem_CustomTypeNameSurvivesCloneAndSerialization()
        {
            var original = new BehaviorItem("Package.CustomComposite");

            var clone = original.Clone();
            var json = JsonUtility.ToJson(original);

            Assert.AreEqual("Package.CustomComposite", clone.TypeName);
            StringAssert.Contains("\"typeName\":\"Package.CustomComposite\"", json);
            StringAssert.DoesNotContain("typeIndex", json);
        }

        [Test]
        public void Parameter_UsesTypedDefaultValue()
        {
            var parameter = new Parameter("speed", ParameterValueType.Float)
            {
                DefaultFloatValue = 3.5f
            };

            var clone = parameter.Clone("speedClone");
            var json = JsonUtility.ToJson(parameter);

            Assert.AreEqual(3.5f, parameter.GetSerializedDefaultValue());
            Assert.AreEqual(3.5f, clone.GetSerializedDefaultValue());
            StringAssert.DoesNotContain("defaultValueJson", json);
        }

        /// <summary>
        /// Test that StateNode can hold behavior items
        /// </summary>
        [Test]
        public void StateNode_CanHoldBehaviorItems()
        {
            var stateNode = new StateNode("TestState");

            var behaviorItem = new BehaviorItem("Wait");
            behaviorItem.SetParameter("duration", 1f);

            stateNode.AddBehaviorItem(behaviorItem);

            Assert.IsTrue(stateNode.HasBehaviors);
            Assert.AreEqual(1, stateNode.BehaviorItems.Count);
            Assert.AreEqual("Wait", stateNode.BehaviorItems[0].TypeName);
        }

        /// <summary>
        /// Test that root behavior items can be retrieved
        /// </summary>
        [Test]
        public void StateNode_GetRootBehaviorItems()
        {
            var stateNode = new StateNode("TestState");

            var behavior1 = new BehaviorItem("Wait");
            var behavior2 = new BehaviorItem("Log");

            stateNode.AddBehaviorItem(behavior1);
            stateNode.AddBehaviorItem(behavior2);

            var roots = stateNode.GetRootBehaviorItems();

            Assert.AreEqual(2, roots.Count);
        }

        /// <summary>
        /// Test that child behavior items can be retrieved
        /// </summary>
        [Test]
        public void StateNode_GetBehaviorChildren()
        {
            var stateNode = new StateNode("TestState");

            var parent = new BehaviorItem("Sequence");
            var child1 = new BehaviorItem("Wait");
            var child2 = new BehaviorItem("Log");

            parent.childIds.Add(child1.id);
            parent.childIds.Add(child2.id);
            child1.parentId = parent.id;
            child2.parentId = parent.id;

            stateNode.AddBehaviorItem(parent);
            stateNode.AddBehaviorItem(child1);
            stateNode.AddBehaviorItem(child2);

            var children = stateNode.GetBehaviorChildren(parent.id);

            Assert.AreEqual(2, children.Count);
        }

        /// <summary>
        /// Test that behavior item removal works correctly
        /// </summary>
        [Test]
        public void StateNode_RemoveBehaviorItem()
        {
            var stateNode = new StateNode("TestState");

            var behavior = new BehaviorItem("Wait");
            stateNode.AddBehaviorItem(behavior);

            Assert.IsTrue(stateNode.HasBehaviors);

            stateNode.RemoveBehaviorItem(behavior.id);

            Assert.IsFalse(stateNode.HasBehaviors);
        }

        /// <summary>
        /// Test that BehaviorTreeBuilder can build from editor items
        /// </summary>
        [Test]
        public void BehaviorTreeBuilder_BuildFromEditorItems()
        {
            var items = new List<BehaviorItem>();

            var waitItem = new BehaviorItem("Wait");
            waitItem.SetParameter("duration", 0.1f);
            items.Add(waitItem);

            var action = BehaviorTreeBuilder.BuildFromEditorItems(items);

            Assert.IsNotNull(action);
            Assert.IsInstanceOf<WaitAction>(action);
        }

        /// <summary>
        /// Test that BehaviorTreeBuilder can build composite actions
        /// </summary>
        [Test]
        public void BehaviorTreeBuilder_BuildCompositeActions()
        {
            var items = new List<BehaviorItem>();

            var sequenceItem = new BehaviorItem("Sequence");
            var waitItem1 = new BehaviorItem("Wait");
            waitItem1.SetParameter("duration", 0.1f);
            var waitItem2 = new BehaviorItem("Wait");
            waitItem2.SetParameter("duration", 0.2f);

            sequenceItem.childIds.Add(waitItem1.id);
            sequenceItem.childIds.Add(waitItem2.id);
            waitItem1.parentId = sequenceItem.id;
            waitItem2.parentId = sequenceItem.id;

            items.Add(sequenceItem);
            items.Add(waitItem1);
            items.Add(waitItem2);

            var action = BehaviorTreeBuilder.BuildFromEditorItems(items);

            Assert.IsNotNull(action);
            Assert.IsInstanceOf<SequenceAction>(action);

            var sequence = action as SequenceAction;
            Assert.AreEqual(2, sequence.children.Count);
        }

        [Test]
        public void BehaviorTreeBuilder_BuildsRegisteredExternalComposite()
        {
            const string typeName = "Tests.CustomComposite";
            if (!BehaviorTypeRegistry.IsInitialized)
                BehaviorTypeRegistry.Initialize();
            if (!BehaviorTypeRegistry.IsRegistered(typeName))
            {
                BehaviorTypeRegistry.RegisterExternal<CustomCompositeAction>(
                    typeName,
                    "Custom Composite",
                    BehaviorCategory.Composite);
            }

            var root = new BehaviorItem(typeName);
            var child = new BehaviorItem("Wait");
            root.childIds.Add(child.id);
            child.parentId = root.id;

            var action = BehaviorTreeBuilder.BuildFromEditorItems(
                new List<BehaviorItem> { root, child },
                root.id);

            var composite = (CustomCompositeAction)action;
            Assert.AreEqual(1, composite.Children.Count);
            Assert.IsInstanceOf<WaitAction>(composite.Children[0]);
        }

        [Test]
        public void BehaviorTreeBuilder_RejectsUnknownType()
        {
            var item = new BehaviorItem("Tests.UnknownBehavior");

            Assert.Throws<InvalidOperationException>(() =>
                BehaviorTreeBuilder.BuildFromEditorItems(new List<BehaviorItem> { item }, item.id));
        }

        [Test]
        public void BehaviorTreeBuilder_ConnectsDecoratorChild()
        {
            var root = new BehaviorItem("Invert");
            var child = new BehaviorItem("Log");
            root.childIds.Add(child.id);
            child.parentId = root.id;

            var action = BehaviorTreeBuilder.BuildFromEditorItems(
                new List<BehaviorItem> { root, child },
                root.id);

            Assert.IsInstanceOf<LogAction>(((InvertAction)action).child);
        }

        [Test]
        public void ActionBehaviorState_StartsOnFirstLogicAndCompletesOnlyOnce()
        {
            var node = new StateNode("ActionState");
            var behavior = new BehaviorItem("Log");
            node.AddBehaviorItem(behavior);
            var state = new ActionBehaviorState<string, string>(
                node,
                needsExitTime: false,
                isGhostState: false,
                mono: null,
                userData: null,
                parentFsm: null);
            var completionCount = 0;
            state.OnBehaviorCompleted += (_, status) =>
            {
                Assert.AreEqual(BehaviorStatus.Success, status);
                completionCount++;
            };

            state.OnEnter();
            state.OnLogic();
            state.OnLogic();

            Assert.AreEqual(1, completionCount);
        }

        /// <summary>
        /// Test WaitAction execution
        /// </summary>
        [Test]
        public void WaitAction_CompletesAfterDuration()
        {
            var waitAction = new WaitAction(0.1f);
            var context = new BehaviorContext { deltaTime = 0.1f };

            var status = waitAction.Execute(context);

            Assert.AreEqual(BehaviorStatus.Success, status);
        }

        /// <summary>
        /// Test WaitAction returns Running before completion
        /// </summary>
        [Test]
        public void WaitAction_ReturnsRunningBeforeCompletion()
        {
            var waitAction = new WaitAction(0.2f);
            var context = new BehaviorContext { deltaTime = 0.1f };

            var status = waitAction.Execute(context);

            Assert.AreEqual(BehaviorStatus.Running, status);
        }

        /// <summary>
        /// Test SequenceAction executes children in order
        /// </summary>
        [Test]
        public void SequenceAction_ExecutesInOrder()
        {
            var sequence = new SequenceAction();
            sequence.children.Add(new LogAction("First") { logToConsole = false });
            sequence.children.Add(new LogAction("Second") { logToConsole = false });
            sequence.children.Add(new LogAction("Third") { logToConsole = false });

            var messages = new List<string>();
            var context = new BehaviorContext { onLog = messages.Add };

            var status = sequence.Execute(context);

            Assert.AreEqual(BehaviorStatus.Success, status);
            CollectionAssert.AreEqual(new[] { "First", "Second", "Third" }, messages);
        }

        /// <summary>
        /// Test SelectorAction executes until success
        /// </summary>
        [Test]
        public void SelectorAction_ExecutesUntilSuccess()
        {
            var first = new LogAction("First") { logToConsole = false };
            var second = new LogAction("Second") { logToConsole = false };
            var third = new LogAction("Third") { logToConsole = false };
            var selector = new SelectorAction();
            selector.children.Add(new InvertAction(first));
            selector.children.Add(second);
            selector.children.Add(third);

            var messages = new List<string>();
            var context = new BehaviorContext { onLog = messages.Add };

            var status = selector.Execute(context);

            Assert.AreEqual(BehaviorStatus.Success, status);
            CollectionAssert.AreEqual(new[] { "First", "Second" }, messages);
        }

        /// <summary>
        /// Test RepeatAction repeats specified times
        /// </summary>
        [Test]
        public void RepeatAction_RepeatsSpecifiedTimes()
        {
            var successAction = new LogAction("Test");
            var repeat = new RepeatAction(successAction, 3);

            var context = new BehaviorContext { deltaTime = 0.1f };

            // Execute should complete because child always succeeds
            var status = repeat.Execute(context);

            Assert.AreEqual(BehaviorStatus.Success, status);
        }

        /// <summary>
        /// Test InvertAction inverts result
        /// </summary>
        [Test]
        public void InvertAction_InvertsResult()
        {
            var failAction = new LogAction("Test"); // Always succeeds
            var invert = new InvertAction(failAction);

            var context = new BehaviorContext();

            var status = invert.Execute(context);

            Assert.AreEqual(BehaviorStatus.Failure, status);
        }

        /// <summary>
        /// Test SetFloatAction sets variable
        /// </summary>
        [Test]
        public void SetFloatAction_SetsVariable()
        {
            var setFloat = new SetFloatAction("testVar", 5f);
            var context = new BehaviorContext();

            setFloat.Execute(context);

            Assert.AreEqual(5f, context.GetVariable<float>("testVar"));
        }

        /// <summary>
        /// Test SetBoolAction sets variable
        /// </summary>
        [Test]
        public void SetBoolAction_SetsVariable()
        {
            var setBool = new SetBoolAction("flag", true);
            var context = new BehaviorContext();

            setBool.Execute(context);

            Assert.AreEqual(true, context.GetVariable<bool>("flag"));
        }

        /// <summary>
        /// Test SetActiveAction functionality (requires GameObject)
        /// </summary>
        [Test]
        public void SetActiveAction_CreatesInstance()
        {
            var setActive = new SetActiveAction();

            Assert.IsNotNull(setActive);
        }

        /// <summary>
        /// Test PlayAnimationAction creates instance with parameters
        /// </summary>
        [Test]
        public void PlayAnimationAction_CreatesInstance()
        {
            var playAnim = new PlayAnimationAction("Idle", 0.25f);

            Assert.AreEqual("Idle", playAnim.stateName);
            Assert.AreEqual(0.25f, playAnim.crossFadeDuration);
        }

        /// <summary>
        /// Test ParallelAction creates instance
        /// </summary>
        [Test]
        public void ParallelAction_CreatesInstance()
        {
            var parallel = new ParallelAction();

            Assert.IsNotNull(parallel);
        }

        /// <summary>
        /// Test RandomSelectorAction creates instance
        /// </summary>
        [Test]
        public void RandomSelectorAction_CreatesInstance()
        {
            var randomSelector = new RandomSelectorAction();

            Assert.IsNotNull(randomSelector);
        }

        /// <summary>
        /// Test TimeLimitAction creates instance
        /// </summary>
        [Test]
        public void TimeLimitAction_CreatesInstance()
        {
            var timeLimit = new TimeLimitAction(null, 5f);

            Assert.AreEqual(5f, timeLimit.timeLimit);
        }

        /// <summary>
        /// Test UntilSuccessAction creates instance
        /// </summary>
        [Test]
        public void UntilSuccessAction_CreatesInstance()
        {
            var untilSuccess = new UntilSuccessAction();

            Assert.IsNotNull(untilSuccess);
        }

        /// <summary>
        /// Test CooldownAction creates instance
        /// </summary>
        [Test]
        public void CooldownAction_CreatesInstance()
        {
            var cooldown = new CooldownAction(null, 1f);

            Assert.AreEqual(1f, cooldown.cooldownDuration);
        }

        /// <summary>
        /// Test BehaviorItem GetDescription
        /// </summary>
        [Test]
        public void BehaviorItem_GetDescription_Wait()
        {
            var item = new BehaviorItem("Wait");
            item.SetParameter("duration", 1.5f);

            var description = item.GetDescription();

            Assert.IsTrue(description.Contains("1.5"));
        }

        /// <summary>
        /// Test BehaviorItem GetDescription SetFloat
        /// </summary>
        [Test]
        public void BehaviorItem_GetDescription_SetFloat()
        {
            var item = new BehaviorItem("SetFloat");
            item.SetParameter("variableName", "health");
            item.SetParameter("value", 100f);

            var description = item.GetDescription();

            Assert.IsTrue(description.Contains("health"));
        }

        /// <summary>
        /// Test BehaviorItem IsComposite check
        /// </summary>
        [Test]
        public void BehaviorItem_IsComposite_TrueForSequence()
        {
            var item = new BehaviorItem("Sequence");

            Assert.IsTrue(item.IsComposite);
            Assert.IsFalse(item.IsDecorator);
        }

        /// <summary>
        /// Test BehaviorItem IsDecorator check
        /// </summary>
        [Test]
        public void BehaviorItem_IsDecorator_TrueForRepeat()
        {
            var item = new BehaviorItem("Repeat");

            Assert.IsFalse(item.IsComposite);
            Assert.IsTrue(item.IsDecorator);
        }
    }
}
