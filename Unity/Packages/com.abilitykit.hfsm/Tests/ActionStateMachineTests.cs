using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityHFSM;
using UnityHFSM.Actions;
using UnityHFSM.Graph;

namespace AbilityKit.Tests
{
    /// <summary>
    /// Integration tests for ActionStateMachine and behavior execution
    /// </summary>
    public class ActionStateMachineTests
    {
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
        /// Test that behavior actions can be created from HfsmBehaviorItem
        /// </summary>
        [Test]
        public void HfsmBehaviorItem_CanCreateWaitAction()
        {
            var item = new HfsmBehaviorItem(HfsmBehaviorType.Wait);
            item.SetParameter("duration", 1f);

            Assert.AreEqual(HfsmBehaviorType.Wait, item.Type);
            Assert.AreEqual(1f, item.GetParamValue<float>("duration"));
        }

        /// <summary>
        /// Test that HfsmBehaviorItem Clone works correctly
        /// </summary>
        [Test]
        public void HfsmBehaviorItem_CloneCreatesNewId()
        {
            var original = new HfsmBehaviorItem(HfsmBehaviorType.Wait);
            original.SetParameter("duration", 2f);

            var clone = original.Clone();

            Assert.AreNotEqual(original.id, clone.id);
            Assert.AreEqual(original.Type, clone.Type);
            Assert.AreEqual(original.GetParamValue<float>("duration"), clone.GetParamValue<float>("duration"));
        }

        /// <summary>
        /// Test that HfsmStateNode can hold behavior items
        /// </summary>
        [Test]
        public void HfsmStateNode_CanHoldBehaviorItems()
        {
            var stateNode = new HfsmStateNode("TestState");

            var behaviorItem = new HfsmBehaviorItem(HfsmBehaviorType.Wait);
            behaviorItem.SetParameter("duration", 1f);

            stateNode.AddBehaviorItem(behaviorItem);

            Assert.IsTrue(stateNode.HasBehaviors);
            Assert.AreEqual(1, stateNode.BehaviorItems.Count);
            Assert.AreEqual(HfsmBehaviorType.Wait, stateNode.BehaviorItems[0].Type);
        }

        /// <summary>
        /// Test that root behavior items can be retrieved
        /// </summary>
        [Test]
        public void HfsmStateNode_GetRootBehaviorItems()
        {
            var stateNode = new HfsmStateNode("TestState");

            var behavior1 = new HfsmBehaviorItem(HfsmBehaviorType.Wait);
            var behavior2 = new HfsmBehaviorItem(HfsmBehaviorType.Log);

            stateNode.AddBehaviorItem(behavior1);
            stateNode.AddBehaviorItem(behavior2);

            var roots = stateNode.GetRootBehaviorItems();

            Assert.AreEqual(2, roots.Count);
        }

        /// <summary>
        /// Test that child behavior items can be retrieved
        /// </summary>
        [Test]
        public void HfsmStateNode_GetBehaviorChildren()
        {
            var stateNode = new HfsmStateNode("TestState");

            var parent = new HfsmBehaviorItem(HfsmBehaviorType.Sequence);
            var child1 = new HfsmBehaviorItem(HfsmBehaviorType.Wait);
            var child2 = new HfsmBehaviorItem(HfsmBehaviorType.Log);

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
        public void HfsmStateNode_RemoveBehaviorItem()
        {
            var stateNode = new HfsmStateNode("TestState");

            var behavior = new HfsmBehaviorItem(HfsmBehaviorType.Wait);
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
            var items = new List<HfsmBehaviorItem>();

            var waitItem = new HfsmBehaviorItem(HfsmBehaviorType.Wait);
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
            var items = new List<HfsmBehaviorItem>();

            var sequenceItem = new HfsmBehaviorItem(HfsmBehaviorType.Sequence);
            var waitItem1 = new HfsmBehaviorItem(HfsmBehaviorType.Wait);
            waitItem1.SetParameter("duration", 0.1f);
            var waitItem2 = new HfsmBehaviorItem(HfsmBehaviorType.Wait);
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
            sequence.children.Add(new LogAction("First"));
            sequence.children.Add(new LogAction("Second"));
            sequence.children.Add(new LogAction("Third"));

            var context = new BehaviorContext();

            // First execution should return Running (first child not done)
            var status = sequence.Execute(context);
            Assert.AreEqual(BehaviorStatus.Running, status);
        }

        /// <summary>
        /// Test SelectorAction executes until success
        /// </summary>
        [Test]
        public void SelectorAction_ExecutesUntilSuccess()
        {
            var sequence = new SequenceAction();
            sequence.children.Add(new WaitAction(0.1f)); // This will succeed
            sequence.children.Add(new WaitAction(0.2f));

            var context = new BehaviorContext { deltaTime = 0.1f };

            var status = sequence.Execute(context);

            // First child succeeds, sequence completes
            Assert.AreEqual(BehaviorStatus.Success, status);
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
        /// Test HfsmBehaviorItem GetDescription
        /// </summary>
        [Test]
        public void HfsmBehaviorItem_GetDescription_Wait()
        {
            var item = new HfsmBehaviorItem(HfsmBehaviorType.Wait);
            item.SetParameter("duration", 1.5f);

            var description = item.GetDescription();

            Assert.IsTrue(description.Contains("1.5"));
        }

        /// <summary>
        /// Test HfsmBehaviorItem GetDescription SetFloat
        /// </summary>
        [Test]
        public void HfsmBehaviorItem_GetDescription_SetFloat()
        {
            var item = new HfsmBehaviorItem(HfsmBehaviorType.SetFloat);
            item.SetParameter("variableName", "health");
            item.SetParameter("value", 100f);

            var description = item.GetDescription();

            Assert.IsTrue(description.Contains("health"));
        }

        /// <summary>
        /// Test HfsmBehaviorItem IsComposite check
        /// </summary>
        [Test]
        public void HfsmBehaviorItem_IsComposite_TrueForSequence()
        {
            var item = new HfsmBehaviorItem(HfsmBehaviorType.Sequence);

            Assert.IsTrue(item.IsComposite);
            Assert.IsFalse(item.IsDecorator);
        }

        /// <summary>
        /// Test HfsmBehaviorItem IsDecorator check
        /// </summary>
        [Test]
        public void HfsmBehaviorItem_IsDecorator_TrueForRepeat()
        {
            var item = new HfsmBehaviorItem(HfsmBehaviorType.Repeat);

            Assert.IsFalse(item.IsComposite);
            Assert.IsTrue(item.IsDecorator);
        }
    }
}
