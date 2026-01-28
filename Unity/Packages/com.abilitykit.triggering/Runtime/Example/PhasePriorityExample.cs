using System;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Triggering.Runtime.Example
{
    public static class PhasePriorityExample
    {
        public readonly struct Ping
        {
            public readonly int N;
            public Ping(int n) { N = n; }
        }

        public static void RunOnce()
        {
            var bus = new EventBus();
            var runner = new TriggerRunner<TriggerContext>(bus, new Registry.FunctionRegistry(), new Registry.ActionRegistry());

            var key = new EventKey<Ping>(Eventing.StableStringId.Get("event:phase_priority_ping"));

            // 说明：TriggerRunner 会先按 phase 从小到大执行，再按 priority 从小到大执行。
            // 下面我们注册 3 个触发器，观察输出顺序。

            runner.Register(key,
                new DelegateTrigger<Ping, TriggerContext>(
                    predicate: (evt, ctx) => true,
                    actions: (evt, ctx) => Console.WriteLine("触发器C：phase=0 priority=10")),
                phase: 0,
                priority: 10);

            runner.Register(key,
                new DelegateTrigger<Ping, TriggerContext>(
                    predicate: (evt, ctx) => true,
                    actions: (evt, ctx) => Console.WriteLine("触发器A：phase=0 priority=0")),
                phase: 0,
                priority: 0);

            runner.Register(key,
                new DelegateTrigger<Ping, TriggerContext>(
                    predicate: (evt, ctx) => true,
                    actions: (evt, ctx) => Console.WriteLine("触发器B：phase=1 priority=0")),
                phase: 1,
                priority: 0);

            bus.Publish(key, new Ping(1));
            bus.Flush();
        }
    }
}
