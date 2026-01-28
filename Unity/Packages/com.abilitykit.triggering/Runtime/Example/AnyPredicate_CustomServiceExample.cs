using System;
using AbilityKit.Core.Eventing;
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

        public static void RunOnce()
        {
            // 这个示例演示：PredicateKind=Function 的“任意条件”如何依赖“自定义服务”做内部判断。
            // 说明：目前 TriggerRunner/ExecCtx 没有专门的 Services 字段，因此这里用最常见的方式：
            //  - 在注册 predicate 函数时通过闭包捕获 service 实例（等价于构造注入）。
            // 如果你后续把服务挂到 TriggerContext/ExecCtx 上，也可以把这里改成从 ctx.Context/ctx.xxx 取。

            var bus = new EventBus();
            var functions = new FunctionRegistry();
            var actions = new ActionRegistry();

            IImmunityService immunity = new DemoImmunityService();

            // 1) 注册任意条件函数：目标不免疫才触发
            var predicateId = new FunctionId(StableStringId.Get("pred:target_not_immune"));
            functions.Register<PlannedTrigger<Hit>.Predicate0>(
                predicateId,
                (evt, ctx) =>
                {
                    // 通过自定义服务做复杂判断（例如查 ECS/查表/查状态）
                    return !immunity.IsImmune(evt.TargetId);
                },
                isDeterministic: true);

            // 2) 注册 action
            var actionId = new ActionId(StableStringId.Get("action:print_hit"));
            actions.Register<PlannedTrigger<Hit>.Action0>(
                actionId,
                (evt, ctx) =>
                {
                    Console.WriteLine("触发成功：target=" + evt.TargetId + " damage=" + evt.Damage);
                },
                isDeterministic: true);

            var runner = new TriggerRunner(bus, functions, actions, contextSource: null, observer: null, blackboards: null, payloads: null, idNames: null, legacy: null, policy: ExecPolicy.DeterministicOnly);

            var key = new EventKey<Hit>(StableStringId.Get("event:hit"));

            var plan = new TriggerPlan<Hit>(
                phase: 0,
                priority: 0,
                predicateId: predicateId,
                actions: new[] { new ActionCallPlan(actionId) });

            runner.RegisterPlan(key, plan);

            // targetId=999 免疫 -> 不触发
            bus.Publish(key, new Hit(targetId: 999, damage: 10));
            bus.Flush();

            // targetId=1 不免疫 -> 触发
            bus.Publish(key, new Hit(targetId: 1, damage: 10));
            bus.Flush();
        }
    }
}
