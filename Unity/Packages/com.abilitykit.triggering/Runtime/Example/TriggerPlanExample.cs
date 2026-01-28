using System;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Example
{
    public static class TriggerPlanExample
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
            var bus = new EventBus();
            var functions = new FunctionRegistry();
            var actions = new ActionRegistry();

            var blackboards = new DictionaryBlackboardResolver();
            var bbCombat = new DictionaryBlackboard();
            var combatBoardId = Eventing.StableStringId.Get("bb:combat");
            blackboards.Register(combatBoardId, bbCombat);

            var atkKeyId = Eventing.StableStringId.Get("bb:combat:atk");
            bbCombat.SetInt(atkKeyId, 7);

            var payloads = new PayloadAccessorRegistry();
            payloads.RegisterIntAccessor(new DamageEventPayloadAccessor());

            var runner = new TriggerRunner<TriggerContext>(bus, functions, actions, blackboards: blackboards, payloads: payloads);

            // 注册 action：这里演示“复合行为”，也就是一次触发会顺序执行多条 action。
            // 注意：签名必须匹配 PlannedTrigger<DamageEvent>.ActionN（N=0/1/2）

            // action1：打印 payload.amount 和来自黑板的 arg0
            var actionPrintDamage = new ActionId(Eventing.StableStringId.Get("action:print_damage"));
            actions.Register<PlannedTrigger<DamageEvent, TriggerContext>.Action1>(
                actionPrintDamage,
                (evt, arg0, ctx) =>
                {
                    Console.WriteLine("动作1：payload.amount=" + evt.Amount + " arg0(atk)=" + arg0);
                },
                isDeterministic: true);

            // action2：同时打印两个参数（arg0=payload.amount, arg1=bb.combat.atk）
            var actionPrint2 = new ActionId(Eventing.StableStringId.Get("action:print_2"));
            actions.Register<PlannedTrigger<DamageEvent, TriggerContext>.Action2>(
                actionPrint2,
                (evt, arg0, arg1, ctx) =>
                {
                    Console.WriteLine("动作2：arg0(amount)=" + arg0 + " arg1(atk)=" + arg1);
                },
                isDeterministic: true);

            var eventKey = new EventKey<DamageEvent>(Eventing.StableStringId.Get("event:damage"));

            // 复合条件（表达式节点是 RPN 逆波兰形式）：
            // ((payload.amount > 3) AND (bb.combat.atk >= 7)) OR NOT(payload.amount == 4)
            // RPN 写法：
            //   A B AND C NOT OR
            //   其中：
            //   A = payload.amount > 3
            //   B = bb.combat.atk >= 7
            //   C = payload.amount == 4
            var predicateExpr = new PredicateExprPlan(new[]
            {
                // A
                BoolExprNode.Compare(ECompareOp.Gt,
                    IntValueRef.PayloadField(Eventing.StableStringId.Get("payload:amount")),
                    IntValueRef.Const(3)),

                // B
                BoolExprNode.Compare(ECompareOp.Ge,
                    IntValueRef.Blackboard(combatBoardId, atkKeyId),
                    IntValueRef.Const(7)),

                // AND
                BoolExprNode.And(),

                // C
                BoolExprNode.Compare(ECompareOp.Eq,
                    IntValueRef.PayloadField(Eventing.StableStringId.Get("payload:amount")),
                    IntValueRef.Const(4)),

                // NOT
                BoolExprNode.Not(),

                // OR
                BoolExprNode.Or(),
            });

            // 复合行为：同一个 plan 里放多条 action，按数组顺序执行。
            var plan = new TriggerPlan<DamageEvent>(
                phase: 0,
                priority: 0,
                predicateExpr: predicateExpr,
                actions: new[]
                {
                    // action1(arg0=bb.combat.atk)
                    new ActionCallPlan(actionPrintDamage, IntValueRef.Blackboard(combatBoardId, atkKeyId)),

                    // action2(arg0=payload.amount, arg1=bb.combat.atk)
                    new ActionCallPlan(actionPrint2,
                        IntValueRef.PayloadField(Eventing.StableStringId.Get("payload:amount")),
                        IntValueRef.Blackboard(combatBoardId, atkKeyId))
                });

            runner.RegisterPlan<DamageEvent, TriggerContext>(eventKey, plan);

            // 触发事件：这里 amount=5
            // 按上面的复合条件：
            //   (5>3 && atk>=7) 为 true，因此会触发；同时 NOT(5==4) 也为 true。
            bus.Publish(eventKey, new DamageEvent(amount: 5));
            bus.Flush();
        }
    }
}
