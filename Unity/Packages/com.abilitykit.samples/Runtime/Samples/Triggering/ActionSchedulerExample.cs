#nullable enable

using System;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Triggering
{
    /// <summary>
    /// ActionSchedulerExample - 动作调度系统示例
    /// 演示 ActionScheduler 如何管理和执行延迟动作序列
    /// </summary>
    [Sample]
    public sealed class ActionSchedulerExample : SampleBase
    {
        public override string Title => "Action Scheduler";
        public override string Description => "演示动作队列调度、ActionInstance、ActionDelegateAdapter";
        public override SampleCategory Category => SampleCategory.Triggering;

        protected override void OnRun()
        {
            Log("=== ActionScheduler 动作调度系统示例 ===");
            Output.Divider();

            // 1. 核心概念
            Log("【1】核心概念");
            Output.Bullet("ActionScheduler - 动作调度器，管理动作队列");
            Output.Bullet("ActionInstance - 动作实例，代表一个待执行的动作");
            Output.Bullet("ActionDelegateAdapter - 委托适配器，封装动作回调");
            Output.Bullet("ActionExecutor - 动作执行器，执行具体的动作逻辑");
            Output.Bullet("ActionSchedulerManager - 调度管理器，管理多个调度器");
            Log("");

            // 2. ActionInstance 生命周期
            Log("【2】ActionInstance 生命周期");
            Output.Bullet("Created - 刚创建，未执行");
            Output.Bullet("Pending - 等待执行");
            Output.Bullet("Executing - 正在执行");
            Output.Bullet("Completed - 执行完成");
            Output.Bullet("Cancelled - 已取消");
            Output.Bullet("Failed - 执行失败");
            Log("");

            // 3. 调度模式
            Log("【3】调度模式");
            Output.Bullet("Immediate - 立即执行");
            Output.Bullet("Delayed - 延迟执行");
            Output.Bullet("Queued - 队列执行，顺序执行");
            Output.Bullet("Parallel - 并行执行，多个动作同时执行");
            Output.Bullet("Conditional - 条件执行，满足条件才执行");
            Log("");

            // 4. ActionSchedulerManager
            Log("【4】ActionSchedulerManager");
            Output.Bullet("管理多个 ActionScheduler 实例");
            Output.Bullet("支持按类别分组调度");
            Output.Bullet("提供统一的 Tick 接口驱动所有调度器");
            Output.Bullet("支持调度器的暂停和恢复");
            Log("");

            // 5. 代码示例
            Log("【5】代码示例");
            Log("");
            Log("  // 创建调度管理器");
            Log("  var manager = new ActionSchedulerManager();");
            Log("  var scheduler = manager.GetOrCreate(\"default\");");
            Log("");
            Log("  // 创建动作实例");
            Log("  var action = new ActionInstance(");
            Log("      id: 1,");
            Log("      name: \"DamageAction\",");
            Log("      callback: () => ApplyDamage(target, 100),");
            Log("      duration: 0f");
            Log("  );");
            Log("");
            Log("  // 调度动作");
            Log("  var handle = scheduler.Schedule(action, delay: 0.5f);");
            Log("");
            Log("  // 取消动作");
            Log("  handle?.Cancel();");
            Log("");

            // 6. 与 Trigger 集成
            Log("【6】与 Trigger 系统集成");
            Output.Bullet("在 Trigger 的 Execute 阶段调度动作");
            Output.Bullet("支持动作的依赖关系和执行顺序");
            Output.Bullet("提供完成回调用于后续处理");
            Log("");
            Log("  // Trigger Execute 中调度");
            Log("  public void Execute(DamageEvent args, ExecCtx ctx)");
            Log("  {");
            Log("      // 调度立即伤害");
            Log("      ctx.ActionScheduler.Schedule(new ActionInstance(");
            Log("          () => ApplyDamage(args.Target, args.Amount)");
            Log("      ));");
            Log("");
            Log("      // 调度延迟特效");
            Log("      ctx.ActionScheduler.ScheduleDelayed(new ActionInstance(");
            Log("          () => PlayEffect(args.Target, \"hit_vfx\"),");
            Log("          0.3f");
            Log("      ));");
            Log("  }");
            Log("");

            // 7. 典型使用场景
            Log("【7】典型使用场景");
            Output.Bullet("技能施法动作队列");
            Output.Bullet("连招系统的动作组合");
            Output.Bullet("表现层和逻辑层的解耦");
            Output.Bullet("延迟效果（如延迟伤害结算）");
            Output.Bullet("动作打断和取消");
            Log("");

            // 8. ActionExecutor
            Log("【8】ActionExecutor 执行器");
            Output.Bullet("IActionExecutor - 执行器接口");
            Output.Bullet("DefaultActionExecutor - 默认实现");
            Output.Bullet("支持自定义执行策略");
            Output.Bullet("提供执行前后的 Hook");
            Log("");
            Log("  // 注册自定义执行器");
            Log("  ActionExecutorRegistry.Register<MyExecutor>(\"MyAction\");");
            Log("");

            // 9. 状态管理
            Log("【9】状态管理");
            Output.Bullet("每个 ActionInstance 有独立状态");
            Output.Bullet("支持暂停/恢复单个动作");
            Output.Bullet("支持批量取消同类型动作");
            Output.Bullet("提供状态变更事件通知");
            Log("");

            // 10. API 参考
            Log("【10】关键 API 参考");
            Output.Bullet("AbilityKit.Triggering.ActionScheduler");
            Output.Bullet("AbilityKit.Triggering.ActionSchedulerManager");
            Output.Bullet("AbilityKit.Triggering.ActionScheduler.ActionInstance");
            Output.Bullet("AbilityKit.Triggering.ActionScheduler.ActionExecutor");
            Log("");

            Output.Divider();
            Log("【总结】ActionScheduler 提供动作级别的调度能力，适合实现复杂的动作序列和时间安排");
        }
    }
}
