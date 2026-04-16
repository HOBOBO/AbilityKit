using System;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Example
{
    /// <summary>
    /// DSL 流畅 API 使用示例
    /// 展示如何使用 TriggerPlanDsl、PredicateExprDsl、NumericValueRefDsl
    /// </summary>
    public static class TriggerPlanDslExample
    {
        public readonly struct DamageEvent
        {
            public readonly int Amount;
            public readonly int TargetId;
            public readonly bool IsCritical;

            public DamageEvent(int amount, int targetId, bool isCritical = false)
            {
                Amount = amount;
                TargetId = targetId;
                IsCritical = isCritical;
            }
        }

        private sealed class DamageEventPayloadAccessor : IPayloadIntAccessor<DamageEvent>
        {
            public bool TryGet(in DamageEvent args, int fieldId, out int value)
            {
                if (fieldId == StableStringId.Get("payload:amount"))
                {
                    value = args.Amount;
                    return true;
                }
                if (fieldId == StableStringId.Get("payload:targetId"))
                {
                    value = args.TargetId;
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
            var combatBoardId = StableStringId.Get("bb:combat");
            blackboards.Register(combatBoardId, bbCombat);

            var atkKeyId = StableStringId.Get("bb:combat:atk");
            var hpKeyId = StableStringId.Get("bb:combat:hp");
            bbCombat.SetDouble(atkKeyId, 7d);
            bbCombat.SetDouble(hpKeyId, 100d);

            var payloads = new PayloadAccessorRegistry();
            payloads.RegisterIntAccessor(new DamageEventPayloadAccessor());

            var runner = new TriggerRunner<TriggerContext>(bus, functions, actions, blackboards: blackboards, payloads: payloads);

            // ========== 使用 DSL 定义 Action IDs ==========
            var actionPrintDamage = new ActionId(StableStringId.Get("action:print_damage"));
            var actionShowEffect = new ActionId(StableStringId.Get("action:show_effect"));
            var actionPlaySound = new ActionId(StableStringId.Get("action:play_sound"));

            actions.Register<PlannedTrigger<DamageEvent, TriggerContext>.Action1>(
                actionPrintDamage,
                (evt, arg0, ctx) => Console.WriteLine($"动作: 伤害值={evt.Amount}, 参数={arg0}"),
                isDeterministic: true);

            actions.Register<PlannedTrigger<DamageEvent, TriggerContext>.Action0>(
                actionShowEffect,
                (evt, ctx) => Console.WriteLine($"显示受伤特效: target={evt.TargetId}"),
                isDeterministic: true);

            actions.Register<PlannedTrigger<DamageEvent, TriggerContext>.Action0>(
                actionPlaySound,
                (evt, ctx) => Console.WriteLine("播放音效"),
                isDeterministic: true);

            var eventKey = new EventKey<DamageEvent>(StableStringId.Get("event:damage"));

            // ========== 方式一：使用传统方式（对照） ==========
            Console.WriteLine("=== 传统方式 ===");
            var traditionalPredicateExpr = new PredicateExprPlan(new[]
            {
                BoolExprNode.Compare(ECompareOp.GreaterThan,
                    NumericValueRef.PayloadField(StableStringId.Get("payload:amount")),
                    NumericValueRef.Const(3d)),
                BoolExprNode.Compare(ECompareOp.GreaterThanOrEqual,
                    NumericValueRef.Blackboard(combatBoardId, atkKeyId),
                    NumericValueRef.Const(7d)),
                BoolExprNode.And(),
            });

            var traditionalPlan = new TriggerPlan<DamageEvent>(
                phase: 0,
                priority: 0,
                triggerId: 0,
                predicateExpr: traditionalPredicateExpr,
                actions: new[]
                {
                    new ActionCallPlan(actionPrintDamage, NumericValueRef.Blackboard(combatBoardId, atkKeyId))
                },
                interruptPriority: 0);

            runner.RegisterPlan<DamageEvent, TriggerContext>(eventKey, traditionalPlan);

            // ========== 方式二：使用 DSL 流畅 API ==========
            Console.WriteLine("\n=== DSL 方式 ===");

            // 使用 NumericValueRefDsl 创建数值引用
            var amountPayload = NumericValueRefDsl.Payload("amount");
            var atkBlackboard = NumericValueRefDsl.Blackboard(combatBoardId, atkKeyId);
            var hpBlackboard = NumericValueRefDsl.Blackboard(combatBoardId, hpKeyId);

            // 使用 PredicateExprDsl 构建布尔条件
            var dslPredicate = PredicateExprDsl
                .Gt(amountPayload, NumericValueRefDsl.Const(10))
                .And()
                .Ge(atkBlackboard, NumericValueRefDsl.Const(5))
                .Build();

            // 使用 TriggerPlanDsl 构建触发器计划
            var dslPlan = TriggerPlanDsl.Create<DamageEvent>(phase: 0, priority: 100)
                .When(dslPredicate)
                .Do(actionShowEffect)
                .DoConst(actionPlaySound, 1.0)
                .Build();

            var dslEventKey = new EventKey<DamageEvent>(StableStringId.Get("event:damage_dsl"));
            runner.RegisterPlan<DamageEvent, TriggerContext>(dslEventKey, dslPlan);

            // ========== 方式三：更简洁的 DSL 用法 ==========
            Console.WriteLine("\n=== 更简洁的 DSL 用法 ===");

            // 链式比较
            var simpleCondition = PredicateExprDsl
                .Gt(NumericValueRefDsl.Payload("amount"), NumericValueRefDsl.Const(0))
                .Build();

            var simplePlan = TriggerPlanDsl.Create<DamageEvent>()
                .When(simpleCondition)
                .Do(actionPrintDamage, NumericValueRefDsl.Const(100))
                .Build();

            // ========== 方式四：复杂条件示例 ==========
            Console.WriteLine("\n=== 复杂条件示例 ===");

            // (amount > 10 AND atk >= 5) OR hp < 20
            var complexCondition = PredicateExprDsl
                .Gt(amountPayload, NumericValueRefDsl.Const(10))
                .And()
                .Ge(atkBlackboard, NumericValueRefDsl.Const(5))
                .Or()
                .Lt(hpBlackboard, NumericValueRefDsl.Const(20))
                .Build();

            var complexPlan = TriggerPlanDsl.Create<DamageEvent>(priority: 200)
                .When(complexCondition)
                .Do(actionShowEffect)
                .Do(actionPlaySound)
                .Build();

            // ========== 方式五：组合多个动作 ==========
            Console.WriteLine("\n=== 组合多个动作 ===");

            var multiActionPlan = TriggerPlanDsl.Create<DamageEvent>(priority: 50)
                .DoAll(
                    ActionCallPlanDsl.Call(actionShowEffect),
                    ActionCallPlanDsl.Call(actionPlaySound),
                    ActionCallPlanDsl.CallConst(actionPrintDamage, 999))
                .Build();

            // ========== 触发测试 ==========
            Console.WriteLine("\n=== 触发测试 ===");
            bus.Publish(eventKey, new DamageEvent(amount: 50, targetId: 1001));
            bus.Flush();
        }
    }
}
