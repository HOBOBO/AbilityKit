using System;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Example
{
    public static class AnyPredicate_BlackboardExample
    {
        public readonly struct Damage
        {
            public readonly int Amount;
            public Damage(int amount) { Amount = amount; }
        }

        public static void RunOnce()
        {
            // 这个示例演示：PredicateKind=Function 的“任意条件”如何从 ctx.Blackboards 取数据做内部判断。
            // 适用场景：条件不是简单 Compare 能表达的（比如多 key 组合、阈值表、特殊规则）。

            var bus = new EventBus();
            var functions = new FunctionRegistry();
            var actions = new ActionRegistry();

            // 1) 准备黑板
            var blackboards = new DictionaryBlackboardResolver();
            var bb = new DictionaryBlackboard();
            var boardId = StableStringId.Get("bb:combat");
            blackboards.Register(boardId, bb);

            var shieldKey = StableStringId.Get("bb:combat:shield");
            bb.SetInt(shieldKey, 10);

            // 2) 注册任意条件函数：只有当 (damage.amount - shield) > 0 才触发
            var predicateId = new FunctionId(StableStringId.Get("pred:damage_after_shield_positive"));
            functions.Register<PlannedTrigger<Damage, TriggerContext>.Predicate0>(
                predicateId,
                (evt, ctx) =>
                {
                    if (ctx.Blackboards == null) return false;
                    if (!ctx.Blackboards.TryResolve(boardId, out var b)) return false;
                    if (!b.TryGetInt(shieldKey, out var shield)) shield = 0;

                    var remain = evt.Amount - shield;
                    return remain > 0;
                },
                isDeterministic: true);

            // 3) 注册 action
            var actionId = new ActionId(StableStringId.Get("action:print_damage_after_shield"));
            actions.Register<PlannedTrigger<Damage, TriggerContext>.Action0>(
                actionId,
                (evt, ctx) =>
                {
                    ctx.Blackboards.TryResolve(boardId, out var b);
                    var shield = 0;
                    b?.TryGetInt(shieldKey, out shield);
                    Console.WriteLine("触发成功：damage=" + evt.Amount + " shield=" + shield);
                },
                isDeterministic: true);

            var runner = new TriggerRunner<TriggerContext>(bus, functions, actions, contextSource: null, observer: null, blackboards: blackboards, payloads: null, idNames: null, policy: ExecPolicy.DeterministicOnly);

            var key = new EventKey<Damage>(StableStringId.Get("event:damage"));

            var plan = new TriggerPlan<Damage>(
                phase: 0,
                priority: 0,
                predicateId: predicateId,
                actions: new[] { new ActionCallPlan(actionId) });

            runner.RegisterPlan<Damage, TriggerContext>(key, plan);

            // shield=10：amount=5 -> 不触发
            bus.Publish(key, new Damage(5));
            bus.Flush();

            // shield=10：amount=15 -> 触发
            bus.Publish(key, new Damage(15));
            bus.Flush();
        }
    }
}
