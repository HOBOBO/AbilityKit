#nullable enable

using System;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Triggering
{
    /// <summary>
    /// BehaviorTreeExample - 行为树示例
    /// 演示 Triggering 模块中的行为树组合模式
    /// </summary>
    [Sample]
    public sealed class BehaviorTreeExample : SampleBase
    {
        public override string Title => "Behavior Tree";
        public override string Description => "演示行为树组合、Composite、Decorator、Selector、Sequence";
        public override SampleCategory Category => SampleCategory.Triggering;

        protected override void OnRun()
        {
            Log("=== BehaviorTree 行为树示例 ===");
            Output.Divider();

            // 1. 核心概念
            Log("【1】核心概念");
            Output.Bullet("Behavior - 行为节点，执行具体动作");
            Output.Bullet("Composite - 组合节点，包含子节点");
            Output.Bullet("Decorator - 装饰节点，修改子节点行为");
            Output.Bullet("Selector - 选择节点，返回第一个成功的子节点");
            Output.Bullet("Sequence - 序列节点，所有子节点都成功才成功");
            Output.Bullet("Parallel - 并行节点，同时执行所有子节点");
            Log("");

            // 2. 节点类型
            Log("【2】节点类型详解");
            Log("");
            Log("  Composite (组合节点):");
            Log("    - Sequence: 从左到右执行，失败即停");
            Log("    - Selector: 从左到右执行，成功即停");
            Log("    - Parallel: 同时执行所有子节点");
            Log("    - RandomSequence/RandomSelector: 随机顺序");
            Log("");
            Log("  Decorator (装饰节点):");
            Log("    - Repeater: 重复执行子节点N次");
            Log("    - RepeatUntilFail: 重复直到失败");
            Log("    - Inverter: 反转结果");
            Log("    - Condition: 条件检查");
            Log("    - TimeLimit: 时间限制");
            Log("");

            // 3. 执行结果
            Log("【3】执行结果 (BehaviorExecutionResult)");
            Output.Bullet("Success - 执行成功");
            Output.Bullet("Failure - 执行失败");
            Output.Bullet("Running - 正在执行（需要下一帧继续）");
            Log("");

            // 4. 在 Trigger 系统中的使用
            Log("【4】在 Trigger 系统中的使用");
            Output.Bullet("TriggerBehavior - 触发器行为基类");
            Output.Bullet("ActionBehavior - 动作行为");
            Output.Bullet("PredicateBehavior - 条件行为");
            Output.Bullet("CompositeBehavior - 组合行为");
            Log("");

            // 5. 代码示例 - 构建行为树
            Log("【5】代码示例 - 构建行为树");
            Log("");
            Log("  // 创建根选择器");
            Log("  var root = new SelectorBehavior(\"Root\");");
            Log("");
            Log("  // 添加攻击序列");
            Log("  var attackSeq = new SequenceBehavior(\"AttackSequence\");");
            Log("  attackSeq.AddChild(new ConditionBehavior(\"InRange\", CheckInRange));");
            Log("  attackSeq.AddChild(new ActionBehavior(\"MeleeAttack\", ExecuteMeleeAttack));");
            Log("  attackSeq.AddChild(new ActionBehavior(\"PlayVFX\", PlayAttackVFX));");
            Log("  root.AddChild(attackSeq);");
            Log("");
            Log("  // 添加移动序列");
            Log("  var moveSeq = new SequenceBehavior(\"MoveSequence\");");
            Log("  moveSeq.AddChild(new ConditionBehavior(\"HasTarget\", CheckHasTarget));");
            Log("  moveSeq.AddChild(new ActionBehavior(\"MoveToTarget\", MoveToTarget));");
            Log("  root.AddChild(moveSeq);");
            Log("");
            Log("  // 添加待机行为");
            Log("  root.AddChild(new ActionBehavior(\"Idle\", ExecuteIdle));");
            Log("");

            // 6. 与 Trigger 集成
            Log("【6】Trigger 行为树集成");
            Log("");
            Log("  // 在 Trigger 中使用行为树");
            Log("  var trigger = new TriggerBehavior(");
            Log("      predicate: new SequenceBehavior(");
            Log("      {");
            Log("          new FunctionPredicate(\"CheckHealth\", () => hp > 30),");
            Log("          new FunctionPredicate(\"CheckMana\", () => mana >= 50)");
            Log("      }),");
            Log("      action: new SequenceBehavior(");
            Log("      {");
            Log("          new ActionCall(\"ConsumeMana\", ConsumeMana),");
            Log("          new ActionCall(\"CastSpell\", CastSpell),");
            Log("          new ActionCall(\"PlayEffect\", PlayEffect)");
            Log("      })");
            Log("  );");
            Log("");

            // 7. 典型使用场景
            Log("【7】典型使用场景");
            Output.Bullet("AI 决策树 - 敌人行为逻辑");
            Output.Bullet("技能施法流程 - 前摇/施法/后摇");
            Output.Bullet("Boss 技能组合 - 随机技能选择");
            Output.Bullet("NPC 行为 - 巡逻/追击/攻击/逃跑");
            Output.Bullet("复杂条件判断 - 多条件组合");
            Log("");

            // 8. 行为树配置化
            Log("【8】行为树配置化 (JSON)");
            Output.Bullet("支持从 JSON 加载行为树定义");
            Output.Bullet("节点类型映射到具体实现");
            Output.Bullet("支持参数化节点配置");
            Output.Bullet("支持运行时动态修改");
            Log("");
            Log("  {");
            Log("    \"type\": \"Selector\",");
            Log("    \"children\": [");
            Log("      { \"type\": \"Sequence\", \"children\": [...] },");
            Log("      { \"type\": \"Action\", \"name\": \"Idle\" }");
            Log("    ]");
            Log("  }");
            Log("");

            // 9. API 参考
            Log("【9】关键 API 参考");
            Output.Bullet("AbilityKit.Triggering.Behavior");
            Output.Bullet("AbilityKit.Triggering.Behavior.Composite");
            Output.Bullet("AbilityKit.Triggering.Behavior.Actions");
            Output.Bullet("AbilityKit.Triggering.Behavior.Predicates");
            Output.Bullet("AbilityKit.Triggering.Behavior.Decorators");
            Log("");

            // 10. 最佳实践
            Log("【10】最佳实践");
            Output.Bullet("保持行为树简洁，深度不超过 5-6 层");
            Output.Bullet("频繁使用的子树抽离为独立行为");
            Output.Bullet("条件检查尽量简单，避免复杂计算");
            Output.Bullet("使用描述性名称命名节点");
            Output.Bullet("为 Running 状态设计合理的退出条件");
            Log("");

            Output.Divider();
            Log("【总结】BehaviorTree 提供结构化的行为组合方式，适合复杂 AI 和流程控制场景");
        }
    }
}
