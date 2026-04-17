using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Runtime.Abstractions;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Instance;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Registry;
using NUnit.Framework;

namespace AbilityKit.Triggering.Tests
{
    /// <summary>
    /// TriggerRegistry 测试
    /// </summary>
    public class TriggerRegistryTests
    {
        [Test]
        public void Register_ShouldAddToRegistry()
        {
            var registry = new TriggerRegistry();

            var plan = new TriggerPlan<object>(
                phase: 0,
                priority: 0,
                triggerId: 100,
                actions: Array.Empty<ActionCallPlan>(),
                interruptPriority: 0,
                cue: null,
                schedule: ScheduleModePlan.None);

            var handle = registry.Register(plan);

            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(handle.TriggerId, Is.EqualTo(100));
        }

        [Test]
        public void Register_ShouldAssignIdIfZero()
        {
            var registry = new TriggerRegistry();

            var plan = new TriggerPlan<object>(
                phase: 0,
                priority: 0,
                triggerId: 0,
                actions: Array.Empty<ActionCallPlan>(),
                interruptPriority: 0,
                cue: null,
                schedule: ScheduleModePlan.None);

            var handle1 = registry.Register(plan);
            var handle2 = registry.Register(plan);

            Assert.That(handle1.TriggerId, Is.Not.EqualTo(handle2.TriggerId));
        }

        [Test]
        public void Unregister_ShouldRemoveFromRegistry()
        {
            var registry = new TriggerRegistry();

            var plan = new TriggerPlan<object>(
                phase: 0,
                priority: 0,
                triggerId: 100,
                actions: Array.Empty<ActionCallPlan>(),
                interruptPriority: 0,
                cue: null,
                schedule: ScheduleModePlan.None);

            var handle = registry.Register(plan);
            Assert.That(registry.Count, Is.EqualTo(1));

            var result = registry.Unregister(handle.TriggerId);

            Assert.That(result, Is.True);
            Assert.That(registry.Count, Is.EqualTo(0));
        }

        [Test]
        public void TryGet_ShouldReturnRegisteredInstance()
        {
            var registry = new TriggerRegistry();

            var plan = new TriggerPlan<object>(
                phase: 0,
                priority: 0,
                triggerId: 100,
                actions: Array.Empty<ActionCallPlan>(),
                interruptPriority: 0,
                cue: null,
                schedule: ScheduleModePlan.None);

            registry.Register(plan);

            var found = registry.TryGet<object>(100, out var instance);

            Assert.That(found, Is.True);
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.TriggerId, Is.EqualTo(100));
        }

        [Test]
        public void TryGet_ShouldReturnFalseForUnregistered()
        {
            var registry = new TriggerRegistry();

            var found = registry.TryGet<object>(999, out var instance);

            Assert.That(found, Is.False);
            Assert.That(instance, Is.Null);
        }

        [Test]
        public void GetAllTriggers_ShouldReturnAll()
        {
            var registry = new TriggerRegistry();

            for (int i = 0; i < 5; i++)
            {
                var plan = new TriggerPlan<object>(
                    phase: 0,
                    priority: 0,
                    triggerId: i,
                    actions: Array.Empty<ActionCallPlan>());
                registry.Register(plan);
            }

            var all = new List<ITrigger>();
            foreach (var t in registry.GetAllTriggers())
            {
                all.Add(t);
            }

            Assert.That(all.Count, Is.EqualTo(5));
        }

        [Test]
        public void Clear_ShouldRemoveAll()
        {
            var registry = new TriggerRegistry();

            for (int i = 0; i < 3; i++)
            {
                var plan = new TriggerPlan<object>(
                    phase: 0,
                    priority: 0,
                    triggerId: i,
                    actions: Array.Empty<ActionCallPlan>());
                registry.Register(plan);
            }

            registry.Clear();

            Assert.That(registry.Count, Is.EqualTo(0));
        }

        [Test]
        public void Handle_Dispose_ShouldUnregister()
        {
            var registry = new TriggerRegistry();

            var plan = new TriggerPlan<object>(
                phase: 0,
                priority: 0,
                triggerId: 100,
                actions: Array.Empty<ActionCallPlan>());

            var handle = registry.Register(plan);
            Assert.That(registry.Count, Is.EqualTo(1));

            handle.Dispose();

            Assert.That(registry.Count, Is.EqualTo(0));
        }

        [Test]
        public void Dispose_ShouldClearAll()
        {
            var registry = new TriggerRegistry();

            for (int i = 0; i < 3; i++)
            {
                var plan = new TriggerPlan<object>(
                    phase: 0,
                    priority: 0,
                    triggerId: i,
                    actions: Array.Empty<ActionCallPlan>());
                registry.Register(plan);
            }

            registry.Dispose();

            Assert.That(registry.Count, Is.EqualTo(0));
        }
    }
}