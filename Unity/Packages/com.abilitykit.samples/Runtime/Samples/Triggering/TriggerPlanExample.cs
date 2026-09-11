#nullable enable

using System;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Triggering
{
    /// <summary>
    /// TriggerPlanExample - 触发器计划示例
    /// 演示触发器系统的核心概念和配置方式
    /// </summary>
    [Sample]
    public sealed class TriggerPlanExample : SampleBase
    {
        public override string Title => "Trigger Plan";
        public override string Description => "演示 TriggerPlan 配置、Predicate、Action 和值引用";
        public override SampleCategory Category => SampleCategory.Triggering;

        protected override void OnRun()
        {
            Log("=== 触发器计划示例 ===");
            Output.Divider();

            // 1. 核心概念
            Log("【1】核心概念");
            Output.Bullet("Trigger - 触发器，包含条件评估和执行逻辑");
            Output.Bullet("TriggerPlan - 触发器计划，包含配置数据");
            Output.Bullet("Predicate - 条件，决定触发器是否执行");
            Output.Bullet("Action - 行为，触发器执行时的具体操作");
            Output.Bullet("EventKey - 事件键，关联事件和触发器");
            Log("");

            // 2. TriggerPlan 配置结构
            Log("【2】TriggerPlan 配置结构");
            Output.Bullet("TriggerId - 触发器唯一标识");
            Output.Bullet("EventKey - 关联的事件键");
            Output.Bullet("Phase - 阶段，用于分组执行");
            Output.Bullet("Priority - 优先级，同 Phase 内按优先级执行");
            Output.Bullet("Predicate - 条件表达式");
            Output.Bullet("Actions - 行为列表");
            Output.Bullet("Schedule - 调度配置");
            Output.Bullet("Cue - 表现层配置 (VFX/SFX)");
            Log("");

            // 3. Predicate (条件) 类型
            Log("【3】Predicate 条件类型");
            Output.Bullet("FunctionPredicate - 函数条件，调用注册表中的函数");
            Output.Bullet("ExpressionPredicate - 表达式条件，RPN 布尔表达式");
            Output.Bullet("DistanceCheckPredicate - 距离检查条件");
            Output.Bullet("HealthCheckPredicate - 生命值检查条件");
            Output.Bullet("BlackboardPredicate - 黑板条件，检查黑板值");
            Log("");

            // 4. Action (行为) 类型
            Log("【4】Action 行为类型");
            Output.Bullet("ActionCall - 单个 Action 调用");
            Output.Bullet("Sequence - 顺序执行多个 Action");
            Output.Bullet("Selector - 选择执行第一个成功的");
            Output.Bullet("Parallel - 并行执行多个 Action");
            Log("");

            // 5. ValueRef (值引用) 类型
            Log("【5】ValueRef 值引用类型");
            Output.Bullet("Const - 常量数值");
            Output.Bullet("Blackboard - 黑板变量");
            Output.Bullet("PayloadField - 事件载荷字段");
            Output.Bullet("Var - 域变量");
            Output.Bullet("Expr - 表达式");
            Log("");

            // 6. 配置示例
            Log("【6】配置示例");
            Log("");

            // 示例 1: 伤害触发器配置
            Log("  --- 伤害触发器 ---");
            Log("  TriggerId: 1001");
            Log("  EventKey: \"event.damage\"");
            Log("  Phase: 0, Priority: 100");
            Log("  Predicate: payload.amount > 40");
            Log("  Actions:");
            Log("    1. action:print_damage(payload.amount)");
            Log("    2. action:increment_combo()");
            Log("");

            // 示例 2: 技能触发器配置
            Log("  --- 技能触发器 ---");
            Log("  TriggerId: 2001");
            Log("  EventKey: \"event.skill.cast\"");
            Log("  Phase: 1, Priority: 50");
            Log("  Predicate: skill.level > 0 AND caster.mp >= skill.cost");
            Log("  Actions:");
            Log("    1. action:consume_mp(skill.cost)");
            Log("    2. action:spawn_effect(skill.effect_id)");
            Log("    3. action:apply_damage(target, skill.damage)");
            Log("");

            // 7. 表达式语法
            Log("【7】表达式语法 (RPN 逆波兰表达式)");
            Log("  RPN 示例:");
            Log("    payload.amount 40 >       => payload.amount > 40");
            Log("    A B AND                 => A AND B");
            Log("    A B OR                  => A OR B");
            Log("    A NOT                   => NOT A");
            Log("    (payload.amount 40 >) (caster.mp 30 >=) AND  => (payload.amount > 40) AND (caster.mp >= 30)");
            Log("");

            // 8. 黑板使用
            Log("【8】Blackboard 黑板系统");
            Log("  Blackboard 用于存储运行时状态");
            Log("  支持类型: Int, Float, Double, Bool, Object");
            Log("  使用 StableStringId 作为键避免字符串比较开销");
            Log("");
            Log("  示例黑板键:");
            Log("    bb:combat:atk      - 攻击力");
            Log("    bb:combat:def      - 防御力");
            Log("    bb:combat:combo    - 连击数");
            Log("    bb:player:health   - 生命值");
            Log("    bb:player:mana      - 魔法值");
            Log("");

            // 9. 执行流程
            Log("【9】触发器执行流程");
            Log("  1. 事件派发 (EventBus.Publish)");
            Log("  2. 查找关联的触发器");
            Log("  3. 按 Phase 分组");
            Log("  4. 同 Phase 内按 Priority 排序");
            Log("  5. 遍历触发器:");
            Log("     a) Evaluate() - 评估条件");
            Log("     b) 条件通过 -> Execute() - 执行行为");
            Log("     c) 检查打断控制");
            Log("  6. Cue 回调 (表现层)");
            Log("");

            // 10. API 参考
            Log("【10】关键 API 参考");
            Output.Bullet("AbilityKit.Triggering.Eventing - 事件系统");
            Output.Bullet("AbilityKit.Triggering.Blackboard - 黑板系统");
            Output.Bullet("AbilityKit.Triggering.Registry - 注册表 (Function/Action)");
            Output.Bullet("AbilityKit.Triggering.Payload - 载荷访问器");
            Output.Bullet("AbilityKit.Triggering.Runtime.Config - 配置类");
            Output.Bullet("AbilityKit.Triggering.Variables - 值引用和表达式");

            Output.Divider();
            Log("【11】总结");
            Output.Bullet("TriggerPlan = 条件 (Predicate) + 行为 (Actions)");
            Output.Bullet("TriggerRunner = 事件驱动引擎，按 Phase/Priority 执行");
            Output.Bullet("Blackboard = 跨触发器共享状态");
            Output.Bullet("ValueRef = 解耦配置和具体值");
            Output.Bullet("PayloadAccessor = 事件数据的强类型访问");
        }
    }
}
