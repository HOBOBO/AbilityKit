#nullable enable

using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Triggering
{
    /// <summary>
    /// 说明扁平计划如何选装默认调度器；示例包本身不依赖调度扩展。
    /// </summary>
    [Sample]
    public sealed class ActionSchedulerExample : SampleBase
    {
        /// <summary>
        /// 示例列表中的标题。
        /// </summary>
        public override string Title => "Action Scheduler";

        /// <summary>
        /// 说明本示例只展示宿主装配步骤，不实际驱动调度器。
        /// </summary>
        public override string Description => "说明可选扁平动作调度的装配、推进与清理";

        /// <summary>
        /// 将示例归入触发器分类。
        /// </summary>
        public override SampleCategory Category => SampleCategory.Triggering;

        protected override void OnRun()
        {
            Log("=== 可选扁平动作调度 ===");
            Output.Bullet("即时规则不需要调度。延迟等扁平计划动作由宿主注入 IFlatActionScheduler。");
            Output.Bullet("使用默认实现时选装 com.abilitykit.triggering.actionscheduling，并引用 AbilityKit.Triggering.ActionScheduling。");
            Log("  var scheduler = new ActionSchedulerManager();");
            Log("  var runner = new TriggerRunner<MyContext>(bus, functions, actions, flatActionScheduler: scheduler);");
            Output.Bullet("注册带 Delayed 动作的计划后，发布事件只登记待执行动作；由宿主在更新阶段推进：");
            Log("  scheduler.Update(deltaMs, null);");
            Output.Bullet("结束规则时先释放订阅，再用 RemoveScheduler(triggerId) 清理该分组；宿主结束时可调用 Clear()。");
            Output.Bullet("动作实例和执行器是内部实现；Queued 不提供动作队列。自有调度的项目无需选装默认实现。");
        }
    }
}
