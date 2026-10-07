using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using NUnit.Framework;

namespace AbilityKit.Triggering.Tests
{
    public sealed class ExecutionControlTests
    {
        private sealed class TestContext
        {
        }

        private readonly struct Ping
        {
        }

        [Test]
        public void StopBelowPriority_SkipsLowerPriorityButKeepsEligibleTriggers()
        {
            var bus = new EventBus();
            var runner = new TriggerRunner<TestContext>(bus, new FunctionRegistry(), new ActionRegistry());
            var key = new EventKey<Ping>(StableStringId.Get("test:execution_control:soft_priority"));
            var executed = 0;

            runner.Register(key,
                new DelegateTrigger<Ping, TestContext>(
                    predicate: (evt, ctx) => true,
                    actions: (evt, ctx) =>
                    {
                        executed += 1;
                        ctx.Control.StopBelowPriority(10, conditionPassed: true, triggerId: 1, sourceName: "high");
                    }),
                priority: 10);

            runner.Register(key,
                new DelegateTrigger<Ping, TestContext>(
                    predicate: (evt, ctx) => true,
                    actions: (evt, ctx) => executed += 100),
                priority: 5);

            runner.Register(key,
                new DelegateTrigger<Ping, TestContext>(
                    predicate: (evt, ctx) => true,
                    actions: (evt, ctx) => executed += 10),
                priority: 20);

            bus.Publish(key, new Ping());

            Assert.That(executed, Is.EqualTo(11));
        }

        [Test]
        public void RejectAction_StopsCurrentPlanWithoutBlockingNextTrigger()
        {
            var bus = new EventBus();
            var runner = new TriggerRunner<TestContext>(bus, new FunctionRegistry(), new ActionRegistry());
            var key = new EventKey<Ping>(StableStringId.Get("test:execution_control:action_rejection"));
            var nextExecuted = false;

            runner.Register(key,
                new DelegateTrigger<Ping, TestContext>(
                    predicate: (evt, ctx) => true,
                    actions: (evt, ctx) => ctx.Control.RejectAction("current plan failed")),
                priority: 10);
            runner.Register(key,
                new DelegateTrigger<Ping, TestContext>(
                    predicate: (evt, ctx) => !ctx.Control.IsActionRejected,
                    actions: (evt, ctx) => nextExecuted = true),
                priority: 5);

            bus.Publish(key, new Ping());

            Assert.That(nextExecuted, Is.True);
        }

        [Test]
        public void StopAll_StopsAllFollowingTriggers()
        {
            var bus = new EventBus();
            var runner = new TriggerRunner<TestContext>(bus, new FunctionRegistry(), new ActionRegistry());
            var key = new EventKey<Ping>(StableStringId.Get("test:execution_control:hard_stop"));
            var executed = 0;

            runner.Register(key,
                new DelegateTrigger<Ping, TestContext>(
                    predicate: (evt, ctx) => true,
                    actions: (evt, ctx) =>
                    {
                        executed += 1;
                        ctx.Control.StopAll();
                    }),
                priority: 10);

            runner.Register(key,
                new DelegateTrigger<Ping, TestContext>(
                    predicate: (evt, ctx) => true,
                    actions: (evt, ctx) => executed += 100),
                priority: 5);

            bus.Publish(key, new Ping());

            Assert.That(executed, Is.EqualTo(1));
        }
    }
}
