#nullable enable

using System;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Triggering
{
    /// <summary>
    /// SchedulerExample - 调度系统示例
    /// 演示 Triggering 模块中的 Scheduler 调度机制
    /// </summary>
    [Sample]
    public sealed class SchedulerExample : SampleBase
    {
        public override string Title => "Scheduler System";
        public override string Description => "演示调度器、调度策略、延迟执行等核心概念";
        public override SampleCategory Category => SampleCategory.Triggering;

        protected override void OnRun()
        {
            Log("=== Scheduler 调度系统示例 ===");
            Output.Divider();

            // 1. 核心概念
            Log("【1】核心概念");
            Output.Bullet("Scheduler - 时间调度器，支持延迟执行和周期执行");
            Output.Bullet("IScheduler - 调度器接口，定义调度行为");
            Output.Bullet("ScheduleData - 调度数据，包含执行参数");
            Output.Bullet("IScheduleManager - 调度管理器，管理多个调度器");
            Log("");

            // 2. 调度类型
            Log("【2】调度类型");
            Output.Bullet("Transient - 瞬时调度，立即执行一次");
            Output.Bullet("Delayed - 延迟调度，指定时间后执行");
            Output.Bullet("Periodic - 周期调度，固定间隔重复执行");
            Output.Bullet("Scheduled - 定时调度，在指定时刻执行");
            Log("");

            // 3. 调度策略
            Log("【3】调度策略 (IScheduleStrategy)");
            Output.Bullet("ImmediateStrategy - 立即执行策略");
            Output.Bullet("QueuedStrategy - 队列策略，延迟到下一帧");
            Output.Bullet("PrioritizedStrategy - 优先级策略，按优先级排序");
            Output.Bullet("BatchedStrategy - 批处理策略，合并多个调度请求");
            Log("");

            // 4. ScheduleHandle
            Log("【4】ScheduleHandle - 调度句柄");
            Output.Bullet("用于跟踪和管理调度任务");
            Output.Bullet("支持取消 (Cancel)、暂停 (Pause)、恢复 (Resume)");
            Output.Bullet("提供状态查询 (IsValid, IsPaused)");
            Log("");

            // 5. 配置示例
            Log("【5】ScheduleConfig 配置结构");
            Output.Bullet("Type - 调度类型 (Transient/Delayed/Periodic)");
            Output.Bullet("Delay - 延迟时间（秒）");
            Output.Bullet("Interval - 周期间隔（秒）");
            Output.Bullet("RepeatCount - 重复次数 (-1 表示无限)");
            Output.Bullet("StartTime - 开始时间（游戏内时间）");
            Output.Bullet("Priority - 优先级");
            Log("");

            // 6. 代码示例
            Log("【6】代码示例");
            Log("");
            Log("  // 创建调度管理器");
            Log("  var manager = new SimpleScheduleManager();");
            Log("");
            Log("  // 延迟执行 - 2秒后执行");
            Log("  var handle1 = manager.ScheduleDelayed(");
            Log("      action: () => Log(\"Delayed action executed\"),");
            Log("      delay: 2.0f");
            Log("  );");
            Log("");
            Log("  // 周期执行 - 每1秒执行一次，共5次");
            Log("  var handle2 = manager.SchedulePeriodic(");
            Log("      action: () => Log(\"Periodic tick\"),");
            Log("      interval: 1.0f,");
            Log("      repeatCount: 5");
            Log("  );");
            Log("");
            Log("  // 取消调度");
            Log("  handle1.Cancel();");
            Log("");

            // 7. 与 Trigger 系统集成
            Log("【7】与 Trigger 系统集成");
            Output.Bullet("在 TriggerPlanConfig 中配置 Schedule");
            Output.Bullet("支持在指定阶段执行调度");
            Output.Bullet("调度完成后触发回调 (OnScheduledComplete)");
            Log("");
            Log("  // TriggerPlan 配置调度");
            Log("  var plan = TriggerPlanConfig.Create(1, eventId)");
            Log("      .WithSchedule(ScheduleConfig.Delayed(2.0f));");
            Log("");

            // 8. 使用场景
            Log("【8】典型使用场景");
            Output.Bullet("DOT (Damage Over Time) - 持续伤害");
            Output.Bullet("HOT (Heal Over Time) - 持续治疗");
            Output.Bullet("Buff/Debuff 持续效果");
            Output.Bullet("技能冷却管理");
            Output.Bullet("定时器事件");
            Output.Bullet("延迟伤害结算");
            Log("");

            // 9. API 参考
            Log("【9】关键 API 参考");
            Output.Bullet("AbilityKit.Triggering.Scheduler");
            Output.Bullet("AbilityKit.Triggering.Schedule");
            Output.Bullet("AbilityKit.Triggering.Schedule.Data");
            Log("");

            // 10. 最佳实践
            Log("【10】最佳实践");
            Output.Bullet("及时取消不需要的调度任务避免内存泄漏");
            Output.Bullet("使用 Handle 管理调度生命周期");
            Output.Bullet("周期调度注意设置合理的 repeatCount");
            Output.Bullet("在 World 销毁时清理所有调度");
            Log("");

            Output.Divider();
            Log("【总结】Scheduler 提供强大的时间调度能力，是实现延迟效果、周期效果的核心组件");
        }
    }
}
