using System;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Example
{
    public static class AnyPredicate_CustomServiceExample
    {
        public readonly struct Hit
        {
            public readonly int TargetId;
            public readonly int Damage;
            public Hit(int targetId, int damage) { TargetId = targetId; Damage = damage; }
        }

        // 代表“自定义服务”（可以来自 ECS/World/DI 容器）。
        // 这里用最小示例：根据 targetId 判断是否免疫。
        private interface IImmunityService
        {
            bool IsImmune(int targetId);
        }

        private sealed class DemoImmunityService : IImmunityService
        {
            public bool IsImmune(int targetId)
            {
                // 这里随便写一个规则：targetId=999 免疫
                return targetId == 999;
            }
        }

        private readonly struct DemoCtx
        {
            public readonly IImmunityService Services;

            public DemoCtx(IImmunityService services)
            {
                Services = services;
            }
        }

        private sealed class DemoContextSource : ITriggerContextSource<DemoCtx>
        {
            private readonly DemoCtx _ctx;

            public DemoContextSource(DemoCtx ctx)
            {
                _ctx = ctx;
            }

            public DemoCtx GetContext()
            {
                return _ctx;
            }
        }

        public static void RunOnce()
        {
            // 这个示例演示：PredicateKind=Function 的“任意条件”如何依赖“自定义服务”做内部判断。
            // 说明：泛型上下文是默认用法，服务应该放在自定义上下文里：ctx.Context.Services。

            var bus = new EventBus();
            var functions = new FunctionRegistry();
            var actions = new ActionRegistry();

            IImmunityService immunity = new DemoImmunityService();
            var contextSource = new DemoContextSource(new DemoCtx(immunity));

            // 1) 注册任意条件函数：目标不免疫才触发
            var predicateId = new FunctionId(StableStringId.Get("pred:target_not_immune"));
            functions.Register<PlannedTrigger<Hit, DemoCtx>.Predicate0>(
                predicateId,
                (evt, ctx) =>
                {
                    // 通过自定义服务做复杂判断（例如查 ECS/查表/查状态）
                    var s = ctx.Context.Services;
                    if (s == null) return false;
                    return !s.IsImmune(evt.TargetId);
                },
                isDeterministic: true);

            // 2) 注册 action
            var actionId = new ActionId(StableStringId.Get("action:print_hit"));
            actions.Register<PlannedTrigger<Hit, DemoCtx>.Action0>(
                actionId,
                (evt, ctx) =>
                {
                    Console.WriteLine("触发成功：target=" + evt.TargetId + " damage=" + evt.Damage);
                },
                isDeterministic: true);

            var runner = new TriggerRunner<DemoCtx>(bus, functions, actions, contextSource: contextSource, observer: null, blackboards: null, payloads: null, idNames: null, policy: ExecPolicy.DeterministicOnly);

            var key = new EventKey<Hit>(StableStringId.Get("event:hit"));

            var plan = new TriggerPlan<Hit>(
                phase: 0,
                priority: 0,
                triggerId: 0,
                predicateId: predicateId,
                predicateArgs: null,
                actions: new[] { new ActionCallPlan(actionId) },
                interruptPriority: 0,
                cue: null,
                schedule: default);

            runner.RegisterPlan<Hit, DemoCtx>(key, plan);

            // targetId=999 免疫 -> 不触发
            bus.Publish(key, new Hit(targetId: 999, damage: 10));
            bus.Flush();

            // targetId=1 不免疫 -> 触发
            bus.Publish(key, new Hit(targetId: 1, damage: 10));
            bus.Flush();
        }
    }
}
