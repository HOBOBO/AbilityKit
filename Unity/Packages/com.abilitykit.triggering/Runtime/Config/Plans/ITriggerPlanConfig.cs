using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Config.Actions;
using AbilityKit.Triggering.Runtime.Config.Cue;
using AbilityKit.Triggering.Runtime.Config.Predicates;
using AbilityKit.Triggering.Runtime.Config.Schedule;

namespace AbilityKit.Triggering.Runtime.Config.Plans
{
    /// <summary>
    /// 触发器计划配置（静态配置数据，可序列化）
    /// </summary>
    public interface ITriggerPlanConfig
    {
        int TriggerId { get; }
        int EventId { get; }
        string EventName { get; }
        int Phase { get; }
        int Priority { get; }
        int InterruptPriority { get; }
        IPredicateConfig Predicate { get; }
        IReadOnlyList<IActionCallConfig> Actions { get; }
        IScheduleConfig Schedule { get; }
        ICueConfig Cue { get; }
    }
}