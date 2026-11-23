using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Example
{
    /// <summary>
    /// 演示队列模式的显式派发与同一事件中的计划执行顺序。
    /// </summary>
    public static class QueuedEventBusExample
    {
        /// <summary>
        /// 运行一次示例；发布只入队，刷新后按阶段升序、同阶段优先级降序执行。
        /// 若行为与示例约定不符，则抛出异常。
        /// </summary>
        public static void RunOnce()
        {
            var bus = new EventBus(new EventBusOptions(EEventDispatchMode.Queued, maxFlushPasses: 8));
            var actions = new ActionRegistry();
            var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), actions);
            var key = new EventKey<int>(StableStringId.Get("sample:queued-events:message"));
            var executionOrder = new List<string>();

            using var laterPhase = RegisterPlan("later-phase", phase: 1, priority: 100);
            using var lowerPriority = RegisterPlan("lower-priority", phase: 0, priority: 0);
            using var higherPriority = RegisterPlan("higher-priority", phase: 0, priority: 10);

            bus.Publish(key, 123);
            if (executionOrder.Count != 0)
            {
                throw new InvalidOperationException("队列事件不应在刷新前执行。");
            }

            bus.Flush();
            var order = string.Join(",", executionOrder);
            if (order != "higher-priority:123,lower-priority:123,later-phase:123")
            {
                throw new InvalidOperationException($"触发器执行顺序不符合预期：{order}");
            }

            IDisposable RegisterPlan(string label, int phase, int priority)
            {
                var actionId = new ActionId(StableStringId.Get($"sample:queued-events:{label}"));
                actions.Register<NamedAction0<int, object, object>>(
                    actionId,
                    (message, args, context) => executionOrder.Add($"{label}:{message}"),
                    isDeterministic: true);
                var plan = new TriggerPlan<int>(phase: phase, priority: priority, actions: new[] { new ActionCallPlan(actionId) });
                return runner.RegisterPlan(key, in plan);
            }
        }
    }
}
