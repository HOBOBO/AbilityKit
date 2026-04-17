using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Abstractions;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Instance;
using AbilityKit.Triggering.Runtime.Plan;
using NUnit.Framework;

namespace AbilityKit.Triggering.Tests
{
    /// <summary>
    /// TriggerInstance 测试
    /// </summary>
    public class TriggerInstanceTests
    {
        [Test]
        public void CreateInstance_ShouldBeInactiveByDefault()
        {
            var config = new TriggerConfig<object>(
                phase: 0,
                priority: 0,
                triggerId: 1,
                actions: Array.Empty<ActionCallPlan>(),
                interruptPriority: 0,
                cue: null,
                schedule: ScheduleModePlan.None);

            var instance = new TriggerInstance<object>(config);

            Assert.That(instance.IsActive, Is.False);
            Assert.That(instance.TriggerId, Is.EqualTo(1));
        }

        [Test]
        public void Execute_ShouldMarkActiveAndIncrementCount()
        {
            var config = new TriggerConfig<object>(
                phase: 0,
                priority: 0,
                triggerId: 1,
                actions: Array.Empty<ActionCallPlan>(),
                interruptPriority: 0,
                cue: null,
                schedule: ScheduleModePlan.None);

            var instance = new TriggerInstance<object>(config);
            var context = new TestTriggerContext();

            instance.Execute(null, context);

            Assert.That(instance.IsActive, Is.True);
            var state = instance.GetState();
            Assert.That(state.ExecutionCount, Is.EqualTo(1));
        }

        [Test]
        public void Interrupt_ShouldDeactivateInstance()
        {
            var config = new TriggerConfig<object>(
                phase: 0,
                priority: 0,
                triggerId: 1,
                actions: Array.Empty<ActionCallPlan>(),
                interruptPriority: 0,
                cue: null,
                schedule: ScheduleModePlan.None);

            var instance = new TriggerInstance<object>(config);
            var context = new TestTriggerContext();

            instance.Execute(null, context);
            instance.Interrupt("Test interrupt");

            Assert.That(instance.IsActive, Is.False);
            Assert.That(instance.GetState().IsActive, Is.False);
        }

        [Test]
        public void Evaluate_WithNoPredicate_ShouldReturnTrue()
        {
            var config = new TriggerConfig<object>(
                phase: 0,
                priority: 0,
                triggerId: 1,
                actions: Array.Empty<ActionCallPlan>(),
                interruptPriority: 0,
                cue: null,
                schedule: ScheduleModePlan.None);

            var instance = new TriggerInstance<object>(config);
            var context = new TestTriggerContext();

            var result = instance.Evaluate(null, context);

            Assert.That(result, Is.True);
        }

        [Test]
        public void StateManagement_ShouldUseCopyOnWrite()
        {
            var config = new TriggerConfig<object>(
                phase: 0,
                priority: 0,
                triggerId: 1,
                actions: Array.Empty<ActionCallPlan>(),
                interruptPriority: 0,
                cue: null,
                schedule: ScheduleModePlan.None);

            var instance = new TriggerInstance<object>(config);

            instance.UpdateState(s =>
            {
                s.IsActive = true;
                s.CurrentActionIndex = 10;
            });

            var state = instance.GetState();
            Assert.That(state.IsActive, Is.True);
            Assert.That(state.CurrentActionIndex, Is.EqualTo(10));
        }
    }

    /// <summary>
    /// 测试用上下文
    /// </summary>
    internal sealed class TestTriggerContext : ITriggerContext
    {
        public IBlackboardResolver Blackboards => null;
        public IEventBus EventBus => null;
        public IFrameClock FrameClock => null;
        public IRandomProvider Random => null;
        public T GetGameContext<T>() where T : class => null;
    }
}
