using System;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Example
{
    public static class TriggeringExample
    {
        public readonly struct DamageEvent
        {
            public readonly int Amount;
            public DamageEvent(int amount) { Amount = amount; }
        }

        private sealed class DamageEventPayloadAccessor : IPayloadIntAccessor<DamageEvent>
        {
            public bool TryGet(in DamageEvent args, int fieldId, out int value)
            {
                // 这里把 "payload:amount" 映射到 DamageEvent.Amount。
                // 实际项目里一般会通过 codegen 生成 switch(fieldId) 来做高性能映射。
                if (fieldId == Eventing.StableStringId.Get("payload:amount"))
                {
                    value = args.Amount;
                    return true;
                }

                value = default;
                return false;
            }
        }

        public static void RunOnce()
        {
            // 1) 事件总线
            var bus = new EventBus();

            // 2) 注册表（函数/动作）
            var functions = new FunctionRegistry();
            var actions = new ActionRegistry();

            // 3) 黑板
            var blackboards = new DictionaryBlackboardResolver();
            var bbCombat = new DictionaryBlackboard();
            var boardId = Eventing.StableStringId.Get("bb:combat");
            blackboards.Register(boardId, bbCombat);

            var atkKeyId = Eventing.StableStringId.Get("bb:combat:atk");
            bbCombat.SetDouble(atkKeyId, 7d);

            // 4) Payload 访问器
            var payloads = new PayloadAccessorRegistry();
            payloads.RegisterIntAccessor(new DamageEventPayloadAccessor());

            // 5) TriggerRunner（负责订阅事件并按 phase/priority 触发触发器）
            var runner = new TriggerRunner<TriggerContext>(bus, functions, actions, contextSource: null, observer: null, blackboards: blackboards, payloads: payloads);

            // 6) 构建一个使用 RPN 的表达式：(payload.amount + bb.combat.atk)
            var expr = new RpnNumericExprRuntime(new RpnNumericExprPlan(RpnNumericExprParser.LangRpnV1, "payload:amount bb:combat:atk +"));

            var key = new EventKey<DamageEvent>(Eventing.StableStringId.Get("event:damage"));

            var trigger = new DelegateTrigger<DamageEvent, TriggerContext>(
                predicate: (evt, ctx) => expr.Eval(in evt, in ctx) >= 10,
                actions: (evt, ctx) =>
                {
                    var v = expr.Eval(in evt, in ctx);
                    Console.WriteLine("Trigger fired. expr=" + v);
                });

            runner.Register(key, trigger, phase: 0, priority: 0);

            // 7) 派发事件
            bus.Publish(key, new DamageEvent(amount: 5));

            // EventBus 默认可能是 queued（取决于 options）；Flush 确保完成派发。
            bus.Flush();
        }
    }
}
