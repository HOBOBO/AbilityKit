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
    /// TriggerConfig 测试
    /// </summary>
    public class TriggerConfigTests
    {
        [Test]
        public void CreateTriggerConfig_WithNoCondition_ShouldHaveNoPredicate()
        {
            var config = new TriggerConfig<object>(
                phase: 0,
                priority: 100,
                triggerId: 1,
                actions: new[] { new ActionCallPlan(ActionId.Empty) },
                interruptPriority: 0,
                cue: null,
                schedule: ScheduleModePlan.None);

            Assert.That(config.HasPredicate, Is.False);
            Assert.That(config.PredicateKind, Is.EqualTo(EPredicateKind.None));
        }

        [Test]
        public void CreateTriggerConfig_WithFunctionPredicate_ShouldHavePredicate()
        {
            var config = new TriggerConfig<object>(
                phase: 0,
                priority: 100,
                triggerId: 1,
                actions: Array.Empty<ActionCallPlan>(),
                interruptPriority: 0,
                cue: null,
                schedule: ScheduleModePlan.None);

            Assert.That(config.HasPredicate, Is.True);
            Assert.That(config.PredicateKind, Is.EqualTo(EPredicateKind.Function));
        }

        [Test]
        public void ExportImportState_ShouldPreserveRuntimeState()
        {
            var config = new TriggerConfig<object>(
                phase: 1,
                priority: 100,
                triggerId: 42,
                actions: Array.Empty<ActionCallPlan>(),
                interruptPriority: 0,
                cue: null,
                schedule: ScheduleModePlan.None);

            config.SetRuntimeState(new TriggerInstanceState
            {
                IsActive = true,
                CurrentActionIndex = 5,
                ElapsedFrames = 100,
                ExecutionCount = 3,
                Variables = new Dictionary<string, double> { { "x", 3.14 } }
            });

            var state = config.ExportState();

            var newConfig = new TriggerConfig<object>(
                phase: 0,
                priority: 0,
                triggerId: 0,
                actions: Array.Empty<ActionCallPlan>());

            newConfig.ImportState(state, isAuthoritative: false);

            var restored = newConfig.GetRuntimeState();
            Assert.That(restored.IsActive, Is.True);
            Assert.That(restored.CurrentActionIndex, Is.EqualTo(5));
            Assert.That(restored.ElapsedFrames, Is.EqualTo(100));
            Assert.That(restored.ExecutionCount, Is.EqualTo(3));
            Assert.That(restored.Variables.ContainsKey("x"), Is.True);
            Assert.That(restored.Variables["x"], Is.EqualTo(3.14).Within(0.001));
        }

        [Test]
        public void FromPlan_ShouldConvertTriggerPlanToConfig()
        {
            var plan = new TriggerPlan<object>(
                phase: 5,
                priority: 200,
                triggerId: 123,
                actions: new[] { new ActionCallPlan(ActionId.Empty) },
                interruptPriority: 100,
                cue: null,
                schedule: ScheduleModePlan.Timed(1000f));

            var config = TriggerConfig<object>.FromPlan(plan);

            Assert.That(config.Phase, Is.EqualTo(5));
            Assert.That(config.Priority, Is.EqualTo(200));
            Assert.That(config.TriggerId, Is.EqualTo(123));
            Assert.That(config.Actions.Length, Is.EqualTo(1));
            Assert.That(config.InterruptPriority, Is.EqualTo(100));
        }
    }
}
