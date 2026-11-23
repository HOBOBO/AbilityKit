using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Validation;

namespace AbilityKit.Triggering.Runtime.Example
{
    /// <summary>
    /// 演示即时事件、规则条件、执行顺序和退订，不要求宿主装配调度。
    /// </summary>
    public static class FormalTriggeringMainlineExample
    {
        private sealed class DamageEvent
        {
            public readonly int Amount;

            public DamageEvent(int amount)
            {
                Amount = amount;
            }
        }

        private sealed class ExampleContext
        {
            public int MinDamage { get; set; }
            public List<string> Logs { get; } = new List<string>();
        }

        private sealed class ExampleContextSource : ITriggerContextSource<ExampleContext>
        {
            public ExampleContext Current { get; } = new ExampleContext { MinDamage = 10 };

            public ExampleContext GetContext()
            {
                return Current;
            }
        }

        /// <summary>
        /// 发布即时事件；条件、排序或退订不符时抛出异常。
        /// </summary>
        public static void RunOnce()
        {
            var bus = new EventBus();
            var functions = new FunctionRegistry();
            var actions = new ActionRegistry();
            var contextSource = new ExampleContextSource();

            var triggerKey = new EventKey<DamageEvent>(Eventing.StableStringId.Get("sample:formal:damage"));
            var functionId = new FunctionId(Eventing.StableStringId.Get("sample:formal:is_strong_hit"));

            functions.Register<Predicate0<DamageEvent, ExampleContext>>(
                functionId,
                (evt, ctx) => evt.Amount >= ctx.Context.MinDamage,
                isDeterministic: true);

            var runner = new TriggerRunner<ExampleContext>(bus, functions, actions, contextSource: contextSource);
            var logs = contextSource.Current.Logs;
            {
                using var laterPhase = RegisterPlan("later", phase: 1, priority: 100);
                using var lowerPriority = RegisterPlan("lower", phase: 0, priority: 0);
                using var firstAtPriority = RegisterPlan("first", phase: 0, priority: 10);
                using var secondAtPriority = RegisterPlan("second", phase: 0, priority: 10);

                bus.Publish(triggerKey, new DamageEvent(8));
                if (logs.Count != 0)
                    throw new InvalidOperationException("不满足条件的事件不应执行动作。");

                bus.Publish(triggerKey, new DamageEvent(12));
                var order = string.Join(",", logs);
                if (order != "first:12,second:12,lower:12,later:12")
                    throw new InvalidOperationException($"即时执行顺序不符合预期：{order}");
            }

            bus.Publish(triggerKey, new DamageEvent(12));
            if (bus.HasSubscribers(triggerKey) || logs.Count != 4)
                throw new InvalidOperationException("释放最后一条注册后，应退订事件，不再执行规则。");

            IDisposable RegisterPlan(string label, int phase, int priority)
            {
                var id = $"sample:formal:record_hit:{label}";
                var actionId = new ActionId(Eventing.StableStringId.Get(id));
                actions.Register<NamedAction0<DamageEvent, object, ExampleContext>>(
                    actionId,
                    (evt, actionArgs, ctx) => ctx.Context.Logs.Add($"{label}:{evt.Amount}"),
                    isDeterministic: true);

                var plan = TriggerPlanFactory.When<DamageEvent>(
                    phase, priority, functionId, actions: new[] { new ActionCallPlan(actionId) });
                var validationContext = ValidationContext<DamageEvent>.CreateForDevelopment(
                    definedFunctionIds: new HashSet<string> { functionId.Value.ToString() },
                    definedActionIds: new HashSet<string> { actionId.Value.ToString() },
                    definedEventKeys: new HashSet<string> { triggerKey.StringId });
                TriggerPlanValidation.ThrowIfInvalidRuntimeBoundary(triggerKey, in plan, id: id, context: in validationContext);
                return runner.RegisterPlan(triggerKey, in plan);
            }
        }
    }
}
