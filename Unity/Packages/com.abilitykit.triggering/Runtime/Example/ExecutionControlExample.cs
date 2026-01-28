using System;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Triggering.Runtime.Example
{
    public static class ExecutionControlExample
    {
        public readonly struct Ping
        {
            public readonly int N;
            public Ping(int n) { N = n; }
        }

        public static void RunOnce_StopPropagation()
        {
            var bus = new EventBus();
            var runner = new TriggerRunner(bus, new Registry.FunctionRegistry(), new Registry.ActionRegistry());

            var key = new EventKey<Ping>(Eventing.StableStringId.Get("event:ping"));

            // 触发器A：始终为 true，并在执行后停止后续触发器传播
            runner.Register(key,
                new DelegateTrigger<Ping>(
                    predicate: (evt, ctx) => true,
                    actions: (evt, ctx) =>
                    {
                        Console.WriteLine("触发器A触发 -> StopPropagation");
                        ctx.Control.StopPropagation = true;
                    }),
                phase: 0,
                priority: 0);

            // 触发器B：因为被 StopPropagation，应该不会执行
            runner.Register(key,
                new DelegateTrigger<Ping>(
                    predicate: (evt, ctx) => true,
                    actions: (evt, ctx) =>
                    {
                        Console.WriteLine("触发器B触发（不应该发生）");
                    }),
                phase: 0,
                priority: 1);

            bus.Publish(key, new Ping(1));
            bus.Flush();
        }

        public static void RunOnce_Cancel()
        {
            var bus = new EventBus();
            var runner = new TriggerRunner(bus, new Registry.FunctionRegistry(), new Registry.ActionRegistry());

            var key = new EventKey<Ping>(Eventing.StableStringId.Get("event:ping"));

            runner.Register(key,
                new DelegateTrigger<Ping>(
                    predicate: (evt, ctx) =>
                    {
                        Console.WriteLine("触发器A Evaluate -> Cancel");
                        ctx.Control.Cancel = true;
                        return true;
                    },
                    actions: (evt, ctx) => Console.WriteLine("触发器A Execute")),
                phase: 0,
                priority: 0);

            runner.Register(key,
                new DelegateTrigger<Ping>(
                    predicate: (evt, ctx) => true,
                    actions: (evt, ctx) => Console.WriteLine("触发器B触发（不应该发生）")),
                phase: 0,
                priority: 1);

            bus.Publish(key, new Ping(2));
            bus.Flush();
        }
    }
}
