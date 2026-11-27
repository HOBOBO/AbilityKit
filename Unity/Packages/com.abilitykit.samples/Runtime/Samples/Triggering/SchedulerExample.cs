#nullable enable

using System;
using AbilityKit.Core.Eventing;
using AbilityKit.Samples.Abstractions;
using AbilityKit.Timer;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Samples.Logic.Samples.Triggering
{
    /// <summary>
    /// 演示宿主定时器到期后发布事件，由即时规则执行动作。
    /// </summary>
    [Sample]
    public sealed class SchedulerExample : SampleBase
    {
        /// <summary>
        /// 示例标题。
        /// </summary>
        public override string Title => "Host Timer and Immediate Rules";

        /// <summary>
        /// 示例覆盖的职责边界。
        /// </summary>
        public override string Description => "宿主推进定时器，规则运行器只处理到期事件。";

        /// <summary>
        /// 此示例归入触发规则分类。
        /// </summary>
        public override SampleCategory Category => SampleCategory.Triggering;

        protected override void OnRun()
        {
            var timer = new DefaultScheduler();
            var bus = new EventBus();
            var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), new ActionRegistry());
            var due = new EventKey<int>(StableStringId.Get("sample:triggering:timer:due"));
            var executions = 0;
            using var registration = runner.Register(due,
                new DelegateTrigger<int, object>((_, _) => true, (_, _) => executions++));
            timer.ScheduleDelay(() => bus.Publish(due, 1), 0.25f);

            timer.Tick(0.125f);
            if (executions != 0)
                throw new InvalidOperationException("到期前不应执行规则。");

            timer.Tick(0.125f);
            if (executions != 1)
                throw new InvalidOperationException("到期时应执行一次规则。");

            timer.Tick(0.25f);
            if (executions != 1)
                throw new InvalidOperationException("一次性定时任务不应重复发布事件。");

            Log("定时器到期后，已通过即时规则执行一次动作。");
        }
    }
}
