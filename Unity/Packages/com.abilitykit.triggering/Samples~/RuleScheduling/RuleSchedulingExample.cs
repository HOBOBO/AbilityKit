using System;
using AbilityKit.Triggering.Runtime.RuleScheduler;

namespace AbilityKit.Triggering.Runtime.Example
{
    /// <summary>
    /// 演示宿主显式登记和推进业务定时规则，不依赖触发主线样例。
    /// </summary>
    public static class RuleSchedulingExample
    {
        /// <summary>
        /// 以模拟的毫秒增量推进规则；登记失败、提前或重复执行时抛出异常。
        /// </summary>
        public static void RunOnce()
        {
            var executionCount = 0;
            var scheduler = new RuleSchedulerRegistry();
            var handle = scheduler.Schedule(
                RuleSchedulePlan.After(250f, groupId: "sample:rules", subjectId: "notice", label: "delay"),
                new DelegateRuleScheduleEffect(_ => executionCount++));

            scheduler.Update(249f);
            if (!handle.IsValid || executionCount != 0)
                throw new InvalidOperationException("规则必须成功登记，且到期前不应执行。");

            scheduler.Update(1f);
            if (executionCount != 1)
                throw new InvalidOperationException("规则应在宿主推进至到期时间时执行。");

            scheduler.Update(250f);
            if (executionCount != 1)
                throw new InvalidOperationException("一次性规则到期后不能重复执行。");
        }
    }
}
