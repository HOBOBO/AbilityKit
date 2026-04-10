using System;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Triggering.Runtime.Example
{
    public static class QueuedEventBusExample
    {
        public readonly struct Msg
        {
            public readonly int Id;
            public Msg(int id) { Id = id; }
        }

        public static void RunOnce()
        {
            // EventBusOptions 默认通常是 Immediate；这里强制使用队列派发模式。
            var bus = new EventBus(new EventBusOptions(EEventDispatchMode.Queued, maxFlushPasses: 8));
            var runner = new TriggerRunner<TriggerContext>(bus, new Registry.FunctionRegistry(), new Registry.ActionRegistry());

            var key = new EventKey<Msg>(Eventing.StableStringId.Get("event:msg"));

            runner.Register(key,
                new DelegateTrigger<Msg, TriggerContext>(
                    predicate: (evt, ctx) => true,
                    actions: (evt, ctx) => Console.WriteLine("Received msg id=" + evt.Id)),
                phase: 0,
                priority: 0);

            // Publish 不会立刻触发（因为是 queued）。
            bus.Publish(key, new Msg(123));

            // Flush 才会真正派发。
            bus.Flush();
        }
    }
}
