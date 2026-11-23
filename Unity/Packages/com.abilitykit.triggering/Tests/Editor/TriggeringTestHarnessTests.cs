using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Testing;
using AbilityKit.Triggering.Runtime;
using NUnit.Framework;

namespace AbilityKit.Triggering.Tests
{
    public sealed class TriggeringTestHarnessTests
    {
        /// <summary>
        /// 验证测试夹具由独立程序集提供，运行时程序集不再导出夹具类型。
        /// </summary>
        [Test]
        public void TestHarness_IsOwnedByTestSupportAssembly()
        {
            var harnessType = typeof(TriggeringTestHarness<>);

            Assert.That(harnessType.Assembly.GetName().Name, Is.EqualTo("AbilityKit.Triggering.TestSupport"));
            Assert.That(typeof(TriggerRunner<>).Assembly.GetType(harnessType.FullName), Is.Null);
        }

        private sealed class TestContext
        {
            public TestContext(int value)
            {
                Value = value;
            }

            public int Value { get; set; }
        }

        private sealed class Ping
        {
            public Ping(int amount)
            {
                Amount = amount;
            }

            public int Amount { get; }
        }

        [Test]
        public void Publish_RegisteredPlan_ExecutesActionWithCurrentContext()
        {
            var observed = new List<int>();
            var key = new EventKey<Ping>(1001);
            var actionId = new ActionId(2001);

            using (var harness = new TriggeringTestHarness<TestContext>(new TestContext(7)))
            {
                harness.RegisterAction<NamedAction0<Ping, object, TestContext>>(
                    actionId,
                    (triggerArgs, actionArgs, ctx) => observed.Add(ctx.Context.Value + triggerArgs.Amount));

                var plan = new TriggerPlan<Ping>(
                    phase: 0,
                    priority: 0,
                    triggerId: 3001,
                    actions: new[] { new ActionCallPlan(actionId) });

                harness.RegisterPlan(key, in plan);
                harness.Publish(key, new Ping(5));
            }

            Assert.That(observed, Is.EqualTo(new[] { 12 }));
        }

        [Test]
        public void AdvanceTime_DelayedPlanAction_ExecutesAfterDelay()
        {
            var observed = new List<int>();
            var key = new EventKey<Ping>(1002);
            var actionId = new ActionId(2002);

            using (var harness = new TriggeringTestHarness<TestContext>(new TestContext(3)))
            {
                harness.RegisterAction<NamedAction0<Ping, object, TestContext>>(
                    actionId,
                    (triggerArgs, actionArgs, ctx) => observed.Add(ctx.Context.Value + triggerArgs.Amount));

                var delayedCall = new ActionCallPlan(
                    actionId,
                    0,
                    default,
                    default,
                    null,
                    EActionScheduleMode.Delayed,
                    10f,
                    1,
                    true,
                    EActionExecutionPolicy.Immediate);

                var plan = new TriggerPlan<Ping>(
                    phase: 0,
                    priority: 0,
                    triggerId: 3002,
                    actions: new[] { delayedCall });

                harness.RegisterPlan(key, in plan);
                harness.Publish(key, new Ping(4));

                Assert.That(observed, Is.Empty);

                harness.AdvanceTime(9f);
                Assert.That(observed, Is.Empty);

                harness.AdvanceTime(1f);
            }

            Assert.That(observed, Is.EqualTo(new[] { 7 }));
        }
    }
}
