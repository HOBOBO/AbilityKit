using System;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Example
{
    public static class AnyPredicate_ContextSourceExample
    {
        public readonly struct Ping
        {
            public readonly int N;
            public Ping(int n) { N = n; }
        }

        private sealed class DemoContextSource : ITriggerContextSource<TriggerContext>
        {
            private int _frame;
            private int _seq;

            public void NextFrame() { _frame++; }

            public TriggerContext GetContext()
            {
                _seq++;
                return new TriggerContext(frame: _frame, sequence: _seq);
            }
        }

        public static void RunOnce()
        {
            // 这个示例演示：PredicateKind=Function 的“任意条件”如何从 ctx.Context（来自 contextSource）取数据做内部判断。
            // 注意：这里的 TriggerContext 目前只有 Frame/Sequence，因此示例用 Frame 做判断。

            var bus = new EventBus();
            var functions = new FunctionRegistry();
            var actions = new ActionRegistry();

            var contextSource = new DemoContextSource();
            contextSource.NextFrame(); // 让 frame=1

            // 1) 注册一个“任意条件”函数：只允许在偶数帧触发
            var predicateId = new FunctionId(StableStringId.Get("pred:even_frame_only"));
            functions.Register<PlannedTrigger<Ping, TriggerContext>.Predicate0>(
                predicateId,
                (evt, ctx) =>
                {
                    // 从上下文取数据，并在内部进行任意判断
                    return (ctx.Context.Frame % 2) == 0;
                },
                isDeterministic: true);

            // 2) 注册一个 action（仅用于观测触发效果）
            var actionId = new ActionId(StableStringId.Get("action:print_context"));
            actions.Register<PlannedTrigger<Ping, TriggerContext>.Action0>(
                actionId,
                (evt, ctx) =>
                {
                    Console.WriteLine("触发成功：frame=" + ctx.Context.Frame + " seq=" + ctx.Context.Sequence + " payload.N=" + evt.N);
                },
                isDeterministic: true);

            var runner = new TriggerRunner<TriggerContext>(
                bus,
                functions,
                actions,
                contextSource: contextSource,
                observer: null,
                blackboards: null,
                payloads: null,
                idNames: null,
                policy: ExecPolicy.DeterministicOnly);

            var key = new EventKey<Ping>(StableStringId.Get("event:ping"));

            // 3) 计划：predicate=Function（任意逻辑），actions=一个打印
            var plan = new TriggerPlan<Ping>(
                phase: 0,
                priority: 0,
                triggerId: 0,
                predicateId: predicateId,
                predicateArgs: null,
                actions: new[] { new ActionCallPlan(actionId) },
                interruptPriority: 0,
                cue: null,
                schedule: default);

            runner.RegisterPlan<Ping, TriggerContext>(key, plan);

            // frame=1 -> 不能触发
            bus.Publish(key, new Ping(1));
            bus.Flush();

            // 切到 frame=2 -> 可以触发
            contextSource.NextFrame();
            bus.Publish(key, new Ping(2));
            bus.Flush();
        }
    }
}
